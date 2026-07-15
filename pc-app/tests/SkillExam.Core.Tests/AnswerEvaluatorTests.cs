using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Tests;

public sealed class AnswerEvaluatorTests
{
    private readonly AnswerEvaluator _evaluator = new();

    [Fact]
    public void SingleChoice_IgnoresWhitespaceAndCase()
    {
        var question = TestQuestions.Create(QuestionType.SingleChoice, "单选", "A");
        Assert.Equal(AnswerStatus.Correct, _evaluator.Evaluate(question, " a ").Status);
    }

    [Theory]
    [InlineData("CA")]
    [InlineData("A,C")]
    [InlineData("c a")]
    public void MultipleChoice_IgnoresLetterOrderAndSeparators(string answer)
    {
        var question = TestQuestions.Create(QuestionType.MultipleChoice, "多选", "AC");
        Assert.Equal(AnswerStatus.Correct, _evaluator.Evaluate(question, answer).Status);
    }

    [Theory]
    [InlineData("A", "正确")]
    [InlineData("√", "True")]
    [InlineData("错误", "B")]
    [InlineData("False", "×")]
    public void TrueFalse_NormalizesLegacyFormats(string correct, string actual)
    {
        var question = TestQuestions.Create(QuestionType.TrueFalse, "判断", correct);
        Assert.Equal(AnswerStatus.Correct, _evaluator.Evaluate(question, actual).Status);
    }

    [Fact]
    public void ShortAnswer_CollapsesWhitespaceButDoesNotAcceptDifferentText()
    {
        var question = TestQuestions.Create(QuestionType.ShortAnswer, "简答", "标准  答案");
        Assert.Equal(AnswerStatus.Correct, _evaluator.Evaluate(question, " 标准 答案 ").Status);
        Assert.Equal(AnswerStatus.Incorrect, _evaluator.Evaluate(question, "近似答案").Status);
    }

    [Fact]
    public void EmptyAnswer_IsUnanswered()
    {
        var question = TestQuestions.Create(QuestionType.SingleChoice, "未答", "A");
        Assert.Equal(AnswerStatus.Unanswered, _evaluator.Evaluate(question, " ").Status);
    }
}
