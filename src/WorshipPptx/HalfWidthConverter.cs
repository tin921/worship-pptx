using System;
using System.Globalization;
using Avalonia.Data.Converters;
namespace WorshipPptx;
public sealed class HalfWidthConverter : IValueConverter
{
    public static HalfWidthConverter Instance { get; }=new();
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture) => value is double width ? Math.Max(50,width/2-16) : 80d;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture) => throw new NotSupportedException();
}
