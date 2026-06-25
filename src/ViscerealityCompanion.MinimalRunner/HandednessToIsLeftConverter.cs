using System.Globalization;
using System.Windows.Data;

namespace ViscerealityCompanion.MinimalRunner;

public sealed class HandednessToIsLeftConverter : IValueConverter
{
    private const string LeftHanded = "left-handed";
    private const string RightHanded = "right-handed";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, LeftHanded, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool isLeft && isLeft ? LeftHanded : RightHanded;
}
