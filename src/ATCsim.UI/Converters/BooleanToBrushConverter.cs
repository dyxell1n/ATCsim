using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ATCsim.UI.Converters;

public class BooleanToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(201, 42, 42)); // Red alert #C92A2A
    public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(33, 37, 41)); // Dark #212529

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return TrueBrush;
        return FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
