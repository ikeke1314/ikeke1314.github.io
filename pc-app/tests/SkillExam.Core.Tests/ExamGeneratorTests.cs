using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Tests;

public sealed class ExamGeneratorTests
{
    private readonly ExamGenerator _generator = new();

    [Fact]
    public void Blueprint_IsSixtyThreeQuestionsAndOneHundredPoints()
    {
        Assert.Equal(63, ExamBlueprint.TotalQuestions);
        Assert.Equal(100, ExamBlueprint.MaximumScore);
    }

    [Theory]
    [InlineData(SkillLevel.Level1, 0.8, 0.2, 0)]
    [InlineData(SkillLevel.Level2, 0.1, 0.7, 0.2)]
    [InlineData(SkillLevel.Level3, 0.1, 0.7, 0.2)]
    [InlineData(SkillLevel.Level4, 0.1, 0.7, 0.2)]
    [InlineData(SkillLevel.Level5, 0.1, 0.7, 0.2)]
    [InlineData(SkillLevel.Level6, 0.1, 0.9, 0)]
    public void AllSixLevels_HaveRequiredWeights(SkillLevel target, double first, double second, double third)
    {
        var weights = _generator.GetWeights(target).Values.ToArray();
        Assert.Equal(first, (double)weights[0], 3);
        Assert.Equal(second, (double)weights[1], 3);
        if (third > 0)
        {
            Assert.Equal(third, (double)weights[2], 3);
        }
        Assert.Equal(1m, weights.Sum());
    }

    [Theory]
    [InlineData(SkillLevel.Level1)]
    [InlineData(SkillLevel.Level2)]
    [InlineData(SkillLevel.Level3)]
    [InlineData(SkillLevel.Level4)]
    [InlineData(SkillLevel.Level5)]
    [InlineData(SkillLevel.Level6)]
    public void CompleteBank_GeneratesUniqueFullPaperForEveryLevel(SkillLevel level)
    {
        var result = _generator.Generate(TestQuestions.CompleteBank(), new ExamRequest(level, Seed: 42));

        Assert.True(result.Success);
        Assert.NotNull(result.Paper);
        Assert.Equal(63, result.Paper.Questions.Count);
        Assert.Equal(63, result.Paper.Questions.Select(question => question.Id).Distinct().Count());
        Assert.Equal(100, result.Paper.MaximumScore);
        foreach (var type in Enum.GetValues<QuestionType>())
        {
            Assert.Equal(ExamBlueprint.QuestionCounts[type], result.Paper.Questions.Count(question => question.Type == type));
        }
    }

    [Fact]
    public void SameSeed_ReproducesQuestionOrder()
    {
        var bank = TestQuestions.CompleteBank();
        var first = _generator.Generate(bank, new ExamRequest(SkillLevel.Level3, Seed: 2026)).Paper!;
        var second = _generator.Generate(bank, new ExamRequest(SkillLevel.Level3, Seed: 2026)).Paper!;
        Assert.Equal(first.Questions.Select(question => question.Id), second.Questions.Select(question => question.Id));
    }

    [Fact]
    public void InsufficientBank_ReturnsDetailedShortagesAndNoPartialPaper()
    {
        var result = _generator.Generate(
            [TestQuestions.Create(QuestionType.SingleChoice, "仅一题")],
            new ExamRequest(SkillLevel.Level1, Seed: 1));

        Assert.False(result.Success);
        Assert.Null(result.Paper);
        Assert.Contains(result.Shortages, shortage => shortage.QuestionType == QuestionType.SingleChoice && shortage.Missing > 0);
        Assert.Contains(result.Shortages, shortage => shortage.QuestionType == QuestionType.ShortAnswer && shortage.Missing > 0);
    }
}
