using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Practice;

public sealed class PracticeSession
{
    private readonly IAnswerEvaluator _answerEvaluator;
    private readonly Dictionary<string, string> _answers = [];
    private readonly HashSet<string> _pendingRetryQuestionIds = [];

    public PracticeSession(
        IReadOnlyList<Question> questions,
        IAnswerEvaluator answerEvaluator,
        PracticeSessionSnapshot? snapshot = null)
    {
        if (questions.Count == 0)
        {
            throw new ArgumentException("练习题目不能为空。", nameof(questions));
        }

        Questions = questions;
        _answerEvaluator = answerEvaluator;
        CurrentIndex = snapshot?.CurrentQuestionId is null
            ? 0
            : Math.Max(0, questions.ToList().FindIndex(question => question.Id == snapshot.CurrentQuestionId));
        if (snapshot is not null)
        {
            foreach (var answer in snapshot.Answers)
            {
                _answers[answer.Key] = answer.Value;
            }
            _pendingRetryQuestionIds.UnionWith(snapshot.PendingRetryQuestionIds);
        }
    }

    public IReadOnlyList<Question> Questions { get; }
    public int CurrentIndex { get; private set; }
    public Question CurrentQuestion => Questions[CurrentIndex];
    public IReadOnlySet<string> PendingRetryQuestionIds => _pendingRetryQuestionIds;
    public IReadOnlyDictionary<string, string> Answers => _answers;

    public AnswerEvaluation Submit(string? answer)
    {
        var evaluation = _answerEvaluator.Evaluate(CurrentQuestion, answer);
        _answers[CurrentQuestion.Id] = answer ?? string.Empty;
        if (evaluation.Status == AnswerStatus.Incorrect)
        {
            _pendingRetryQuestionIds.Add(CurrentQuestion.Id);
        }
        else if (evaluation.Status == AnswerStatus.Correct)
        {
            _pendingRetryQuestionIds.Remove(CurrentQuestion.Id);
        }
        return evaluation;
    }

    public bool MoveNext()
    {
        if (CurrentIndex >= Questions.Count - 1)
        {
            return false;
        }
        CurrentIndex++;
        return true;
    }

    public bool MovePrevious()
    {
        if (CurrentIndex <= 0)
        {
            return false;
        }
        CurrentIndex--;
        return true;
    }

    public bool MoveTo(int index)
    {
        if (index < 0 || index >= Questions.Count)
        {
            return false;
        }
        CurrentIndex = index;
        return true;
    }

    public PracticeSessionSnapshot CreateSnapshot(
        string sessionId,
        IReadOnlyList<SkillLevel> levels,
        IReadOnlyList<QuestionCategory> categories,
        DateTimeOffset updatedAt) => new()
    {
        SessionId = sessionId,
        Levels = levels,
        Categories = categories,
        QuestionType = Questions[0].Type,
        QuestionIds = Questions.Select(question => question.Id).ToArray(),
        CurrentQuestionId = CurrentQuestion.Id,
        Answers = new Dictionary<string, string>(_answers),
        PendingRetryQuestionIds = new HashSet<string>(_pendingRetryQuestionIds),
        UpdatedAt = updatedAt
    };
}
