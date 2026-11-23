namespace Domain.Entities
{
    public class UserProgress
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int LessonId { get; set; }

        public Lesson? Lesson { get; set; }

        public int Score { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
