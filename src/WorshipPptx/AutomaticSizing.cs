using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WorshipPptx;

public partial class MainWindow
{
    private static readonly double[] presetFontSizes={18,20,24,28,32,36,40,44,48,54,60,64,72};
    private readonly ObservableCollection<double> fontSizes=new(presetFontSizes);
    private bool syncingFontSize;
    private bool EditingSongTitle => selected!=null && page==0 && SlideLayout.HasSectionTags(selected.Lyrics);
    private void SyncFontSizePicker(double? actual=null)
    {
        if(selected==null) return;
        syncingFontSize=true;
        try {
            var song=SlideLayout.HasSectionTags(selected.Lyrics);
            var title=EditingSongTitle;
            var automatic=song && (title ? selected.Formatting.AutoTitleFontSize : selected.Formatting.AutoFontSize);
            AutoFontSizeButton.IsChecked=automatic;
            AutoFontSizeButton.IsEnabled=song;
            FontSizeScope.IsVisible=song;
            FontSizeScope.Text=ChineseText.Ui(title ? "歌名字號" : "歌詞字號");
            var size=automatic && actual.HasValue ? actual.Value : title ? selected.Formatting.TitleFontSize : selected.Formatting.FontSize;
            var choices=presetFontSizes.Append(size).Distinct().OrderBy(value=>value).ToArray();
            // Automatic sizing is state, not a dropdown option. Keep one numeric
            // entry for the current calculated size, even between preset sizes.
            if(!fontSizes.SequenceEqual(choices)) {
                fontSizes.Clear(); foreach(var choice in choices) fontSizes.Add(choice);
            }
            FontSizePicker.SelectedItem=size;
            ToolTip.SetTip(FontSizePicker,title ? ChineseText.Ui("只調整歌名頁字號（pt），不影響歌詞") : automatic ? ChineseText.Ui("按最長歌詞段落統一字號；選擇數字可固定字號") : ChineseText.Ui("固定字號（pt）"));
            ToolTip.SetTip(AutoFontSizeButton,ChineseText.Ui(title ? "按歌名及版權資料自動調整；不影響歌詞字號" : "按最長歌詞段落統一字號；持續自動調整，直至選擇固定字號"));
        } finally { syncingFontSize=false; }
    }
    private void AutoFontSize_Click(object sender,RoutedEventArgs e)
    {
        if(loading || selected==null) return;
        Capture();
        if(EditingSongTitle) selected.Formatting.AutoTitleFontSize=true;
        else selected.Formatting.AutoFontSize=true;
        // Clicking the active button recalculates; only a numeric choice locks size.
        AutoFontSizeButton.IsChecked=true;
        UpdateContentMode(); RefreshPreview(); SaveDraft();
    }
}
