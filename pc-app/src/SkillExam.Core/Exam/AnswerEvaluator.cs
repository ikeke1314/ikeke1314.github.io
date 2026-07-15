using System.Text.RegularExpressions;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Models;

namespace SkillExam.Core.Exam;

public sealed partial class AnswerEvaluator : IAnswerEvaluator
{
    private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "√", "对", "正确", "TRUE", "T", "是", "1", "YES"
    };

    private static readonly HashSet<string> FalseValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "B", "×", "X", "错", "错误", "FALSE", "F", "否", "0", "NO"
    };

    public AnswerEvaluation Evaluate(Question question, string? userAnswer)
    {
        var normalizedUser = Normalize(question.Type, userAnswer);
        var normalizedCorrect = Normalize(question.Type, question.Answer);
        var status = string.IsNullOrWhiteSpace(userAnswer)
            ? AnswerStatus.Unanswered
            : string.Equals(normalizedUser, normalizedCorrect, StringComparison.OrdinalIgnoreCase)
                ? AnswerStatus.Correct
                : AnswerStatus.Incorrect;
        return new AnswerEvaluation(status, normalizedUser, normalizedCorrect);
    }

    public static string Normalize(QuestionType type, string? answer)
    {
        var value = QuestionIdentity.Normalize(answer);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return type switch
        {
            QuestionType.MultipleChoice => NormalizeChoiceLetters(value),
            QuestionType.SingleChoice => NormalizeSingleChoice(value),
            QuestionType.TrueFalse => NormalizeTrueFalse(value),
            QuestionType.ShortAnswer => value,
            _ => value
        };
    }

    private static string NormalizeSingleChoice(string value)
    {
        var letters = ChoiceLetterRegex().Matches(value.ToUpperInvariant()).Select(match => match.Value).ToArray();
        return letters.Length == 1 ? letters[0] : value.ToUpperInvariant();
    }

    private static string NormalizeChoiceLetters(string value)
    {
        var letters = ChoiceLetterRegex().Matches(value.ToUpperInvariant())
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return letters.Length > 0 ? string.Concat(letters) : value.ToUpperInvariant();
    }

    private static string NormalizeTrueFalse(string value)
    {
        if (TrueValues.Contains(value))
        {
            return "TRUE";
        }

        if (FalseValues.Contains(value))
        {
            return "FALSE";
        }

        return value.ToUpperInvariant();
    }

    [GeneratedRegex("[A-E]", RegexOptions.IgnoreCase)]
    private static partial Regex ChoiceLetterRegex();
}
