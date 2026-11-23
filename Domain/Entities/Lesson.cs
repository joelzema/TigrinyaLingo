namespace Domain.Entities
{
    public class Lesson
    {
        public string Title { get; set; } = string.Empty; 

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int Id { get; set; }  

        public int Order { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
