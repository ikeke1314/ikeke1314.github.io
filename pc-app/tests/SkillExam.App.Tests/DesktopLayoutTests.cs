using System.IO;

namespace SkillExam.App.Tests;

public sealed class DesktopLayoutTests
{
    [Fact]
    public void MainWindow_ProvidesCompleteDesktopTitleBarControls()
    {
        var xaml = ReadSource("src", "SkillExam.App", "MainWindow.xaml");

        Assert.Contains("<ui:TitleBar", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowMinimize=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowMaximize=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowClose=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_EmbedsWindowAndExecutableIcon()
    {
        var project = ReadSource("src", "SkillExam.App", "SkillExam.App.csproj");
        var window = ReadSource("src", "SkillExam.App", "MainWindow.xaml");
        var icon = File.ReadAllBytes(Path.Combine(FindProjectRoot(), "src", "SkillExam.App", "Assets", "AppIcon.ico"));

        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\AppIcon.ico\" />", project, StringComparison.Ordinal);
        Assert.Contains("Icon=\"/Assets/AppIcon.ico\"", window, StringComparison.Ordinal);
        Assert.True(icon.Length > 6);
        Assert.Equal(0, BitConverter.ToUInt16(icon, 0));
        Assert.Equal(1, BitConverter.ToUInt16(icon, 2));
        Assert.True(BitConverter.ToUInt16(icon, 4) >= 7);
    }

    [Fact]
    public void HomeView_UsesLegacyDesktopOrderWithoutDashboardLayout()
    {
        var xaml = ReadSource("src", "SkillExam.App", "Views", "HomeView.xaml");

        Assert.Contains("技能士理论考核模拟系统 V3.1", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UniformGrid", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CardBorderStyle", xaml, StringComparison.Ordinal);
        AssertInOrder(xaml,
            "TabIndex=\"0\"",
            "TabIndex=\"1\"",
            "TabIndex=\"2\"",
            "TabIndex=\"3\"",
            "TabIndex=\"4\"",
            "TabIndex=\"5\"",
            "TabIndex=\"6\"");
        AssertInOrder(xaml, "开始模拟考试", "专项练习模式", "我的错题本", "设置");
    }

    [Fact]
    public void PracticeView_UsesTwoListsAndVerticalQuestionTypeButtons()
    {
        var xaml = ReadSource("src", "SkillExam.App", "Views", "PracticeSetupView.xaml");

        Assert.DoesNotContain("UniformGrid", xaml, StringComparison.Ordinal);
        Assert.Equal(2, Count(xaml, "DesktopListBorderStyle"));
        Assert.Contains("刷新题目数量", xaml, StringComparison.Ordinal);
        AssertInOrder(xaml, "单选题练习", "多选题练习", "判断题练习", "简答题练习", "返回主页");
    }

    [Fact]
    public void QuestionView_PreservesLeftNavigatorAndBottomButtonOrder()
    {
        var xaml = ReadSource("src", "SkillExam.App", "Views", "QuestionView.xaml");
        var optionXaml = ReadSource("src", "SkillExam.App", "Controls", "QuestionOptionControl.xaml");
        var navigatorXaml = ReadSource("src", "SkillExam.App", "Controls", "QuestionNavigator.xaml");

        Assert.Contains("Width=\"300\"", xaml, StringComparison.Ordinal);
        Assert.Contains("QuestionNavigator", xaml, StringComparison.Ordinal);
        AssertInOrder(xaml, "上一题", "确认答案", "查看答案", "下一题", "ExitButtonText", "交卷");
        Assert.Contains("<RadioButton", optionXaml, StringComparison.Ordinal);
        Assert.Contains("<CheckBox", optionXaml, StringComparison.Ordinal);
        Assert.Contains(
            "<RadioButton IsChecked=\"{Binding Option.IsSelected, ElementName=Root, Mode=OneWay}\"",
            optionXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<CheckBox IsChecked=\"{Binding Option.IsSelected, ElementName=Root, Mode=TwoWay}\"",
            optionXaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CardBorderStyle", optionXaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Groups, ElementName=Root}\"", navigatorXaml, StringComparison.Ordinal);
        Assert.Contains("<WrapPanel Orientation=\"Horizontal\"", navigatorXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CollectionViewSource", navigatorXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsView_ProvidesAutoNextDelaySlider()
    {
        var xaml = ReadSource("src", "SkillExam.App", "Views", "SettingsView.xaml");

        Assert.Contains("Text=\"跳题延迟\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Minimum=\"0\" Maximum=\"3\" TickFrequency=\"0.1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding AutoNextDelaySeconds}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AutoNextDelayDisplay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorBookView_UsesOneWayBindingsForReadOnlyStatistics()
    {
        var xaml = ReadSource("src", "SkillExam.App", "Views", "ErrorBookView.xaml");

        Assert.Contains("{Binding TotalQuestions, Mode=OneWay}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding TotalAttempts, Mode=OneWay}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void NewApplicationSources_OnlyExposeVersionThree()
    {
        var root = FindProjectRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "SkillExam.App"), "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml")
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("V2.1", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("V2.3", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadSource(params string[] parts) => File.ReadAllText(Path.Combine([FindProjectRoot(), .. parts]));

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SkillExam.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("无法定位 SkillExam.sln。");
    }

    private static int Count(string value, string needle) =>
        value.Split(needle, StringSplitOptions.None).Length - 1;

    private static void AssertInOrder(string value, params string[] needles)
    {
        var current = -1;
        foreach (var needle in needles)
        {
            var index = value.IndexOf(needle, current + 1, StringComparison.Ordinal);
            Assert.True(index > current, $"未按预期顺序找到：{needle}");
            current = index;
        }
    }
}
