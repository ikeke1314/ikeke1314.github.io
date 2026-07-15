using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SkillExam.App.ViewModels;
using SkillExam.Core.Exam;
using SkillExam.Core.Models;

namespace SkillExam.App.Converters;

public sealed class NavigationStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            NavigationState.Current => "PrimaryBrush",
            NavigationState.Correct => "SuccessBrush",
            NavigationState.Incorrect => "ErrorBrush",
            _ => "BorderBrush"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class NavigationStateToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NavigationState.Unanswered
            ? Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black
            : Brushes.White;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class AnswerStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            AnswerStatus.Correct => "SuccessBrush",
            AnswerStatus.Incorrect => "ErrorBrush",
            _ => "WarningBrush"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool flag && !flag;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool flag && !flag;
}

public sealed class SkillLevelToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SkillLevel level ? level.ToDisplayName() : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
