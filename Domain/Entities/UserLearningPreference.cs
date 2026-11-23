namespace Domain.Entities;

public enum LearningExperience { Beginner = 1, SomeUnderstanding = 2, Familiar = 3 }

// Självskattning för vägledning, aldrig ett lektionsresultat eller en XP-belöning.
public class UserLearningPreference
{
    public string UserId { get; set; } = string.Empty;
    public LearningExperience Experience { get; set; }
}
