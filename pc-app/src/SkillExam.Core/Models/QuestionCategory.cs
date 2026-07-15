namespace SkillExam.Core.Models;

public enum QuestionCategory
{
    BaseStation,
    GroundRobot,
    WindowRobot,
    OpticalComponent
}

public static class QuestionCategoryExtensions
{
    public static string ToDisplayName(this QuestionCategory category) => category switch
    {
        QuestionCategory.BaseStation => "基站",
        QuestionCategory.GroundRobot => "地宝",
        QuestionCategory.WindowRobot => "窗宝",
        QuestionCategory.OpticalComponent => "光学组件",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static IReadOnlyList<QuestionCategory> All { get; } = Enum.GetValues<QuestionCategory>();
}
