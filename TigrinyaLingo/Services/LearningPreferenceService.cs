using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TigrinyaLingo.Services;

public class LearningPreferenceService(ApplicationDbContext context)
{
    public Task<UserLearningPreference?> FindAsync(string userId) => context.UserLearningPreferences.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId);
    public static string Label(LearningExperience experience) => experience switch {
        LearningExperience.SomeUnderstanding => "Grundförståelse", LearningExperience.Familiar => "Jag kan redan en del", _ => "Nybörjare"
    };
    // Gäller startkursen. Andra kurser behåller sin egen ordning.
    // Om senare innehåll saknas används första återstående färdiga lektion.
    public static int StartingOrder(LearningExperience? experience) => experience switch {
        LearningExperience.SomeUnderstanding => 4, LearningExperience.Familiar => 14, _ => 1
    };
}
