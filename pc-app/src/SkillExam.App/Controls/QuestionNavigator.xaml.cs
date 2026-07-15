using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SkillExam.App.Controls;

public partial class QuestionNavigator : UserControl
{
    public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register(
        nameof(Groups), typeof(IEnumerable), typeof(QuestionNavigator));
    public static readonly DependencyProperty NavigateCommandProperty = DependencyProperty.Register(
        nameof(NavigateCommand), typeof(ICommand), typeof(QuestionNavigator));

    public QuestionNavigator() => InitializeComponent();

    public IEnumerable? Groups
    {
        get => (IEnumerable?)GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }
    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }
}
