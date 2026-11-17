using System;
using System.IO;

namespace EurekaHelper.System
{
    // 選用功能的副檔（AggroRanges／SeenMonsters／Hazards）讀不懂時改用空資料，下一次存檔就會整份覆蓋原檔。
    // 覆蓋前先在同目錄留一份 <檔名>.bad-<時間戳>；備份失敗只記錄，不擋載入。
    internal static class SideFileBackup
    {
        public static void BackupUnreadable(string path, string owner)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var backup = $"{path}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(path, backup, overwrite: false);
                DalamudApi.Log.Warning($"[{owner}] {Path.GetFileName(path)} 讀不懂，原檔已備份為 {Path.GetFileName(backup)}；之後存檔會以目前的資料覆蓋原檔");
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Warning($"[{owner}] 備份讀不懂的 {Path.GetFileName(path)} 失敗：{ex.Message}");
            }
        }
    }
}
