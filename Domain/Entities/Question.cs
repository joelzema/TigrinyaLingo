namespace Domain.Entities
{
    public class Question
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;

        // Tigrinja med latinska bokstäver, till exempel selam.
        public string LatinText { get; set; } = string.Empty;

        public PhraseAudience Audience { get; set; }
        public string? TenseGroup { get; set; }
        public TimeReference TimeReference { get; set; }
        public string? TenseContext { get; set; }
        public string? TenseNotes { get; set; }
        public bool TenseReviewed { get; set; }

        // Namnet på en lokal inspelning under wwwroot/audio/tigrinya.
        public string? AudioFileName { get; set; }

        public int LessonId { get; set; }

        public Lesson? Lesson { get; set; }

        public ICollection<Choice> Choices { get; set; } = new List<Choice>();
    }
}
