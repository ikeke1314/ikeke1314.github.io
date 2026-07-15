namespace SkillExam.Infrastructure.Tests;

internal static class TestPaths
{
    public static string PcAppRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SkillExam.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("无法定位 pc-app 根目录。");
        }
    }

    public static string RealQuestionBank => Path.Combine(PcAppRoot, "exam_bank", "PE 技能士题库.xlsx");
}
