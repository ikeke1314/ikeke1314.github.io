namespace SkillExam.Infrastructure.Persistence;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PEGroup",
            "SkillExam");
        DatabasePath = Path.Combine(RootDirectory, "skill-exam.db");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
    }

    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string LogsDirectory { get; }
    public string BackupsDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
