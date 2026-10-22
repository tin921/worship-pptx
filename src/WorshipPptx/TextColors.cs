using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace WorshipPptx;

public sealed class TextColorChoice
{
    public string Name { get; }
    public string Hex { get; }
    public IBrush Brush { get; }
    public TextColorChoice(string name,string hex) { Name=name; Hex=hex; Brush=Avalonia.Media.Brush.Parse(hex); }
}

public static class TextColors
{
    public static readonly IReadOnlyList<TextColorChoice> All=new[] {
        new TextColorChoice("白色","#FFFFFF"),new TextColorChoice("米白","#FFF4DC"),new TextColorChoice("淺灰","#D9D9D9"),new TextColorChoice("銀灰","#A6A6A6"),new TextColorChoice("灰色","#808080"),new TextColorChoice("深灰","#444444"),new TextColorChoice("炭黑","#202020"),new TextColorChoice("純黑","#000000"),
        new TextColorChoice("櫻花","#FFE3E6"),new TextColorChoice("粉紅","#FFC4D6"),new TextColorChoice("珊瑚","#FF8F80"),new TextColorChoice("鮭紅","#F06B6B"),new TextColorChoice("紅色","#B83D3D"),new TextColorChoice("玫紅","#D94C8A"),new TextColorChoice("莓紅","#9E2A52"),new TextColorChoice("酒紅","#7F2545"),
        new TextColorChoice("淺杏","#FFE5CB"),new TextColorChoice("蜜桃","#FFD0A1"),new TextColorChoice("杏色","#FFB779"),new TextColorChoice("橙色","#F39C3D"),new TextColorChoice("深橙","#D97525"),new TextColorChoice("銅色","#AC592A"),new TextColorChoice("棕色","#795548"),new TextColorChoice("深棕","#4E342E"),
        new TextColorChoice("奶黃","#FFF9D6"),new TextColorChoice("淺黃","#FFF4A3"),new TextColorChoice("黃色","#FFE46B"),new TextColorChoice("檸檬黃","#FFD633"),new TextColorChoice("琥珀","#E8B82F"),new TextColorChoice("金色","#D6A744"),new TextColorChoice("赭金","#A78128"),new TextColorChoice("深金","#6D571D"),
        new TextColorChoice("嫩綠","#E4F2D7"),new TextColorChoice("薄荷綠","#BDE8CD"),new TextColorChoice("淺綠","#A8D49A"),new TextColorChoice("草綠","#7DB569"),new TextColorChoice("翠綠","#4E965B"),new TextColorChoice("綠色","#326B4B"),new TextColorChoice("森林綠","#235139"),new TextColorChoice("墨綠","#163B2C"),
        new TextColorChoice("淺青","#D8F3EF"),new TextColorChoice("水綠","#A4E4DD"),new TextColorChoice("碧綠","#78D8CE"),new TextColorChoice("青色","#53CFCE"),new TextColorChoice("湖綠","#27AAA8"),new TextColorChoice("深青","#208482"),new TextColorChoice("松石綠","#14645F"),new TextColorChoice("深湖綠","#104442"),
        new TextColorChoice("冰藍","#E1F0FF"),new TextColorChoice("淺藍","#A5D8FF"),new TextColorChoice("天藍","#78B9EA"),new TextColorChoice("晴藍","#4D98D4"),new TextColorChoice("鈷藍","#3677BC"),new TextColorChoice("藍色","#2457A7"),new TextColorChoice("深藍","#163A62"),new TextColorChoice("午夜藍","#102640"),
        new TextColorChoice("淡紫","#EEE4FA"),new TextColorChoice("薰衣草","#D8C4F0"),new TextColorChoice("丁香紫","#BDA1DF"),new TextColorChoice("紫色","#8154B5"),new TextColorChoice("葡萄紫","#684094"),new TextColorChoice("梅紫","#8E4D80"),new TextColorChoice("深紫","#492C68"),new TextColorChoice("墨紫","#30203E")
    };
    public static TextColorChoice Get(string hex) => All.FirstOrDefault(c=>c.Hex.Equals(hex,StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
