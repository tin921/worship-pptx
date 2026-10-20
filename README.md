# 敬拜簡報 · Worship PPTX

為香港教會預備主日敬拜簡報的輕量 Windows 程式。輸入詩歌及經文、安排重唱次序、選擇背景，即可製作文字只佔上半部的 PowerPoint。

**[下載 Windows 安裝程式](https://github.com/tin921/worship-pptx/releases/latest)** · [使用說明](docs/usage.md) · [開發與驗證](docs/development.md)

![敬拜簡報的實際程式畫面](docs/screenshot.png)

## 功能

- 香港繁體中文介面、淺色主題、獨立分頁和可加詩歌。
- 預設次序：宣召經文 → 詩歌一 → 詩歌二 → 讀經 → 詩歌三；開啟時顯示第一首詩歌。
- 在同一歌詞欄用 `[v1]`、`[c]`、`[v2]` 標記段落；用純文字 `v1 c v2 c c` 指定次序及重唱。
- 空行指定分頁；長句自動換行，每頁最多三行。歌詞、經文和版權文字全部位於上半部。
- 大型即時預覽、右側直排縮圖，以及整個崇拜的逐頁預覽。
- 按背景中央「編輯背景」：瀏覽本機圖片、輸入直接圖片網址、拖放或從剪貼簿貼上。
- 內置香港黑體及宋體，毋須另裝字體；所選完整字體會嵌入 PPTX，文字仍可編輯。
- 自動儲存、崇拜紀錄、原子寫入及上一版本備份。
- 版權欄、個別 CCLI 詩歌編號，以及獨立的教會授權編號設定。
- 本機匯出 `.pptx`，毋須安裝 PowerPoint、登入帳戶或申請 API。下載圖片網址時才需要網絡。

## 安裝

在 [Releases](https://github.com/tin921/worship-pptx/releases/latest) 下載 `WorshipPptx-1.0.0-Setup.msi`，開啟安裝。程式安裝至目前使用者的 `%LOCALAPPDATA%\Programs\WorshipPptx`，並加入開始功能表；不會在系統安裝字體。

也可下載 `WorshipPptx-1.0.0-Portable.zip`，解壓後執行 `WorshipPptx.exe`。請保留旁邊的 `Fonts` 資料夾。

**系統需求：** Windows 10（最低相容目標：1507 / build 10240）或 Windows 11，x86 / x64，.NET Framework 4.6.2 或更新版本。較新的 Windows 已內置相容 Framework；舊版 Windows 10 如有需要，先安裝 [Microsoft .NET Framework 4.6.2](https://dotnet.microsoft.com/download/dotnet-framework/net462)。這是相容性目標，不代表舊 Windows 仍獲 Microsoft 支援；目前測試環境及限制見 [驗證紀錄](docs/development.md#validation)。

此版本未作數碼簽署。Release 附有 `SHA256SUMS.txt`，供核對下載完整性。

## 字體與可攜性

內置 **Noto Sans CJK HK** 及 **Noto Serif CJK HK** 的常規靜態字體，支援繁簡中文。程式以私人檔案載入字體，並把所選字體完整嵌入 PowerPoint，無需倚賴接收電腦已有的中文字體。完整嵌入會增加檔案大小。其他簡報檢視器對嵌入字體的支援不一，建議用桌面版 PowerPoint 播放。

## 開發

C# / WPF，目標 .NET Framework 4.6.2，AnyCPU；沒有 Electron、WebView、雲端 AI 或圖片搜尋服務。

```powershell
git clone https://github.com/tin921/worship-pptx.git
cd worship-pptx
.\scripts\build.ps1
.\scripts\package.ps1
```

需要 Windows、Visual Studio Build Tools 2022（MSBuild 及 .NET desktop build tools）及 PowerShell。參考組件會由 NuGet 還原；打包腳本會下載經雜湊核對的 WiX 3.14.1。詳細步驟見 [開發說明](docs/development.md)。

程式碼採用 [MIT License](LICENSE)，字體採用 SIL OFL 1.1，詳見 [第三方聲明](THIRD-PARTY-NOTICES.md)。
