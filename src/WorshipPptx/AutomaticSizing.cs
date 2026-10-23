using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace WorshipPptx;

public partial class MainWindow
{
    private readonly ObservableCollection<object> fontSizes=new() { "自動",18.0,20.0,24.0,28.0,32.0,36.0,40.0,44.0,48.0,54.0,60.0,64.0,72.0 };
    private bool syncingFontSize;
    private void SyncFontSizePicker(double? actual=null)
    {
        if(selected==null) return;
        syncingFontSize=true;
        try {
            var automatic=selected.Formatting.AutoFontSize && SlideLayout.HasSectionTags(selected.Lyrics);
            // Show the effective, shared song size directly in the control.
            fontSizes[0]=ChineseText.Ui("自動")+(actual.HasValue && automatic ? " "+actual.Value : "");
            FontSizePicker.SelectedItem=automatic ? fontSizes[0] : selected.Formatting.FontSize;
            ToolTip.SetTip(FontSizePicker,automatic ? ChineseText.Ui("按最長段落統一字號；選擇數字可固定字號") : ChineseText.Ui("固定字號（pt）；選擇「自動」恢復每段一頁"));
        } finally { syncingFontSize=false; }
    }
}
