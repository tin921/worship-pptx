using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
namespace WorshipPptx
{
    public sealed class LocalStore
    {
        public string Root { get; }
        public string Assets => Path.Combine(Root, "images");
        public string History => Path.Combine(Root, "services");
        public string RecoveryWarning { get; private set; } = "";
        public LocalStore(string root) { Root = root; Directory.CreateDirectory(Assets); Directory.CreateDirectory(History); }
        public static T Read<T>(string path)
        {
            using (var file = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(file);
        }
        public static void Write<T>(string path, T value)
        {
            var temp = path + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { new DataContractJsonSerializer(typeof(T)).WriteObject(file, value); file.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public void Save(ServicePlan plan) { plan.Updated = DateTime.Now; Write(Path.Combine(History, plan.Id + ".json"), plan); }
        public List<ServicePlan> LoadHistory()
        {
            var list = new List<ServicePlan>();
            foreach (var file in Directory.GetFiles(History, "*.json"))
            {
                try { var plan = Read<ServicePlan>(file); if (plan?.Items != null && plan.Items.Count > 0) list.Add(plan); }
                catch (Exception ex) when (ex is IOException || ex is System.Runtime.Serialization.SerializationException || ex is System.Xml.XmlException)
                {
                    try { var plan = Read<ServicePlan>(file + ".bak"); if (plan?.Items != null) list.Add(plan); }
                    catch { /* Keep damaged files intact for recovery. */ }
                    RecoveryWarning = ChineseText.Ui("部分紀錄未能讀取；原檔及備份已保留。");
                }
            }
            return list.OrderByDescending(p => p.Updated).ToList();
        }
        public UserSettings LoadSettings()
        {
            var file = Path.Combine(Root, "settings.json");
            if (!File.Exists(file)) return new UserSettings();
            try { return Read<UserSettings>(file) ?? new UserSettings(); }
            catch { RecoveryWarning = ChineseText.Ui("設定未能讀取，原檔已保留。"); return new UserSettings(); }
        }
        public void SaveSettings(UserSettings settings) => Write(Path.Combine(Root, "settings.json"), settings);
    }
}
