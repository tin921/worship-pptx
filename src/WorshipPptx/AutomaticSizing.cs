using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;

namespace WorshipPptx;

public partial class MainWindow
{
    private static readonly double[] presetFontSizes={18,20,24,28,32,36,40,44,48,54,60,64,72};
    private readonly ObservableCollection<double> fontSizes=new(presetFontSizes);
    private bool syncingFontSize;
    private void SyncFontSizePicker(double? actual=null)
    {
        if(selected==null) return;
        syncingFontSize=true;
        try {
            var automatic=selected.Formatting.AutoFontSize && SlideLayout.HasSectionTags(selected.Lyrics);
            var size=automatic && actual.HasValue ? actual.Value : selected.Formatting.FontSize;
            var choices=presetFontSizes.Append(size).Distinct().OrderBy(value=>value).ToArray();
            // Automatic sizing is state, not a dropdown option. Keep one numeric
            // entry for the current calculated size, even between preset sizes.
            if(!fontSizes.SequenceEqual(choices)) {
                fontSizes.Clear(); foreach(var choice in choices) fontSizes.Add(choice);
            }
            FontSizePicker.SelectedItem=size;
            ToolTip.SetTip(FontSizePicker,automatic ? ChineseText.Ui("按最長段落統一字號；選擇數字可固定字號") : ChineseText.Ui("固定字號（pt）"));
        } finally { syncingFontSize=false; }
    }
}
