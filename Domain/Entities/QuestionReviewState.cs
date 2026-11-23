namespace Domain.Entities;

// Senaste rättningen per användare och fråga, inte en historik över alla försök.
public class QuestionReviewState
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public bool NeedsReview { get; set; }
    public DateTime LastAnsweredAt { get; set; }
}
