using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using JsonViewer.Models;

namespace JsonViewer.Converters;

/// <summary>
/// 文件大小转换器
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long size)
        {
            return FormatFileSize(size);
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// JSON值转换器
/// </summary>
public class JsonValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return "null";
        
        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}



/// <summary>
/// JSON值类型到图标的转换器
/// </summary>
public class JsonValueTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JsonValueType valueType)
        {
            return valueType switch
            {
                JsonValueType.Object => "📁", // 文件夹图标
                JsonValueType.Array => "📋", // 列表图标
                JsonValueType.String => "📝", // 文本图标
                JsonValueType.Number => "🔢", // 数字图标
                JsonValueType.Boolean => "☑️", // 复选框图标
                JsonValueType.Null => "❌", // 空值图标
                _ => "❓" // 未知类型图标
            };
        }
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// JSON值类型到颜色的转换器
/// </summary>
public class JsonValueTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JsonValueType valueType)
        {
            var color = valueType switch
            {
                JsonValueType.Object => Color.FromRgb(86, 156, 214), // 蓝色
                JsonValueType.Array => Color.FromRgb(78, 201, 176), // 青色
                JsonValueType.String => Color.FromRgb(206, 145, 120), // 橙色
                JsonValueType.Number => Color.FromRgb(181, 206, 168), // 绿色
                JsonValueType.Boolean => Color.FromRgb(86, 156, 214), // 蓝色
                JsonValueType.Null => Color.FromRgb(128, 128, 128), // 灰色
                _ => Color.FromRgb(220, 220, 220) // 默认白色
            };
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔值到可见性的转换器
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // 支持反转参数
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool result = invert ? !boolValue : boolValue;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool result = visibility == Visibility.Visible;
            return invert ? !result : result;
        }
        return false;
    }
}

/// <summary>
/// 空值到可见性的转换器
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter?.ToString()?.ToLower() == "invert";
        bool isNull = value == null;
        bool result = invert ? !isNull : isNull;
        return result ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 字节数到可读格式的转换器
/// </summary>
public class BytesToReadableConverter : IValueConverter
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            return FormatBytes(bytes);
        }
        if (value is int intBytes)
        {
            return FormatBytes(intBytes);
        }
        return "0 B";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < SizeSuffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {SizeSuffixes[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数字到百分比的转换器
/// </summary>
public class NumberToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return $"{doubleValue:P1}";
        }
        if (value is float floatValue)
        {
            return $"{floatValue:P1}";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && stringValue.EndsWith("%"))
        {
            var percentString = stringValue.TrimEnd('%');
            if (double.TryParse(percentString, out var percent))
            {
                return percent / 100.0;
            }
        }
        return 0.0;
    }
}

/// <summary>
/// 集合计数到字符串的转换器
/// </summary>
public class CountToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            string suffix = parameter?.ToString() ?? "项";
            return $"{count} {suffix}";
        }
        return "0 项";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 时间跨度到可读格式的转换器
/// </summary>
public class TimeSpanToReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}天 {timeSpan.Hours}小时";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}分钟 {timeSpan.Seconds}秒";
            if (timeSpan.TotalSeconds >= 1)
                return $"{timeSpan.Seconds}.{timeSpan.Milliseconds:000}秒";
            return $"{timeSpan.Milliseconds}毫秒";
        }
        return "0毫秒";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 搜索匹配类型到描述的转换器
/// </summary>
public class SearchMatchTypeToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SearchMatchType matchType)
        {
            return matchType switch
            {
                SearchMatchType.Key => "键名匹配",
                SearchMatchType.Value => "值匹配",
                SearchMatchType.Path => "路径匹配",
                _ => "未知匹配"
            };
        }
        return "未知匹配";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 多值转换器基类
/// </summary>
public abstract class MultiValueConverterBase : IMultiValueConverter
{
    public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);

    public virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 多个布尔值的AND操作转换器
/// </summary>
public class MultiBooleanAndConverter : MultiValueConverterBase
{
    public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
            return false;
            
        return values.All(v => v is bool b && b);
    }
}

/// <summary>
/// 多个布尔值的OR操作转换器
/// </summary>
public class MultiBooleanOrConverter : MultiValueConverterBase
{
    public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
            return false;
            
        return values.Any(v => v is bool b && b);
    }
}

/// <summary>
/// 字符串格式化转换器
/// </summary>
public class StringFormatConverter : MultiValueConverterBase
{
    public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string format && values != null && values.Length > 0)
        {
            try
            {
                return string.Format(culture, format, values);
            }
            catch (FormatException)
            {
                return string.Join(" ", values.Where(v => v != null));
            }
        }
        return string.Empty;
    }
}

/// <summary>
/// 数学运算转换器
/// </summary>
public class MathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue && parameter is string operation)
        {
            var parts = operation.Split(' ');
            if (parts.Length == 2)
            {
                var op = parts[0];
                if (double.TryParse(parts[1], out var operand))
                {
                    return op switch
                    {
                        "+" => doubleValue + operand,
                        "-" => doubleValue - operand,
                        "*" => doubleValue * operand,
                        "/" => operand != 0 ? doubleValue / operand : doubleValue,
                        "^" => Math.Pow(doubleValue, operand),
                        "max" => Math.Max(doubleValue, operand),
                        "min" => Math.Min(doubleValue, operand),
                        _ => doubleValue
                    };
                }
            }
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 颜色亮度调整转换器
/// </summary>
public class ColorBrightnessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color color && parameter is string factorString)
        {
            if (double.TryParse(factorString, out var factor))
            {
                var adjustedColor = AdjustBrightness(color, factor);
                return targetType == typeof(Color) ? adjustedColor : new SolidColorBrush(adjustedColor);
            }
        }
        return value;
    }

    private static Color AdjustBrightness(Color color, double factor)
    {
        var r = (byte)Math.Max(0, Math.Min(255, color.R * factor));
        var g = (byte)Math.Max(0, Math.Min(255, color.G * factor));
        var b = (byte)Math.Max(0, Math.Min(255, color.B * factor));
        return Color.FromArgb(color.A, r, g, b);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}