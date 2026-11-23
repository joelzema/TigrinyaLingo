using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

internal record PlacementSource(int Id, string Fingerprint);
internal record PlacementTurn(PlacementQuestion Question, Guid CorrectKey, string CorrectAnswer, string? Explanation);
internal class PlacementSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public DateTime IssuedAt { get; } = DateTime.UtcNow;
    public List<PlacementTurn> Turns { get; } = new();
    public List<PlacementPracticeCard> PracticeCards { get; } = new();
    public List<PlacementSource> Sources { get; } = new();
    public SemaphoreSlim Gate { get; } = new(1,1);
    public string? SubmissionKey { get; set; }
    public PlacementResult? Result { get; set; }
}

// Ett litet urval av aktuellt, publicerat kursinnehåll. Ingen ny översättning skapas här.
public class PlacementStore(ApplicationDbContext context)
{
    private static readonly int[] SourceIds = { 1, 3, 10, 12, 13, 14, 2001, 2002, 2003 };
    internal async Task<PlacementSession?> CreateAsync(string userId)
    {
        var words = await context.Questions.AsNoTracking().Include(q=>q.Choices)
            .Where(q=>SourceIds.Contains(q.Id)).ToDictionaryAsync(q=>q.Id);
        if (words.Count != SourceIds.Length || words.Values.Any(q=>!TenseGuide.IsPublished(q) || string.IsNullOrWhiteSpace(q.LatinText)
            || q.Choices.Count!=4 || q.Choices.Count(c=>c.IsCorrect)!=1 || q.Choices.Select(c=>c.Text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=4)) return null;
        // Tilltalsfrågan kräver fortfarande rätt betydelse och rätt granskade mottagare.
        foreach(var (id,audience) in new[]{(13,PhraseAudience.Man),(14,PhraseAudience.Woman)})
            if (words[id].Choices.Single(c=>c.IsCorrect).Text.Trim().TrimEnd('?') != "Hur mår du"
                || PhraseUsageGuide.For(words[id].LatinText,words[id].Audience)?.Label != PhraseUsageGuide.Label(audience)) return null;
        foreach(var (id,time) in new[]{(2001,TimeReference.Present),(2002,TimeReference.Past),(2003,TimeReference.Future)})
            if (!TenseGuide.IsReviewed(words[id]) || words[id].TimeReference!=time) return null;
        var session = new PlacementSession { UserId=userId };
        foreach(var id in SourceIds)
        {
            var word=words[id];
            var area=id<=3 ? "Hälsningar" : id<=12 ? "Vardagsfraser" : id<=14 ? "Tilltal" : "Tidsformer";
            var prompt="Vad betyder uttrycket ”"+word.LatinText+"”?";
            string correct;
            List<string> options;
            if(id is 13 or 14)
            {
                prompt="Du frågar hur "+(id==13 ? "en man" : "en kvinna")+" mår. Vilken fras passar?";
                correct=word.LatinText;
                options=new[]{13,14,1,12}.Select(key=>words[key].LatinText).ToList();
            }
            else
            {
                correct=word.Choices.Single(c=>c.IsCorrect).Text;
                options=word.Choices.OrderBy(c=>c.Id).Select(c=>c.Text).ToList();
            }
            if(options.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=4) return null;
            var choices=options.Select(text=>new PlacementChoice(Guid.NewGuid(),text)).OrderBy(_=>Random.Shared.Next()).ToList();
            if (id is 13 or 14 or 2001 or 2002 or 2003)
            {
                var address = id is 13 or 14;
                var practicePrompt = id switch {
                    13 => "Du hälsar på en man. Hur frågar du hur han mår?",
                    14 => "Du hälsar på en kvinna. Hur frågar du hur hon mår?",
                    _ => word.TenseContext
                };
                var optionIds = address ? new[] { 13, 14, 1, 12 } : new[] { 2001, 2002, 2003, 1 };
                var practiceChoices = optionIds.Select(keyId => words[keyId].LatinText.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(text => new PlacementChoice(Guid.NewGuid(), text)).OrderBy(_ => Random.Shared.Next()).ToList();
                if (practiceChoices.Count == 4 && !string.IsNullOrWhiteSpace(practicePrompt))
                {
                    var explanation = address
                        ? $"Till en man säger du ”{words[13].LatinText}”. Till en kvinna säger du ”{words[14].LatinText}”. Det är den du talar med som avgör formen."
                        : $"{word.LatinText} betyder ”{correct}”. {TenseGuide.Explanation(word)}";
                    session.PracticeCards.Add(new(area, practicePrompt, practiceChoices,
                        practiceChoices.Single(c => c.Text == word.LatinText.Trim()).Key, explanation));
                }
            }
            var key=choices.Single(c=>c.Text==correct).Key;
            session.Turns.Add(new(new PlacementQuestion(Guid.NewGuid(),area,prompt,choices),key,correct,TenseGuide.Explanation(word)));
        }
        session.Sources.AddRange(words.Values.Select(q=>new PlacementSource(q.Id,ConversationContentStore.Fingerprint(q))));
        return session;
    }
    internal async Task<bool> IsCurrentAsync(PlacementSession session)
    {
        var ids=session.Sources.Select(s=>s.Id).ToArray();
        var words=await context.Questions.AsNoTracking().Include(q=>q.Choices).Where(q=>ids.Contains(q.Id)).ToListAsync();
        return words.Count==ids.Length && words.All(q=>session.Sources.Any(s=>s.Id==q.Id && s.Fingerprint==ConversationContentStore.Fingerprint(q)));
    }
    internal async Task<PlacementResult?> GradeAsync(PlacementSession session, List<PlacementAnswer> answers)
    {
        var feedback=session.Turns.Select(turn=>{
            var selected=answers.Single(a=>a.QuestionKey==turn.Question.Key).ChoiceKey!.Value;
            var skipped=selected==Guid.Empty;
            return new PlacementFeedback(turn.Question.Area,turn.Question.Prompt,
                skipped ? "Jag vet inte / hoppa över" : turn.Question.Choices.Single(c=>c.Key==selected).Text,
                turn.CorrectAnswer,selected==turn.CorrectKey,skipped,turn.Explanation);
        }).ToList();
        var areas=feedback.GroupBy(a=>a.Area).Select(g=>new PlacementArea(g.Key,g.Count(a=>a.Correct),g.Count(),g.Count(a=>a.Skipped))).ToList();
        var basics=feedback.Where(a=>a.Area is "Hälsningar" or "Vardagsfraser").Count(a=>a.Correct);
        var audience=areas.Single(a=>a.Name=="Tilltal").Correct;
        var experience=basics<3 ? LearningExperience.Beginner : audience<2 ? LearningExperience.SomeUnderstanding : LearningExperience.Familiar;
        var order=LearningPreferenceService.StartingOrder(experience);
        var lessons=await context.Lessons.AsNoTracking().Include(l=>l.Questions).ThenInclude(q=>q.Choices)
            .Where(l=>l.CourseId==1).OrderBy(l=>l.Order).ThenBy(l=>l.Id).ToListAsync();
        var ready=lessons.Where(LearningPathService.IsReady).ToList();
        var lesson=ready.FirstOrDefault(l=>l.Order>=order) ?? ready.FirstOrDefault();
        if(lesson is null) return null;
        var advice=experience switch {
            LearningExperience.Beginner => "Börja med hälsningar och korta vardagsfraser för att stärka grunderna.",
            LearningExperience.SomeUnderstanding => "Du känner igen flera grunduttryck. Fortsätt med småprat och öva särskilt på vem frasen riktas till.",
            _ => "Du känner igen många grunduttryck och tilltalsformer i urvalet. Bygg vidare med vardagsuttryck och tidsformer, eller repetera valfria lektioner."
        };
        return new(feedback.Count(a=>a.Correct),feedback.Count,feedback.Count(a=>a.Skipped),experience,lesson.Id,lesson.Title,advice,areas,feedback);
    }
}
