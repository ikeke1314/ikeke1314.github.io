namespace SkillExam.Core.Models;

public sealed record Question
{
    public required string Id { get; init; }
    public required QuestionType Type { get; init; }
    public required string Text { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
    public required string Answer { get; init; }
    public IReadOnlySet<SkillLevel> Levels { get; init; } = new HashSet<SkillLevel>();
    public required string Source { get; init; }
    public string SourceSheet { get; init; } = string.Empty;
    public IReadOnlySet<QuestionCategory> Categories { get; init; } = new HashSet<QuestionCategory>();
}

public sealed record QuestionBankIssue(
    string Sheet,
    int? RowNumber,
    string Field,
    string Message,
    QuestionBankIssueSeverity Severity = QuestionBankIssueSeverity.Error);

public enum QuestionBankIssueSeverity
{
    Warning,
    Error
}

public sealed record SheetInfo(string Name, bool IsSelectedByDefault);

public sealed record QuestionBankLoadResult(
    IReadOnlyList<Question> Questions,
    IReadOnlyList<QuestionBankIssue> Issues,
    IReadOnlyList<string> LoadedSheets,
    int InvalidRowCount)
{
    public bool HasUsableQuestions => Questions.Count > 0;
}

public sealed record QuestionBankStatistics(
    int Total,
    IReadOnlyDictionary<QuestionType, int> ByType,
    IReadOnlyDictionary<SkillLevel, int> ByLevel)
{
    public static QuestionBankStatistics From(IEnumerable<Question> questions)
    {
        var materialized = questions.ToArray();
        return new QuestionBankStatistics(
            materialized.Length,
            Enum.GetValues<QuestionType>().ToDictionary(type => type, type => materialized.Count(q => q.Type == type)),
            SkillLevelExtensions.All.ToDictionary(level => level, level => materialized.Count(q => q.Levels.Contains(level))));
    }
}
