namespace SkillExam.Core.Models;

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    TrueFalse,
    ShortAnswer
}

public static class QuestionTypeExtensions
{
    public static string ToDisplayName(this QuestionType type) => type switch
    {
        QuestionType.SingleChoice => "单选题",
        QuestionType.MultipleChoice => "多选题",
        QuestionType.TrueFalse => "判断题",
        QuestionType.ShortAnswer => "简答题",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool TryParseDisplayName(string? value, out QuestionType type)
    {
        type = value?.Trim() switch
        {
            "单选题" or "单选" => QuestionType.SingleChoice,
            "多选题" or "多选" => QuestionType.MultipleChoice,
            "判断题" or "判断" => QuestionType.TrueFalse,
            "简答题" or "简答" => QuestionType.ShortAnswer,
            _ => (QuestionType)(-1)
        };
        return Enum.IsDefined(type);
    }
}
