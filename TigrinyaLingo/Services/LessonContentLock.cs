using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TigrinyaLingo.Services;

// Frågeantalet måste vara stabilt medan ett resultat sparas.
public static class LessonContentLock
{
    public static Task ReadAsync(ApplicationDbContext context, int lessonId) => AcquireAsync(context, lessonId, "Shared");
    public static Task WriteAsync(ApplicationDbContext context, int lessonId) => AcquireAsync(context, lessonId, "Exclusive");

    private static Task AcquireAsync(ApplicationDbContext context, int lessonId, string mode)
    {
        var resource = "TigrinyaLingo:LessonContent:" + lessonId;
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = {mode}, @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 50001, N'Lektionen används just nu. Försök igen.', 1;
            """);
    }
}
