using ClosedXML.Excel;
using SkillExam.Core.Models;
using SkillExam.Infrastructure.Excel;

namespace SkillExam.Infrastructure.Tests;

public sealed class ExcelQuestionBankReaderTests
{
    private readonly ExcelQuestionBankReader _reader = new();

    [Fact]
    public async Task RealWorkbook_ParsesRequiredBaselineWithoutHardCodingReaderOutput()
    {
        var result = await _reader.ReadAsync(TestPaths.RealQuestionBank);
        var statistics = QuestionBankStatistics.From(result.Questions);

        Assert.Single(result.LoadedSheets);
        Assert.Equal(1291, statistics.Total);
        Assert.Equal(661, statistics.ByType[QuestionType.SingleChoice]);
        Assert.Equal(258, statistics.ByType[QuestionType.MultipleChoice]);
        Assert.Equal(263, statistics.ByType[QuestionType.TrueFalse]);
        Assert.Equal(109, statistics.ByType[QuestionType.ShortAnswer]);
        Assert.Equal(606, statistics.ByLevel[SkillLevel.Level1]);
        Assert.Equal(735, statistics.ByLevel[SkillLevel.Level2]);
        Assert.Equal(664, statistics.ByLevel[SkillLevel.Level3]);
        Assert.Equal(910, statistics.ByLevel[SkillLevel.Level4]);
        Assert.Equal(694, statistics.ByLevel[SkillLevel.Level5]);
        Assert.Equal(466, statistics.ByLevel[SkillLevel.Level6]);
        Assert.Equal(1291, result.Questions.Select(question => question.Id).Count());
        Assert.All(result.Questions, question => Assert.Matches("^[a-f0-9]{64}$", question.Id));
    }

    [Fact]
    public async Task SheetList_DefaultsToExcludingPivotButSupportsExplicitMultiSelect()
    {
        var path = CreateWorkbook(workbook =>
        {
            AddValidSheet(workbook, "主库", "题目1");
            AddValidSheet(workbook, "补充库", "题目2");
            AddValidSheet(workbook, "数据透视", "题目3");
        });
        try
        {
            var sheets = await _reader.GetSheetsAsync(path);
            Assert.Equal(3, sheets.Count);
            Assert.False(sheets.Single(sheet => sheet.Name == "数据透视").IsSelectedByDefault);

            var defaults = await _reader.ReadAsync(path);
            Assert.Equal(2, defaults.Questions.Count);
            var selected = await _reader.ReadAsync(path, ["主库", "数据透视"]);
            Assert.Equal(2, selected.Questions.Count);
            Assert.Equal(["主库", "数据透视"], selected.LoadedSheets);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidSheetAndRow_ReturnStructuredDetails()
    {
        var path = CreateWorkbook(workbook =>
        {
            var missing = workbook.AddWorksheet("缺列表");
            missing.Cell(4, 1).Value = "题目";
            missing.Cell(4, 2).Value = "答案";
            var invalid = workbook.AddWorksheet("无效行");
            invalid.Cell(4, 1).Value = "考题类型";
            invalid.Cell(4, 2).Value = "题目";
            invalid.Cell(4, 3).Value = "答案";
            invalid.Cell(5, 1).Value = "单选题";
            invalid.Cell(5, 2).Value = "缺少答案";
        });
        try
        {
            var result = await _reader.ReadAsync(path, ["缺列表", "无效行", "不存在"]);
            Assert.Empty(result.Questions);
            Assert.Equal(1, result.InvalidRowCount);
            Assert.Contains(result.Issues, issue => issue.Sheet == "缺列表" && issue.Field == "考题类型" && issue.RowNumber == 4);
            Assert.Contains(result.Issues, issue => issue.Sheet == "无效行" && issue.RowNumber == 5 && issue.Field.Contains("答案"));
            Assert.Contains(result.Issues, issue => issue.Sheet == "不存在" && issue.Field == "Sheet");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateWorkbook(Action<XLWorkbook> configure)
    {
        var path = Path.Combine(Path.GetTempPath(), $"skill-exam-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        configure(workbook);
        workbook.SaveAs(path);
        return path;
    }

    private static void AddValidSheet(XLWorkbook workbook, string name, string questionText)
    {
        var sheet = workbook.AddWorksheet(name);
        var columns = new[] { "考题类型", "题目", "选项A", "选项B", "答案", "来源", "一级" };
        for (var index = 0; index < columns.Length; index++)
        {
            sheet.Cell(4, index + 1).Value = columns[index];
        }
        sheet.Cell(5, 1).Value = "单选题";
        sheet.Cell(5, 2).Value = questionText;
        sheet.Cell(5, 3).Value = "是";
        sheet.Cell(5, 4).Value = "否";
        sheet.Cell(5, 5).Value = "A";
        sheet.Cell(5, 6).Value = name;
        sheet.Cell(5, 7).Value = "√";
    }
}
