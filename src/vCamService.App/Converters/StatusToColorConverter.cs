using System.Globalization;
using System.Windows.Data;

namespace vCamService.App.Converters;

[ValueConversion(typeof(StreamStatus), typeof(Brush))]
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            StreamStatus.Connected    => Brushes.Green,
            StreamStatus.Connecting   => Brushes.Orange,
            StreamStatus.Reconnecting => Brushes.Yellow,
            StreamStatus.Error        => Brushes.Red,
            _                         => Brushes.Gray
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
