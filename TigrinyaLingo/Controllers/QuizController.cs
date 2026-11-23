using System.Security.Claims;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TigrinyaLingo.Services;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly QuizProgressStore _progress;
        private readonly LearningPathService _path;
        private readonly TenseLearningStore _tenses;
        private readonly LearningPreferenceService _preferences;
        public QuizController(ApplicationDbContext context, QuizProgressStore progress, LearningPathService path, TenseLearningStore tenses, LearningPreferenceService preferences) { _context = context; _progress = progress; _path = path; _tenses = tenses; _preferences = preferences; }

        public async Task<IActionResult> Index(int id)
        {
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            if (await _preferences.FindAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!) is null)
                return RedirectToAction("Index", "LearningPreferences", new { returnUrl = Url.Action("Index", "Quiz", new { id }) });
            var model = BuildQuiz(lesson);
            model.StatusMessage = TempData["LessonMessage"] as string;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, QuizSubmissionInput input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            if (!IsReady(lesson)) return BadRequest("Lektionen har inget färdigt quiz.");

            // Frågorna ska vara exakt de frågor som hör till den här lektionen.
            if (input.Answers is null || !MatchesQuestions(lesson, input.Answers))
                return BadRequest("Svarens frågor stämmer inte med lektionen.");

            foreach (var answer in input.Answers)
            {
                var question = lesson.Questions.Single(question => question.Id == answer.QuestionId);
                if (answer.ChoiceId is not null && !question.Choices.Any(choice => choice.Id == answer.ChoiceId))
                    return BadRequest("Svarsalternativet hör inte till frågan.");
            }

            if (!ModelState.IsValid || input.Answers.Any(answer => answer.ChoiceId is null))
            {
                ModelState.AddModelError(string.Empty, "Svara på alla frågor innan du rättar quizet.");
                return View("Index", BuildQuiz(lesson, input.Answers));
            }

            // Poängen beräknas från databasens facit. Klienten skickar bara svarens ID:n.
            var earnedXp = 0;
            await using (var transaction = await _progress.BeginLessonUpdateAsync(userId, lesson.Id))
            {
                // Innehåll kan ha lagts till efter att den första formulärkontrollen gjordes.
                // Ladda om under det delade innehållslåset innan ett resultat kan sparas.
                var currentLesson = await LoadLessonAsync(id);
                if (currentLesson is null) return NotFound();
                if (!IsReady(currentLesson) || !MatchesQuestions(currentLesson, input.Answers)
                    || input.Answers.Any(answer => !currentLesson.Questions.Single(question => question.Id == answer.QuestionId)
                        .Choices.Any(choice => choice.Id == answer.ChoiceId)))
                {
                    TempData["LessonMessage"] = "Lektionen har uppdaterats. Här är den aktuella versionen att öva på.";
                    return RedirectToAction(nameof(Index), new { id });
                }
                earnedXp = await _progress.SaveAnswersAsync(userId, currentLesson, input.Answers, fullQuiz: true);
                await transaction.CommitAsync();
            }

            TempData["QuizAttempt"] = JsonSerializer.Serialize(new QuizAttemptData
            {
                UserId = userId, LessonId = lesson.Id, Answers = input.Answers, EarnedXp = earnedXp
            });
            return RedirectToAction(nameof(Result), new { id = lesson.Id });
        }

        public async Task<IActionResult> Result(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            var progress = await _context.UserProgresses.AsNoTracking()
                .SingleOrDefaultAsync(progress => progress.UserId == userId && progress.LessonId == id);
            if (progress is null) return RedirectToAction(nameof(Index), new { id });

            var model = new QuizResultViewModel
            {
                CourseId = lesson.CourseId, LessonId = id, LessonTitle = lesson.Title, QuestionCount = lesson.Questions.Count,
                TotalXp = await _context.UserProgresses.Where(saved => saved.UserId == userId)
                    .Select(saved => saved.Score * LearningRewards.CorrectAnswerXp + (saved.CompletedAt != null ? LearningRewards.CompletionBonus : 0)).SumAsync(),
                PendingReviewCount = await PendingReviewCountAsync(userId, id),
                BestScore = progress.Score, IsCompleted = progress.CompletedAt.HasValue
            };
            if (model.IsCompleted)
            {
                model.NextLesson = (await _path.CoursesAsync(userId, lesson.CourseId)).SingleOrDefault()?.NextLesson;
                model.TensePractice = (await _tenses.ModelsAsync(userId, false))
                    .Where(group => group.Words.Any(word => word.LessonId == id)).ToList();
            }
            var serialized = TempData["QuizAttempt"] as string;
            if (serialized is not null)
            {
                try
                {
                    var attempt = JsonSerializer.Deserialize<QuizAttemptData>(serialized);
                    if (attempt is not null && attempt.UserId == userId && attempt.LessonId == id
                        && IsReady(lesson) && MatchesQuestions(lesson, attempt.Answers)
                        && attempt.Answers.All(answer => lesson.Questions.Single(question => question.Id == answer.QuestionId)
                            .Choices.Any(choice => choice.Id == answer.ChoiceId)))
                    {
                        model.EarnedXp = attempt.EarnedXp;
                        model.Answers = Grade(lesson, attempt.Answers);
                        model.AttemptScore = model.Answers.Count(answer => answer.IsCorrect);
                    }
                }
                catch (JsonException)
                {
                    // Det sparade framsteget kan fortfarande visas om försöksinformationen saknas.
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Review(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            await KeepPendingQuestionsAsync(lesson, userId);
            var model = BuildQuiz(lesson, isReview: true);
            model.StatusMessage = TempData["ReviewMessage"] as string;
            return View("Index", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int id, QuizSubmissionInput input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            await using var transaction = await _progress.BeginLessonUpdateAsync(userId, id);
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            if (!IsReady(lesson)) return BadRequest("Lektionen har inget färdigt quiz.");

            if (input.Answers is null || input.Answers.Any(answer => answer is null)
                || input.Answers.Select(answer => answer.QuestionId).Distinct().Count() != input.Answers.Count
                || input.Answers.Any(answer => !lesson.Questions.Any(question => question.Id == answer.QuestionId)))
                return BadRequest("Svarens frågor stämmer inte med lektionen.");

            await KeepPendingQuestionsAsync(lesson, userId);
            // En annan flik kan ha rättat orden sedan formuläret öppnades.
            if (lesson.Questions.Count == 0 || !MatchesQuestions(lesson, input.Answers))
            {
                TempData["ReviewMessage"] = "Dina repetitionsord har ändrats. Här visas den aktuella listan.";
                return RedirectToAction(nameof(Review), new { id });
            }
            foreach (var answer in input.Answers)
            {
                var question = lesson.Questions.Single(question => question.Id == answer.QuestionId);
                if (answer.ChoiceId is not null && !question.Choices.Any(choice => choice.Id == answer.ChoiceId))
                    return BadRequest("Svarsalternativet hör inte till frågan.");
            }
            if (!ModelState.IsValid || input.Answers.Any(answer => answer.ChoiceId is null))
            {
                ModelState.AddModelError(string.Empty, "Svara på alla frågor innan du rättar övningen.");
                return View("Index", BuildQuiz(lesson, input.Answers, isReview: true));
            }
            await _progress.SaveAnswersAsync(userId, lesson, input.Answers, fullQuiz: false);
            await transaction.CommitAsync();
            TempData["ReviewAttempt"] = JsonSerializer.Serialize(new QuizAttemptData
            {
                UserId = userId, LessonId = id, Answers = input.Answers
            });
            return RedirectToAction(nameof(ReviewResult), new { id });
        }

        public async Task<IActionResult> ReviewResult(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            var model = new QuizResultViewModel
            {
                IsReview = true, CourseId = lesson.CourseId, LessonId = id, LessonTitle = lesson.Title,
                PendingReviewCount = await PendingReviewCountAsync(userId, id)
            };
            if (TempData["ReviewAttempt"] is string serialized)
            {
                try
                {
                    var attempt = JsonSerializer.Deserialize<QuizAttemptData>(serialized);
                    if (attempt?.UserId == userId && attempt.LessonId == id && attempt.Answers is { Count: > 0 }
                        && attempt.Answers.All(answer => answer is not null)
                        && attempt.Answers.Select(answer => answer.QuestionId).Distinct().Count() == attempt.Answers.Count
                        && attempt.Answers.All(answer => lesson.Questions.Any(question => question.Id == answer.QuestionId
                            && question.Choices.Any(choice => choice.Id == answer.ChoiceId))))
                    {
                        var ids = attempt.Answers.Select(answer => answer.QuestionId).ToHashSet();
                        lesson.Questions = lesson.Questions.Where(question => ids.Contains(question.Id)).ToList();
                        if (IsReady(lesson))
                        {
                            model.EarnedXp = attempt.EarnedXp;
                        model.Answers = Grade(lesson, attempt.Answers);
                            model.AttemptScore = model.Answers.Count(answer => answer.IsCorrect);
                            model.QuestionCount = model.Answers.Count;
                        }
                    }
                }
                catch (JsonException) { /* Vid omladdning visas fortfarande sparat antal repetitionsord. */ }
            }
            return View("Result", model);
        }

        private IActionResult Locked(Lesson lesson, LessonListItemViewModel access)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("Locked", new LockedLessonViewModel(lesson.Title, lesson.CourseId,
                access.RequiredLessonId!.Value, access.RequiredLessonTitle!));
        }
        private Task<int> PendingReviewCountAsync(string userId, int lessonId) =>
            _context.QuestionReviewStates.CountAsync(state => state.UserId == userId
                && state.Question!.LessonId == lessonId && state.NeedsReview);

        private async Task KeepPendingQuestionsAsync(Lesson lesson, string userId)
        {
            var ids = await _context.QuestionReviewStates.AsNoTracking()
                .Where(state => state.UserId == userId && state.Question!.LessonId == lesson.Id && state.NeedsReview)
                .Select(state => state.QuestionId).ToListAsync();
            lesson.Questions = lesson.Questions.Where(question => ids.Contains(question.Id)).ToList();
        }

        private Task<Lesson?> LoadLessonAsync(int id) => _context.Lessons.AsNoTracking()
            .Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .SingleOrDefaultAsync(lesson => lesson.Id == id);

        private static bool IsReady(Lesson lesson) => LearningPathService.IsReady(lesson);

        private static bool MatchesQuestions(Lesson lesson, List<QuizAnswerInput> answers) =>
            answers.Count == lesson.Questions.Count && answers.All(answer => answer is not null)
            && answers.Select(answer => answer.QuestionId).Distinct().Count() == answers.Count
            && answers.All(answer => lesson.Questions.Any(question => question.Id == answer.QuestionId));

        private static Dictionary<string, string> LatinOptions(Lesson lesson, Question question)
        {
            // Bara entydiga betydelser får vändas. Shimka/shimki delar betydelse och
            // används därför inte som konkurrerande alternativ till samma svenska fras.
            var meanings = lesson.Questions.Where(item => !string.IsNullOrWhiteSpace(item.LatinText))
                .Select(item => new { item.LatinText, Meaning = item.Choices.SingleOrDefault(choice => choice.IsCorrect)?.Text })
                .Where(item => item.Meaning is not null).GroupBy(item => item.Meaning!)
                .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().LatinText);
            return question.Choices.Count == 4 && question.Choices.All(choice => meanings.ContainsKey(choice.Text))
                && question.Choices.Select(choice => meanings[choice.Text]).Distinct().Count() == 4
                ? meanings : new Dictionary<string, string>();
        }

        internal static QuizViewModel BuildQuiz(Lesson lesson, List<QuizAnswerInput>? answers = null, bool isReview = false)
        {
            var model = new QuizViewModel
            {
                IsReview = isReview, CourseId = lesson.CourseId, LessonId = lesson.Id, LessonTitle = lesson.Title, IsAvailable = IsReady(lesson)
            };
            var ordered = lesson.Questions.OrderBy(question => question.Id).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var question = ordered[index];
                var latin = model.IsAvailable ? LatinOptions(lesson, question) : new Dictionary<string, string>();
                var kind = index % 3 == 2 && latin.Count > 0 ? ExerciseKind.Translate
                    : index % 3 == 1 || string.IsNullOrWhiteSpace(question.AudioFileName) ? ExerciseKind.Read : ExerciseKind.Listen;
                model.Questions.Add(new QuizQuestionViewModel
                {
                    Id = question.Id, Kind = kind, LatinText = question.LatinText, Audience = question.Audience, TenseHint = TenseGuide.For(question), AudioFileName = question.AudioFileName,
                    StudyMeaning = !isReview ? question.Choices.SingleOrDefault(choice => choice.IsCorrect)?.Text ?? string.Empty : string.Empty,
                    Prompt = kind == ExerciseKind.Translate ? question.Choices.Single(choice => choice.IsCorrect).Text : question.LatinText,
                    SelectedChoiceId = answers?.SingleOrDefault(answer => answer.QuestionId == question.Id)?.ChoiceId,
                    Choices = question.Choices.OrderBy(_ => Random.Shared.Next()).Select(choice => new QuizChoiceViewModel
                    {
                        Id = choice.Id, Text = kind == ExerciseKind.Translate ? latin[choice.Text] : choice.Text,
                        UsageLabel = kind == ExerciseKind.Translate ? PhraseUsageGuide.For(latin[choice.Text],
                            lesson.Questions.Single(item => item.Choices.SingleOrDefault(answer => answer.IsCorrect)?.Text == choice.Text).Audience)?.Label : null
                    }).ToList()
                });
            }
            return model;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckAnswer(int id, QuizAnswerInput input)
        {
            if (!ModelState.IsValid || input.ChoiceId is null) return BadRequest();
            var lesson = await LoadLessonAsync(id);
            if (lesson is null) return NotFound();
            var question = lesson.Questions.SingleOrDefault(question => question.Id == input.QuestionId);
            if (question is null || !question.Choices.Any(choice => choice.Id == input.ChoiceId) || !IsReady(lesson))
                return BadRequest();
            var latin = LatinOptions(lesson, question);
            if (input.Kind == ExerciseKind.Translate && latin.Count == 0) return BadRequest();
            var correct = question.Choices.Single(choice => choice.IsCorrect);
            // Direkt återkoppling sparar ingenting. Hela inskickningen rättas på nytt
            // på servern innan framsteg eller XP kan sparas.
            return Json(new
            {
                isCorrect = correct.Id == input.ChoiceId,
                correctChoiceId = correct.Id,
                correctAnswer = input.Kind == ExerciseKind.Translate ? question.LatinText : correct.Text,
                explanation = TenseGuide.Explanation(question)
            });
        }

        private static List<QuizQuestionResultViewModel> Grade(Lesson lesson, List<QuizAnswerInput> answers) =>
            lesson.Questions.Where(TenseGuide.IsPublished).OrderBy(question => question.Id).Select(question =>
            {
                var answer = answers.Single(answer => answer.QuestionId == question.Id);
                var selected = question.Choices.Single(choice => choice.Id == answer.ChoiceId);
                var correct = question.Choices.Single(choice => choice.IsCorrect);
                var latin = answer.Kind == ExerciseKind.Translate ? LatinOptions(lesson, question) : new Dictionary<string, string>();
                return new QuizQuestionResultViewModel
                {
                    LatinText = question.LatinText, Audience = question.Audience, TenseHint = TenseGuide.For(question), IsLatinAnswer = latin.Count > 0,
                    SelectedAnswer = latin.Count > 0 ? latin[selected.Text] : selected.Text,
                    CorrectAnswer = latin.Count > 0 ? latin[correct.Text] : correct.Text, IsCorrect = selected.Id == correct.Id
                };
            }).ToList();
    }
}