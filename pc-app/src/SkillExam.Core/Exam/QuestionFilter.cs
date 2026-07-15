using SkillExam.Core.Models;

namespace SkillExam.Core.Exam;

public static class QuestionFilter
{
    public static IReadOnlyList<Question> Apply(
        IEnumerable<Question> questions,
        IReadOnlyCollection<SkillLevel>? levels = null,
        IReadOnlyCollection<QuestionCategory>? categories = null,
        IReadOnlyCollection<string>? sources = null,
        QuestionType? type = null)
    {
        var levelSet = levels is { Count: > 0 } ? levels.ToHashSet() : null;
        var categorySet = categories is { Count: > 0 } ? categories.ToHashSet() : null;
        var sourceSet = sources is { Count: > 0 } ? sources.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

        return questions.Where(question =>
                (type is null || question.Type == type) &&
                (levelSet is null || question.Levels.Overlaps(levelSet)) &&
                // 通用题不绑定具体产品类别，选择任一类别时仍应保留。
                (categorySet is null || question.Categories.Count == 0 || question.Categories.Overlaps(categorySet)) &&
                (sourceSet is null || sourceSet.Contains(question.Source)))
            .ToArray();
    }
}
