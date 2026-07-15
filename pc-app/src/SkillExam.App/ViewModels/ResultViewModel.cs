using CommunityToolkit.Mvvm.Input;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.App.ViewModels;

public sealed partial class ResultViewModel(
    ExamResult result,
    IReadOnlyList<Question> incorrectQuestions,
    Action backToHome,
    Func<IReadOnlyList<Question>, Task> reviewErrors)
{
    public ExamResult Result { get; } = result;
    public string ScoreText => $"{Result.Score} 分";
    public string PassText => Result.Passed ? "考试通过" : "未达到 80 分及格线";
    public string ElapsedText => $"{(int)Result.Elapsed.TotalMinutes:00}:{Result.Elapsed.Seconds:00}";
    public bool HasIncorrectQuestions => incorrectQuestions.Count > 0;

    [RelayCommand]
    private void BackToHome() => backToHome();

    [RelayCommand]
    private Task ReviewErrorsAsync() => reviewErrors(incorrectQuestions);
}
