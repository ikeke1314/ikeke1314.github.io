namespace SkillExam.Core.Models;

public enum SkillLevel
{
    Level1 = 1,
    Level2,
    Level3,
    Level4,
    Level5,
    Level6
}

public static class SkillLevelExtensions
{
    private static readonly string[] Names = ["一级", "二级", "三级", "四级", "五级", "六级"];

    public static string ToDisplayName(this SkillLevel level) => Names[(int)level - 1];

    public static bool TryParseDisplayName(string? value, out SkillLevel level)
    {
        var index = Array.IndexOf(Names, value?.Trim());
        level = index >= 0 ? (SkillLevel)(index + 1) : (SkillLevel)0;
        return index >= 0;
    }

    public static IReadOnlyList<SkillLevel> All { get; } = Enum.GetValues<SkillLevel>();
}
