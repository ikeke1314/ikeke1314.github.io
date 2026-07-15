using SkillExam.Infrastructure.Excel;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Tests;

public sealed class RealQuestionBankTests
{
    [Fact]
    public async Task RepositoryQuestionBank_LoadsAllCoreDimensions()
    {
        var reader = new ExcelQuestionBankReader();

        var sheets = await reader.GetSheetsAsync(TestPaths.RealQuestionBank);
        var result = await reader.ReadAsync(TestPaths.RealQuestionBank);

        Assert.NotEmpty(sheets);
        Assert.True(result.Questions.Count >= 1000, $"实际只加载到 {result.Questions.Count} 题。");
        Assert.All(Enum.GetValues<QuestionType>(), type =>
            Assert.Contains(result.Questions, question => question.Type == type));
        Assert.All(SkillLevelExtensions.All, level =>
            Assert.Contains(result.Questions, question => question.Levels.Contains(level)));
        Assert.DoesNotContain(result.LoadedSheets, sheet => sheet.Contains("透视", StringComparison.OrdinalIgnoreCase));
    }
}
