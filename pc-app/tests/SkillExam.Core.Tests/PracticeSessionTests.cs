using SkillExam.Core.Exam;
using SkillExam.Core.Models;
using SkillExam.Core.Practice;

namespace SkillExam.Core.Tests;

public sealed class PracticeSessionTests
{
    [Fact]
    public void WrongAnswer_RemainsPendingUntilCorrectRetry()
    {
        var question = TestQuestions.Create(QuestionType.SingleChoice, "重试题");
        var session = new PracticeSession([question], new AnswerEvaluator());

        Assert.Equal(AnswerStatus.Incorrect, session.Submit("B").Status);
        Assert.Contains(question.Id, session.PendingRetryQuestionIds);
        Assert.Equal(AnswerStatus.Correct, session.Submit("A").Status);
        Assert.DoesNotContain(question.Id, session.PendingRetryQuestionIds);
    }

    [Fact]
    public void Snapshot_UsesStableQuestionIdsAndMultipleLevels()
    {
        var questions = new[]
        {
            TestQuestions.Create(QuestionType.SingleChoice, "第一题", level: SkillLevel.Level1),
            TestQuestions.Create(QuestionType.SingleChoice, "第二题", level: SkillLevel.Level2)
        };
        var session = new PracticeSession(questions, new AnswerEvaluator());
        session.MoveNext();

        var snapshot = session.CreateSnapshot(
            "session",
            [SkillLevel.Level1, SkillLevel.Level2],
            [QuestionCategory.BaseStation],
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"));

        Assert.Equal(questions.Select(question => question.Id), snapshot.QuestionIds);
        Assert.Equal(questions[1].Id, snapshot.CurrentQuestionId);
        Assert.Equal([SkillLevel.Level1, SkillLevel.Level2], snapshot.Levels);
    }

    [Fact]
    public void CompletedSnapshot_StartsFreshPracticeRound()
    {
        var questions = new[]
        {
            TestQuestions.Create(QuestionType.SingleChoice, "第一题"),
            TestQuestions.Create(QuestionType.SingleChoice, "第二题")
        };
        var snapshot = new PracticeSessionSnapshot
        {
            SessionId = "completed-session",
            Levels = [SkillLevel.Level1],
            Categories = [],
            QuestionType = QuestionType.SingleChoice,
            QuestionIds = questions.Select(question => question.Id).ToArray(),
            CurrentQuestionId = questions[1].Id,
            Answers = questions.ToDictionary(question => question.Id, _ => "A"),
            PendingRetryQuestionIds = new HashSet<string>(),
            UpdatedAt = DateTimeOffset.Parse("2026-07-17T00:00:00Z")
        };

        var session = new PracticeSession(questions, new AnswerEvaluator(), snapshot);

        Assert.Equal(0, session.CurrentIndex);
        Assert.Empty(session.Answers);
        Assert.Empty(session.PendingRetryQuestionIds);
    }

    [Fact]
    public void IncompleteSnapshot_ResumesPreviousProgress()
    {
        var questions = new[]
        {
            TestQuestions.Create(QuestionType.SingleChoice, "第一题"),
            TestQuestions.Create(QuestionType.SingleChoice, "第二题")
        };
        var snapshot = new PracticeSessionSnapshot
        {
            SessionId = "incomplete-session",
            Levels = [SkillLevel.Level1],
            Categories = [],
            QuestionType = QuestionType.SingleChoice,
            QuestionIds = questions.Select(question => question.Id).ToArray(),
            CurrentQuestionId = questions[1].Id,
            Answers = new Dictionary<string, string> { [questions[0].Id] = "A" },
            PendingRetryQuestionIds = new HashSet<string>(),
            UpdatedAt = DateTimeOffset.Parse("2026-07-17T00:00:00Z")
        };

        var session = new PracticeSession(questions, new AnswerEvaluator(), snapshot);

        Assert.Equal(1, session.CurrentIndex);
        Assert.Equal("A", session.Answers[questions[0].Id]);
    }

    [Fact]
    public void Restart_ClearsAnswersPendingRetriesAndPosition()
    {
        var questions = new[]
        {
            TestQuestions.Create(QuestionType.SingleChoice, "第一题"),
            TestQuestions.Create(QuestionType.SingleChoice, "第二题")
        };
        var session = new PracticeSession(questions, new AnswerEvaluator());
        session.Submit("B");
        session.MoveNext();
        session.Submit("A");

        session.Restart();

        Assert.Equal(0, session.CurrentIndex);
        Assert.Empty(session.Answers);
        Assert.Empty(session.PendingRetryQuestionIds);
    }
}
