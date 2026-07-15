using SkillExam.Core.Abstractions;

namespace SkillExam.Core.Exam;

public sealed class ExamScorer(IAnswerEvaluator answerEvaluator)
{
    public ExamResult Score(ExamPaper paper, IReadOnlyDictionary<string, string> answers, TimeSpan elapsed)
    {
        var results = paper.Questions.Select(question =>
        {
            answers.TryGetValue(question.Id, out var userAnswer);
            var evaluation = answerEvaluator.Evaluate(question, userAnswer);
            var maximum = ExamBlueprint.Points[question.Type];
            return new ExamQuestionResult(
                question,
                userAnswer ?? string.Empty,
                evaluation.Status,
                evaluation.IsCorrect ? maximum : 0,
                maximum);
        }).ToArray();
        var score = results.Sum(result => result.AwardedPoints);
        return new ExamResult(
            score,
            paper.MaximumScore,
            score >= ExamBlueprint.PassingScore,
            results.Count(result => result.Status == AnswerStatus.Correct),
            results.Count(result => result.Status == AnswerStatus.Incorrect),
            results.Count(result => result.Status == AnswerStatus.Unanswered),
            elapsed,
            results);
    }
}
