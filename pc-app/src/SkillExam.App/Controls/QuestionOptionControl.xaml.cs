using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkillExam.App.ViewModels;

namespace SkillExam.App.Controls;

public partial class QuestionOptionControl : UserControl
{
    public static readonly DependencyProperty OptionProperty = DependencyProperty.Register(
        nameof(Option), typeof(QuestionOptionViewModel), typeof(QuestionOptionControl));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(QuestionOptionControl));

    public static readonly DependencyProperty IsMultipleChoiceProperty = DependencyProperty.Register(
        nameof(IsMultipleChoice), typeof(bool), typeof(QuestionOptionControl));

    public QuestionOptionControl() => InitializeComponent();

    public QuestionOptionViewModel? Option
    {
        get => (QuestionOptionViewModel?)GetValue(OptionProperty);
        set => SetValue(OptionProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool IsMultipleChoice
    {
        get => (bool)GetValue(IsMultipleChoiceProperty);
        set => SetValue(IsMultipleChoiceProperty, value);
    }
}
