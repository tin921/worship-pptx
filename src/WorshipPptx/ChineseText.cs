using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using OpenccNetLib;

namespace WorshipPptx;

// Keep original document strings in storage. Conversion is a view of that source,
// not a destructive round trip (for example, 後/后 and 發/髮 both merge in Simplified).
public sealed class ChineseText : INotifyPropertyChanged
{
    public const string Traditional = "zh-Hant", Simplified = "zh-Hans";
    public static ChineseText Current { get; } = new();
    private static readonly Lazy<Opencc> toSimplified = new(() => new Opencc("t2s"));
    private static readonly Lazy<Opencc> toTraditional = new(() => new Opencc("s2t"));
    private static readonly ConcurrentDictionary<(string Language, string Text), string> cache = new();
    private string language = Traditional;
    public string Language => language;
    public static bool IsSimplified => Current.Language == Simplified;
    public static string CultureName => IsSimplified ? "zh-CN" : "zh-HK";
    public event PropertyChangedEventHandler PropertyChanged;

    public static void SetLanguage(string value)
    {
        value = value == Simplified ? Simplified : Traditional;
        if (Current.language == value) return;
        Current.language = value;
        Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs(nameof(Language)));
    }

    public static string Convert(string text, string language)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var key = (language == Simplified ? Simplified : Traditional, text);
        if (cache.Count > 4096) cache.Clear();
        return cache.GetOrAdd(key, k =>
        {
            var converter=k.Item1 == Simplified ? toSimplified.Value : toTraditional.Value;
            // Worship pronouns are intentional in both scripts. Generic dictionaries
            // can change 祢 to the uncommon 禰, so preserve these authored characters.
            return string.Concat(Regex.Split(k.Text,"([祢祂])").Select(part=>part is "祢" or "祂" ? part : converter.Convert(part)));
        });
    }

    public static string Content(string source) => Convert(source, Current.Language);
    // All interface source strings use Traditional Chinese; leave that authored wording intact.
    public static string Ui(string traditional) => IsSimplified ? Convert(traditional, Simplified) : traditional;
}

// A live XAML binding updates labels, tooltips and accessibility names immediately.
public sealed class TrExtension : MarkupExtension
{
    public string Text { get; set; }
    public TrExtension() { }
    public TrExtension(string text) => Text = text;
    public override object ProvideValue(IServiceProvider serviceProvider) => For(Text);
    public static Binding For(string text) => new Binding(nameof(ChineseText.Language))
    {
        Source = ChineseText.Current,
        Mode = BindingMode.OneWay,
        Converter = new TranslationConverter(text)
    };

    private sealed class TranslationConverter(string text) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ChineseText.Ui(text);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

public sealed class TrText : TextBlock
{
    public TrText(string text) => this.Bind(TextProperty, TrExtension.For(text));
}
