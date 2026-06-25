using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CodeIsland.Desktop;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value is AgentStatus s ? s : AgentStatus.Idle;
        return new SolidColorBrush(status switch
        {
            AgentStatus.Running => System.Windows.Media.Color.FromRgb(98, 211, 148),
            AgentStatus.Processing => System.Windows.Media.Color.FromRgb(159, 183, 255),
            AgentStatus.WaitingApproval => System.Windows.Media.Color.FromRgb(255, 184, 92),
            AgentStatus.WaitingQuestion => System.Windows.Media.Color.FromRgb(255, 208, 128),
            _ => System.Windows.Media.Color.FromRgb(141, 151, 168)
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

public sealed class StatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value is AgentStatus s ? s : AgentStatus.Idle;
        return new SolidColorBrush(status switch
        {
            AgentStatus.Running => System.Windows.Media.Color.FromRgb(20, 48, 34),
            AgentStatus.Processing => System.Windows.Media.Color.FromRgb(24, 32, 45),
            AgentStatus.WaitingApproval => System.Windows.Media.Color.FromRgb(54, 38, 23),
            AgentStatus.WaitingQuestion => System.Windows.Media.Color.FromRgb(46, 33, 48),
            _ => System.Windows.Media.Color.FromRgb(18, 23, 34)
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

public sealed class StatusAccentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value is AgentStatus s ? s : AgentStatus.Idle;
        return new SolidColorBrush(status switch
        {
            AgentStatus.Running => System.Windows.Media.Color.FromRgb(85, 214, 107),
            AgentStatus.Processing => System.Windows.Media.Color.FromRgb(111, 145, 255),
            AgentStatus.WaitingApproval => System.Windows.Media.Color.FromRgb(255, 184, 92),
            AgentStatus.WaitingQuestion => System.Windows.Media.Color.FromRgb(191, 166, 255),
            _ => System.Windows.Media.Color.FromRgb(67, 79, 99)
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
