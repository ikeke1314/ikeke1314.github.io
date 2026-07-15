using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Tests;

public sealed class ExamScorerTests
{
    [Fact]
    public void Score_UsesEightyPointPassBoundaryAndCountsUnanswered()
    {
        var questions = Enumerable.Range(0, 100)
            .Select(index => TestQuestions.Create(QuestionType.SingleChoice, $"题目{index}"))
            .ToArray();
        var paper = new ExamPaper(
            SkillLevel.Level1,
            questions,
            new Dictionary<QuestionType, IReadOnlyDictionary<SkillLevel, int>>(),
            100,
            TimeSpan.FromMinutes(45));
        var answers = questions.Take(80).ToDictionary(question => question.Id, _ => "A");

        var result = new ExamScorer(new AnswerEvaluator()).Score(paper, answers, TimeSpan.FromMinutes(12));

        Assert.Equal(80, result.Score);
        Assert.True(result.Passed);
        Assert.Equal(80, result.CorrectCount);
        Assert.Equal(20, result.UnansweredCount);
    }
}
