using System.Text;
using ExcelDataReader;
using SkillExam.Core.Abstractions;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.Infrastructure.Excel;

public sealed class ExcelQuestionBankReader : IQuestionBankReader
{
    private const int HeaderRowNumber = 4;
    private static readonly string[] RequiredColumns = ["考题类型", "题目", "答案"];
    private static readonly string[] OptionNames = ["A", "B", "C", "D", "E"];

    static ExcelQuestionBankReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<IReadOnlyList<SheetInfo>> GetSheetsAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => GetSheets(filePath, cancellationToken), cancellationToken);

    public Task<QuestionBankLoadResult> ReadAsync(
        string filePath,
        IReadOnlyCollection<string>? selectedSheets = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(filePath, selectedSheets, cancellationToken), cancellationToken);

    private static IReadOnlyList<SheetInfo> GetSheets(string filePath, CancellationToken cancellationToken)
    {
        ValidateFile(filePath);
        using var stream = OpenRead(filePath);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var sheets = new List<SheetInfo>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheets.Add(new SheetInfo(reader.Name, !reader.Name.Contains("透视", StringComparison.OrdinalIgnoreCase)));
        } while (reader.NextResult());
        return sheets;
    }

    private static QuestionBankLoadResult Read(
        string filePath,
        IReadOnlyCollection<string>? selectedSheets,
        CancellationToken cancellationToken)
    {
        ValidateFile(filePath);
        var selectedSet = selectedSheets is { Count: > 0 }
            ? selectedSheets.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        var questions = new List<Question>();
        var issues = new List<QuestionBankIssue>();
        var loadedSheets = new List<string>();
        var invalidRows = 0;

        using var stream = OpenRead(filePath);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shouldLoad = selectedSet?.Contains(reader.Name) ??
                             !reader.Name.Contains("透视", StringComparison.OrdinalIgnoreCase);
            if (!shouldLoad)
            {
                continue;
            }

            loadedSheets.Add(reader.Name);
            ParseSheet(reader, questions, issues, ref invalidRows, cancellationToken);
        } while (reader.NextResult());

        if (selectedSet is not null)
        {
            foreach (var missingSheet in selectedSet.Except(loadedSheets, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new QuestionBankIssue(missingSheet, null, "Sheet", "选择的 Sheet 不存在。"));
            }
        }

        if (loadedSheets.Count == 0)
        {
            issues.Add(new QuestionBankIssue(string.Empty, null, "Sheet", "没有可加载的 Sheet；名称包含“透视”的 Sheet 默认排除。"));
        }

        return new QuestionBankLoadResult(questions, issues, loadedSheets, invalidRows);
    }

    private static void ParseSheet(
        IExcelDataReader reader,
        ICollection<Question> questions,
        ICollection<QuestionBankIssue> issues,
        ref int invalidRows,
        CancellationToken cancellationToken)
    {
        var rowNumber = 0;
        Dictionary<string, int>? columns = null;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            if (rowNumber < HeaderRowNumber)
            {
                continue;
            }

            if (rowNumber == HeaderRowNumber)
            {
                columns = ReadColumns(reader);
                var missingColumns = RequiredColumns.Where(column => !columns.ContainsKey(column)).ToArray();
                foreach (var missing in missingColumns)
                {
                    issues.Add(new QuestionBankIssue(reader.Name, HeaderRowNumber, missing, $"缺少必填列“{missing}”。"));
                }
                if (missingColumns.Length > 0)
                {
                    return;
                }
                continue;
            }

            if (columns is null || IsEmptyRow(reader))
            {
                continue;
            }

            var typeText = GetText(reader, columns, "考题类型");
            var text = GetText(reader, columns, "题目");
            var answer = GetText(reader, columns, "答案");
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(typeText)) missingFields.Add("考题类型");
            if (string.IsNullOrWhiteSpace(text)) missingFields.Add("题目");
            if (string.IsNullOrWhiteSpace(answer)) missingFields.Add("答案");
            if (missingFields.Count > 0)
            {
                invalidRows++;
                issues.Add(new QuestionBankIssue(reader.Name, rowNumber, string.Join("、", missingFields), "必填字段为空，该行未导入。"));
                continue;
            }

            if (!QuestionTypeExtensions.TryParseDisplayName(typeText, out var type))
            {
                invalidRows++;
                issues.Add(new QuestionBankIssue(reader.Name, rowNumber, "考题类型", $"不支持的题型“{typeText}”。"));
                continue;
            }

            var options = OptionNames
                .Select(name => (name, value: GetText(reader, columns, $"选项{name}")))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.value))
                .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.OrdinalIgnoreCase);
            var levels = SkillLevelExtensions.All
                .Where(level => HasValue(reader, columns, level.ToDisplayName()))
                .ToHashSet();
            var source = GetText(reader, columns, "来源");
            source = string.IsNullOrWhiteSpace(source) ? reader.Name : source;
            var categories = DetectCategories(source, text);
            questions.Add(new Question
            {
                Id = QuestionIdentity.Create(type, text, options, answer, source),
                Type = type,
                Text = text,
                Options = options,
                Answer = answer,
                Levels = levels,
                Source = source,
                SourceSheet = reader.Name,
                Categories = categories
            });
        }

        if (rowNumber < HeaderRowNumber)
        {
            issues.Add(new QuestionBankIssue(reader.Name, null, "表头", "Sheet 不足 4 行，无法读取第 4 行表头。"));
        }
    }

    private static Dictionary<string, int> ReadColumns(IExcelDataReader reader)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var name = GetCellText(reader.GetValue(index));
            if (!string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name))
            {
                result[name] = index;
            }
        }
        return result;
    }

    private static bool IsEmptyRow(IExcelDataReader reader)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (!string.IsNullOrWhiteSpace(GetCellText(reader.GetValue(index))))
            {
                return false;
            }
        }
        return true;
    }

    private static string GetText(IExcelDataReader reader, IReadOnlyDictionary<string, int> columns, string column) =>
        columns.TryGetValue(column, out var index) && index < reader.FieldCount
            ? GetCellText(reader.GetValue(index))
            : string.Empty;

    private static bool HasValue(IExcelDataReader reader, IReadOnlyDictionary<string, int> columns, string column) =>
        !string.IsNullOrWhiteSpace(GetText(reader, columns, column));

    private static string GetCellText(object? value) => value switch
    {
        null => string.Empty,
        double number when number == Math.Truncate(number) => number.ToString("0", System.Globalization.CultureInfo.InvariantCulture).Trim(),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static IReadOnlySet<QuestionCategory> DetectCategories(string source, string questionText)
    {
        var combined = $"{source} {questionText}";
        var categories = new HashSet<QuestionCategory>();
        if (combined.Contains("基站", StringComparison.OrdinalIgnoreCase)) categories.Add(QuestionCategory.BaseStation);
        if (combined.Contains("地宝", StringComparison.OrdinalIgnoreCase)) categories.Add(QuestionCategory.GroundRobot);
        if (combined.Contains("窗宝", StringComparison.OrdinalIgnoreCase)) categories.Add(QuestionCategory.WindowRobot);
        if (combined.Contains("光学组件", StringComparison.OrdinalIgnoreCase)) categories.Add(QuestionCategory.OpticalComponent);
        return categories;
    }

    private static FileStream OpenRead(string filePath) =>
        new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static void ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("题库文件不存在。", filePath);
        }

        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("仅支持 .xlsx 和 .xls 题库文件。");
        }
    }
}
