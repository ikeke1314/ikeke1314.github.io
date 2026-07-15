using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Core.Tests;

public sealed class QuestionFilterTests
{
    [Fact]
    public void CategoryFilter_IncludesSelectedAndGeneralQuestions()
    {
        var general = TestQuestions.Create(QuestionType.SingleChoice, "通用");
        var baseStation = TestQuestions.Create(QuestionType.SingleChoice, "基站", category: QuestionCategory.BaseStation);
        var windowRobot = TestQuestions.Create(QuestionType.SingleChoice, "窗宝", category: QuestionCategory.WindowRobot);

        var result = QuestionFilter.Apply([general, baseStation, windowRobot], categories: [QuestionCategory.BaseStation]);

        Assert.Contains(general, result);
        Assert.Contains(baseStation, result);
        Assert.DoesNotContain(windowRobot, result);
    }

    [Fact]
    public void MultipleLevelFilter_MatchesAnySelectedLevel()
    {
        var level1 = TestQuestions.Create(QuestionType.SingleChoice, "一级", level: SkillLevel.Level1);
        var level3 = TestQuestions.Create(QuestionType.SingleChoice, "三级", level: SkillLevel.Level3);
        var level6 = TestQuestions.Create(QuestionType.SingleChoice, "六级", level: SkillLevel.Level6);

        var result = QuestionFilter.Apply([level1, level3, level6], [SkillLevel.Level1, SkillLevel.Level3]);
        Assert.Equal([level1, level3], result);
    }
}
