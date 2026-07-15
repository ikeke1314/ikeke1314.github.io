using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Tests;

internal static class TestQuestions
{
    public static Question Create(
        QuestionType type,
        string text,
        string answer = "A",
        SkillLevel level = SkillLevel.Level1,
        QuestionCategory? category = null,
        string source = "测试来源")
    {
        var options = type == QuestionType.ShortAnswer
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["A"] = "正确项", ["B"] = "错误项" };
        return new Question
        {
            Id = QuestionIdentity.Create(type, text, options, answer, source),
            Type = type,
            Text = text,
            Options = options,
            Answer = answer,
            Levels = new HashSet<SkillLevel> { level },
            Source = source,
            SourceSheet = "Sheet1",
            Categories = category is null ? new HashSet<QuestionCategory>() : new HashSet<QuestionCategory> { category.Value }
        };
    }

    public static IReadOnlyList<Question> CompleteBank(int perTypeAndLevel = 80) =>
        (from type in Enum.GetValues<QuestionType>()
         from level in SkillLevelExtensions.All
         from index in Enumerable.Range(0, perTypeAndLevel)
         select Create(
             type,
             $"{type}-{level}-{index}",
             type switch
             {
                 QuestionType.MultipleChoice => "AC",
                 QuestionType.TrueFalse => "√",
                 QuestionType.ShortAnswer => "参考答案",
                 _ => "A"
             },
             level)).ToArray();
}
