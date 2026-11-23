using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;



namespace Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

 
        
        public DbSet<UserLearningPreference> UserLearningPreferences => Set<UserLearningPreference>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<Choice> Choices => Set<Choice>();
        public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
        public DbSet<QuestionReviewState> QuestionReviewStates => Set<QuestionReviewState>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Identity behöver först konfigurera sina användar- och kontotabeller.
            base.OnModelCreating(builder);

            builder.Entity<Course>()
                .HasMany(course => course.Lessons)
                .WithOne(lesson => lesson.Course)
                .HasForeignKey(lesson => lesson.CourseId);

            builder.Entity<Lesson>()
                .HasMany(lesson => lesson.Questions)
                .WithOne(question => question.Lesson)
                .HasForeignKey(question => question.LessonId);

            builder.Entity<Question>()
                .HasMany(question => question.Choices)
                .WithOne(choice => choice.Question)
                .HasForeignKey(choice => choice.QuestionId);

            builder.Entity<Question>()
                .Property(question => question.LatinText)
                .HasMaxLength(200);

            builder.Entity<Question>()
                .Property(question => question.AudioFileName)
                .HasMaxLength(160);

            builder.Entity<Question>().Property(question => question.TenseGroup).HasMaxLength(80);
            builder.Entity<Question>().Property(question => question.TenseContext).HasMaxLength(500);
            builder.Entity<Question>().Property(question => question.TenseNotes).HasMaxLength(500);

            builder.Entity<UserProgress>()
                .HasOne(progress => progress.Lesson)
                .WithMany()
                .HasForeignKey(progress => progress.LessonId);

            // Identity-kopplingen ligger här så Domain slipper bero på Identity.
            builder.Entity<UserProgress>()
                .HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(progress => progress.UserId);

            // En användare får en sammanfattande framstegsrad per lektion.
            builder.Entity<UserProgress>()
                .HasIndex(progress => new { progress.UserId, progress.LessonId })
                .IsUnique();

            builder.Entity<QuestionReviewState>()
                .HasOne(state => state.Question).WithMany().HasForeignKey(state => state.QuestionId);
            builder.Entity<QuestionReviewState>()
                .HasOne<IdentityUser>().WithMany().HasForeignKey(state => state.UserId);
            builder.Entity<QuestionReviewState>()
                .HasIndex(state => new { state.UserId, state.QuestionId }).IsUnique();

            builder.Entity<UserLearningPreference>().HasKey(row => row.UserId);
            builder.Entity<UserLearningPreference>().Property(row => row.UserId).HasMaxLength(450);
            builder.Entity<UserLearningPreference>().HasOne<IdentityUser>().WithMany().HasForeignKey(row => row.UserId);

            LearningSeedData.Configure(builder);
        }
    }
}
