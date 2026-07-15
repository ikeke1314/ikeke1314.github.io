using Serilog;
using Serilog.Events;
using SkillExam.Infrastructure.Persistence;

namespace SkillExam.Infrastructure.Logging;

public static class LoggingConfigurator
{
    public static ILogger CreateLogger(AppDataPaths paths)
    {
        paths.EnsureCreated();
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "SkillExam")
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "skill-exam-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();
    }
}
