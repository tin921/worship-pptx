using System;
using System.IO;

namespace WorshipPptx;

public static class LocalPresentationFiles
{
    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"WorshipPptx");
    public static string NormalizeFolder(string value)
    {
        var folder=Environment.ExpandEnvironmentVariables((value ?? "").Trim().Trim('"'));
        if(string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder) || folder.IndexOfAny(Path.GetInvalidPathChars())>=0)
            throw new ArgumentException(ChineseText.Ui("請輸入完整的本機儲存資料夾路徑。"));
        return Path.GetFullPath(folder);
    }
    public static string SaveCopy(string source,string folder,string fileName,bool replace)
    {
        folder=NormalizeFolder(folder); DriveFileName.Validate(fileName); Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,fileName);
        // Publish a complete copy atomically; a copy failure must not damage an existing deck.
        var staging=Path.Combine(folder,"."+Guid.NewGuid().ToString("N")+".tmp");
        try {
            File.Copy(source,staging);
            if(replace) { File.Move(staging,path,true); return path; }
            for(var number=0;;number++) {
                var target=number==0 ? path : Path.Combine(folder,Path.GetFileNameWithoutExtension(fileName)+" ("+number+").pptx");
                try { File.Move(staging,target,false); return target; }
                catch(IOException) when(File.Exists(target)) { }
            }
        }
        finally { if(File.Exists(staging)) File.Delete(staging); }
    }
    public static string RememberFolder(string root,string value)
    {
        var folder=NormalizeFolder(value); var store=new LocalStore(root); var settings=store.LoadSettings();
        if(settings.PptxSaveFolder!=folder) { settings.PptxSaveFolder=folder; store.SaveSettings(settings); }
        return folder;
    }
}
