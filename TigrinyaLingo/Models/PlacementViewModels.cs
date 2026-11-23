using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace TigrinyaLingo.Models;

public record PlacementChoice(Guid Key, string Text);
public record PlacementQuestion(Guid Key, string Area, string Prompt, List<PlacementChoice> Choices, Guid? Selected = null);
public record PlacementPlay(string Token, List<PlacementQuestion> Questions, string? Message = null);
public class PlacementSubmission
{
    [Required, StringLength(4000)] public string Token { get; set; } = "";
    public List<PlacementAnswer> Answers { get; set; } = new();
}
public class PlacementAnswer
{
    public Guid QuestionKey { get; set; }
    public Guid? ChoiceKey { get; set; }
}
public record PlacementPracticeCard(string Area, string Prompt, List<PlacementChoice> Choices, Guid CorrectKey, string Explanation);
public record PlacementPracticePage(string Token, PlacementPracticeCard Card, int Step, int Total, Guid? Choice);
public record PlacementArea(string Name, int Correct, int Total, int Skipped);
public record PlacementFeedback(string Area, string Prompt, string Answer, string CorrectAnswer, bool Correct, bool Skipped, string? Explanation);
public record PlacementResult(int Correct, int Total, int Skipped, LearningExperience SuggestedExperience,
    int LessonId, string LessonTitle, string Advice, List<PlacementArea> Areas, List<PlacementFeedback> Answers);
