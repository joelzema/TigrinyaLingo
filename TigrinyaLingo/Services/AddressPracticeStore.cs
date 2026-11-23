using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

internal record AddressConcept(string Meaning, string Man, string Request, string PartnerLine);
internal record AddressKnownPair(AddressConcept Concept, string Man, string Woman, List<Question> Sources);
internal record AddressContent(List<AddressKnownPair> Pairs, List<Question> KnownQuestions);
internal record PracticeSourceSnapshot(int QuestionId, int LessonId, string Fingerprint, long? LastAnsweredTicks, bool Review);

// Motformerna kommer från samma granskade guide som ordkorten, inte från ändelseregler.
public class AddressPracticeStore(ApplicationDbContext context)
{
    private static readonly AddressConcept[] Concepts = {
        new("Vad heter du?", "menyu shimka", "Fråga vad personen heter.", "Hej! Vad roligt att träffas."),
        new("Hur mår du?", "kemey aleka", "Fråga hur personen mår.", "Hej! Det var länge sedan vi sågs."),
        new("Var kommer du ifrån?", "kabey adi metsika", "Fråga var personen kommer ifrån.", "Jag vill gärna lära känna dig bättre."),
        new("God morgon", "kemey hadirka", "Det är morgon. Välj en morgonhälsning.", "God morgon! Redo för en ny dag?"),
        new("God eftermiddag", "kemey wu’elka", "Ni ses efter lunch. Välj en eftermiddagshälsning.", "Vad trevligt att ses i eftermiddag."),
        new("God kväll", "kemey amsika", "Det är kväll. Välj en kvällshälsning.", "Hej! Nu har det blivit kväll."),
        new("Kan du prata långsammare?", "kes elka tezareb bejaka", "Personen talar för snabbt. Be om långsammare tal.", "Ta andra gatan, sedan höger och fortsätt förbi hållplatsen …"),
        new("Kan du säga det igen?", "eske digemeley", "Du missade vad personen sa. Be om en upprepning.", "Vi träffas vid …"),
        new("Kan du skriva ner det?", "tsehafo bejaka", "Be personen skriva ner det som har sagts.", "Jag kan ge dig adressen."),
        new("Snälla", "bejaka", "Du ber om en tjänst. Välj ordet för snälla.", "Vill du att jag hjälper dig?"),
        new("Pratar du engelska?", "englizgna tzarebdo", "Fråga om personen pratar engelska.", "Jag vill gärna prata med dig."),
        new("Pratar du tigrinja?", "tigrigna tzarebdo", "Fråga om personen pratar tigrinja.", "Vilket språk vill du prata?"),
        new("Och du?", "bwegenka ke", "Du har berättat hur du mår. Ställ samma fråga tillbaka.", "Tack för att du berättar!"),
        new("Hej då", "dehan kun", "Mötet är slut. Säg hej då till personen.", "Nu behöver jag gå."),
        new("Varsågod", "genzebka", "Personen tackar dig. Svara med uttrycket för ingen orsak.", "Tack för hjälpen!")
    };

    internal async Task<AddressContent> LoadAsync(string userId)
    {
        var lessons = await context.Lessons.AsNoTracking().Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .Where(lesson => context.UserProgresses.Any(saved => saved.UserId == userId && saved.LessonId == lesson.Id && saved.CompletedAt != null))
            .ToListAsync();
        var known = lessons.Where(LearningPathService.IsReady).SelectMany(lesson => lesson.Questions)
            .Where(question => !string.IsNullOrWhiteSpace(question.LatinText)).OrderBy(question => question.Id).ToList();
        var pairs = new List<AddressKnownPair>();
        foreach (var concept in Concepts)
        {
            var woman = PhraseUsageGuide.For(concept.Man)?.OtherForm;
            if (woman is null) continue;
            var sources = known.Where(question => SameMeaning(question.Choices.Single(choice => choice.IsCorrect).Text, concept.Meaning)
                && ((PhraseUsageGuide.SameForm(question.LatinText, concept.Man) && question.Audience is PhraseAudience.Automatic or PhraseAudience.Man)
                    || (PhraseUsageGuide.SameForm(question.LatinText, woman) && question.Audience is PhraseAudience.Automatic or PhraseAudience.Woman)))
                .ToList();
            if (sources.Count == 0) continue;
            var manText = sources.FirstOrDefault(question => PhraseUsageGuide.SameForm(question.LatinText, concept.Man))?.LatinText ?? concept.Man;
            var womanText = sources.FirstOrDefault(question => PhraseUsageGuide.SameForm(question.LatinText, woman))?.LatinText ?? woman;
            // Fyra olika alternativ: båda tilltalen och två andra redan mötta uttryck.
            if (known.Where(question => !SameMeaning(question.Choices.Single(choice => choice.IsCorrect).Text, concept.Meaning) && !PhraseUsageGuide.SameForm(question.LatinText, manText) && !PhraseUsageGuide.SameForm(question.LatinText, womanText))
                .Select(question => PhraseUsageGuide.FormKey(question.LatinText)).Distinct().Count() < 2) continue;
            pairs.Add(new AddressKnownPair(concept, manText, womanText, sources));
        }
        return new AddressContent(pairs, known);
    }

    private static bool SameMeaning(string left, string right) => string.Equals(left.Trim().TrimEnd('?', '!', '.', '…'),
        right.Trim().TrimEnd('?', '!', '.', '…'), StringComparison.OrdinalIgnoreCase);

    internal async Task<ConversationSession?> CreateAsync(string userId)
    {
        var content = await LoadAsync(userId);
        if (content.Pairs.Count == 0) return null;
        var states = await context.QuestionReviewStates.AsNoTracking().Where(state => state.UserId == userId)
            .ToDictionaryAsync(state => state.QuestionId);
        var selected = content.Pairs.OrderByDescending(pair => pair.Sources.Any(question => states.GetValueOrDefault(question.Id)?.NeedsReview == true))
            .ThenBy(pair => pair.Sources.Min(question => states.GetValueOrDefault(question.Id)?.LastAnsweredAt ?? DateTime.MinValue))
            .ThenBy(_ => Random.Shared.Next()).Take(3).ToList();
        var dependencies = new Dictionary<int, Question>();
        var turns = new List<ConversationSessionTurn>();
        foreach (var pair in selected)
        {
            foreach (var source in pair.Sources) dependencies.TryAdd(source.Id, source);
            foreach (var audience in new[] { PhraseAudience.Man, PhraseAudience.Woman })
            {
                var correct = audience == PhraseAudience.Man ? pair.Man : pair.Woman;
                var other = audience == PhraseAudience.Man ? pair.Woman : pair.Man;
                var person = audience == PhraseAudience.Man ? "man" : "kvinna";
                var otherPerson = audience == PhraseAudience.Man ? "kvinna" : "man";
                var question = pair.Sources.FirstOrDefault(item => PhraseUsageGuide.SameForm(item.LatinText, correct)) ?? pair.Sources[0];
                var distractors = content.KnownQuestions.Where(item => !SameMeaning(item.Choices.Single(choice => choice.IsCorrect).Text, pair.Concept.Meaning) && !PhraseUsageGuide.SameForm(item.LatinText, correct) && !PhraseUsageGuide.SameForm(item.LatinText, other))
                    .OrderBy(_ => Random.Shared.Next()).DistinctBy(item => PhraseUsageGuide.FormKey(item.LatinText)).Take(2).ToList();
                foreach (var distractor in distractors) dependencies.TryAdd(distractor.Id, distractor);
                var choices = new[] { correct, other }.Concat(distractors.Select(item => item.LatinText)).OrderBy(_ => Random.Shared.Next())
                    .Select(text => new ConversationChoiceViewModel(Guid.NewGuid(), text)).ToList();
                turns.Add(new ConversationSessionTurn(Guid.NewGuid(), question.Id, question.LessonId, correct, pair.Concept.Meaning,
                    $"Du pratar med en {person}. {pair.Concept.Request}", pair.Concept.PartnerLine, ConversationContentStore.Fingerprint(question),
                    states.GetValueOrDefault(question.Id)?.LastAnsweredAt.Ticks, choices.Single(choice => choice.Text == correct).Key, choices, audience,
                    $"Du talar till en {person}, därför används {correct}. Till en {otherPerson} säger du {other}."));
            }
        }
        var reviewIds = turns.Select(turn => turn.QuestionId).ToHashSet();
        return new ConversationSession {
            UserId = userId, SceneId = "address-practice", Title = "Välj rätt tilltal", Turns = turns.OrderBy(_ => Random.Shared.Next()).ToList(),
            Sources = dependencies.Values.Select(question => new PracticeSourceSnapshot(question.Id, question.LessonId,
                ConversationContentStore.Fingerprint(question), states.GetValueOrDefault(question.Id)?.LastAnsweredAt.Ticks, reviewIds.Contains(question.Id))).ToList()
        };
    }
}
