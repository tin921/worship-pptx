using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace WorshipPptx;

internal static class InputFocus
{
    public static bool IsField(object target) => (target as Visual)?.GetSelfAndVisualAncestors().Any(c=>c is TextBox or ComboBox or NumericUpDown or CalendarDatePicker or Slider)==true;
    public static void Attach(Window window)
    {
        window.Focusable=true;
        window.AddHandler(InputElement.KeyDownEvent,(_,e)=> {
            if(e.Key!=Key.Escape || !IsField(window.FocusManager?.GetFocusedElement())) return;
            if(window.FocusManager.GetFocusedElement() is Visual visual)
                foreach(var combo in visual.GetSelfAndVisualAncestors().OfType<ComboBox>()) combo.IsDropDownOpen=false;
            window.Focus(); e.Handled=true;
        },RoutingStrategies.Tunnel);
    }
}

public partial class MainWindow
{
    public T ActiveServiceField<T>(string name) where T : Control => HistoryList.GetVisualDescendants().OfType<T>().FirstOrDefault(c=>c.Name==name && ReferenceEquals(c.DataContext,plan));
    private void InitializeNavigation()
    {
        InputFocus.Attach(this);
        Opened+=(_,_)=>Focus();
        AddHandler(KeyDownEvent,Main_KeyDown,RoutingStrategies.Tunnel);
    }
    private void Main_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Handled) return;
        var focused=FocusManager?.GetFocusedElement();
        if(e.Key==Key.Escape) {
            if(BackgroundHost.Child is CompositionPreview preview) preview.CancelPan();
            if(SlideHost.Child is CompositionPreview projection) projection.CancelPan();
            Focus(); e.Handled=true; return;
        }
        if(e.Key==Key.Tab && e.KeyModifiers==KeyModifiers.Control && Lyrics.IsFocused) {
            Lyrics.SelectedText="\t"; e.Handled=true; return;
        }
        if(e.Key==Key.Tab && (e.KeyModifiers==KeyModifiers.None || e.KeyModifiers==KeyModifiers.Shift)) {
            var fields=new Control[]{SongTitle,CopyrightText,SongId,SongOrder,Lyrics,ActiveServiceField<TextBox>("ServiceTitle"),ActiveServiceField<CalendarDatePicker>("ServiceDate"),FontPicker,AutoFontSizeButton,FontSizePicker,TextColorPicker,BoldToggle,AlignLeft,AlignCenter,AlignRight,PositionTop,PositionMiddle,PositionBottom,BackgroundOpacity,FlipBackground};
            var visible=fields.Where(c=>c!=null && c.IsEffectivelyVisible && c.IsEffectivelyEnabled).ToArray();
            var index=Array.FindIndex(visible,c=>ReferenceEquals(c,focused) || (focused as Visual)?.GetVisualAncestors().Contains(c)==true);
            var step=e.KeyModifiers==KeyModifiers.Shift ? -1 : 1;
            if(index<0) index=step>0 ? -1 : 0;
            visible[(index+step+visible.Length)%visible.Length].Focus(NavigationMethod.Tab);
            e.Handled=true; return;
        }
        if(InputFocus.IsField(focused) || e.KeyModifiers!=KeyModifiers.None) return;
        switch(e.Key) {
            case Key.PageUp: case Key.PageDown:
                if(HistoryList.ItemCount>0) HistoryList.SelectedIndex=(HistoryList.SelectedIndex+(e.Key==Key.PageUp ? -1 : 1)+HistoryList.ItemCount)%HistoryList.ItemCount;
                HistoryList.ScrollIntoView(HistoryList.SelectedItem); break;
            case Key.Left: case Key.Right:
                SongTabs.SelectedIndex=(SongTabs.SelectedIndex+(e.Key==Key.Left ? -1 : 1)+plan.Items.Count)%plan.Items.Count;
                (SongTabs.SelectedItem as TabItem)?.BringIntoView(); break;
            case Key.Up: Previous_Click(sender,e); break;
            case Key.Down: Next_Click(sender,e); break;
            default: return;
        }
        Focus(); e.Handled=true;
    }
}
