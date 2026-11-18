using System;
using System.IO;

namespace EurekaHelper.System
{
    // 選用功能的副檔（AggroRanges／SeenMonsters／Hazards）讀不懂時改用空資料，下一次存檔就會整份覆蓋原檔。
    // 覆蓋前先在同目錄留一份 <檔名>.bad-<時間戳>；既有備份不覆寫，同內容只留一份（副檔只在變動時存，
    // 使用者不動時每次載入都會讀到同一份壞檔）。備份失敗只記錄，不擋載入；永不擲例外。
    internal static class SideFileBackup
    {
        public static void BackupUnreadable(string path, string owner, Func<DateTime> now = null)
        {
            var name = Path.GetFileName(path);
            try
            {
                if (!File.Exists(path))
                    return;

                var original = File.ReadAllBytes(path);
                var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
                foreach (var old in new DirectoryInfo(dir).EnumerateFiles(name + ".bad-*"))
                {
                    if (old.Length == original.Length && File.ReadAllBytes(old.FullName).AsSpan().SequenceEqual(original))
                    {
                        DalamudApi.Log.Warning($"[{owner}] {name} 讀不懂，相同內容已備份過（{old.Name}），這次不再另存；之後存檔會以目前的資料覆蓋原檔");
                        return;
                    }
                }

                var backup = Path.Combine(dir, $"{name}.bad-{(now ?? (() => DateTime.Now))():yyyyMMdd-HHmmss-fff}");
                using (var fs = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    fs.Write(original);
                DalamudApi.Log.Warning($"[{owner}] {name} 讀不懂，原檔已備份為 {Path.GetFileName(backup)}；之後存檔會以目前的資料覆蓋原檔");
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Warning($"[{owner}] 備份讀不懂的 {name} 失敗：{ex.Message}；之後存檔會以目前的資料覆蓋原檔");
            }
        }

        // Newtonsoft 對空白／全 NUL 的內容回 null、不擲例外：仍算讀壞，先備份再原樣交回 null，呼叫端照舊改用空資料。
        public static T NullAsUnreadable<T>(T parsed, string path, string owner) where T : class
        {
            if (parsed == null)
            {
                DalamudApi.Log.Warning($"[{owner}] {Path.GetFileName(path)} 內容是空的或無法辨識（解析結果是 null），改用空資料");
                BackupUnreadable(path, owner);
            }

            return parsed;
        }
    }
}
