using Dalamud.Configuration;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using EurekaHelper.System;
using EurekaHelper.XIV;

namespace EurekaHelper
{
    public enum PayloadOptions
    {
        ShoutToChat,
        CopyToClipboard,
        Nothing
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 3;

        public void Initialize() 
        {
            if (CustomMessages.Count == 0)
            {
                CustomMessages.Add("/shout %bossName% POP. %flag%");
            }
            
            if (Version < 3)
            {
                Version = 3;
            }
            
            if (!Enum.IsDefined(typeof(ChatSoundEffect), NMChatSoundEffect))
            {
                DalamudApi.Log.Error($"NM Sound Effect ID is invalid, resetting to default.");
                NMChatSoundEffect = ChatSoundEffect.ChatSoundEffect1;
            }
            if (!Enum.IsDefined(typeof(BaseSoundEffect), NMSoundEffect))
            {
                DalamudApi.Log.Error($"NM Chat Sound Effect ID is invalid, resetting to default.");
                NMSoundEffect = BaseSoundEffect.SoundEffect36;
            }
            if (!Enum.IsDefined(typeof(ChatSoundEffect), BunnyChatSoundEffect))
            {
                DalamudApi.Log.Error($"Bunny Sound Effect ID is invalid, resetting to default.");
                BunnyChatSoundEffect = ChatSoundEffect.ChatSoundEffect6;
            }
            if (!Enum.IsDefined(typeof(BaseSoundEffect), BunnySoundEffect))
            {
                DalamudApi.Log.Error($"Bunny Chat Sound Effect ID is invalid, resetting to default.");
                BunnySoundEffect = BaseSoundEffect.SoundEffect41;
            }
            
            foreach (var alarm in Alarms)
            {
                if (!Enum.IsDefined(typeof(BaseSoundEffect), alarm.SoundEffect))
                {
                    DalamudApi.Log.Error($"Alarm Sound Effect ID is invalid, resetting to default.");
                    alarm.SoundEffect = BaseSoundEffect.SoundEffect36;
                }
                if (!Enum.IsDefined(typeof(ChatSoundEffect), alarm.ChatSoundEffect))
                {
                    DalamudApi.Log.Error($"Alarm Chat Sound Effect ID is invalid, resetting to default.");
                    alarm.ChatSoundEffect = ChatSoundEffect.ChatSoundEffect1;
                }
            }
            
            Save();
        }

        /*
         * General Configurations
         */
        public XivChatType ChatChannel { get; set; } = XivChatType.Echo;

        /*
         * Tracker Configurations
         */
        public bool DisplayFateProgress = false;

        public bool DisplayBunnyFates = false;

        public bool DisplayFatePop = true;

        public bool PlayPopSound = true;

        public bool DisplayToastPop = false;

        public bool AutoPopFate = true;

        public bool RandomizeMapCoords = true;

        public bool AutoCreateTracker = false;

        public bool AutoPopFateWithinRange = false;
        public bool GlobalUseChatSoundEffect = false;

        public bool ShowLevelInTrackerTable = false;

        public List<string> CustomMessages { get; set; } = new();

        public BaseSoundEffect NMSoundEffect { get; set; } = BaseSoundEffect.SoundEffect36;

        public BaseSoundEffect BunnySoundEffect { get; set; } = BaseSoundEffect.SoundEffect41;
        public ChatSoundEffect NMChatSoundEffect { get; set; } = ChatSoundEffect.ChatSoundEffect1;
        public ChatSoundEffect BunnyChatSoundEffect { get; set; } = ChatSoundEffect.ChatSoundEffect6;

        public PayloadOptions PayloadOptions { get; set; } = PayloadOptions.ShoutToChat;

        /*
         * Splatoon Configurations
         */

        // Draws NM aggro-detection ranges via Splatoon IPC. Off by default: requires Splatoon
        // installed, and the aggro range data (System/SplatoonManager.cs AggroRanges.json) is
        // unverified/empty until someone fills it in - see EurekaHelper/System/SplatoonManager.cs.
        public bool EnableSplatoonAggroRanges = false;

        // 仇恨範圍半徑還沒量出來的（Magic/Blood 兩型出廠是 0）要不要在怪腳下畫一個小標記圈。
        // 半徑 0 在 Splatoon 上等於「完全不畫」，畫面上跟「這隻沒有威脅」長得一模一樣 —— 把
        // 未知畫成 0 會直接誤導玩家。開起來會改畫一個固定大小的小圈，意思是「這隻是魔法／
        // 血量偵測型，但範圍未知」，而不是假裝範圍就是那麼大。
        // 預設關閉：沿用現行行為（不畫），要不要改預設由使用者裁決。
        public bool ShowUnmeasuredAggroMarkers = false;

        // 在優雷卡／兵武塔標示隱藏陷阱與傳送門的「可能有／已確認」兩態（見
        // System/HazardManager.cs）。預設關閉：需要玩家自己先分類 DataId 才會有東西畫，
        // 而且需要裝 Splatoon。
        public bool EnableHazardMarkers = false;

        /*
         * Relic Window Configurations
         */
        public bool AutoOpenRelicWindowInEureka = false;

        /*
         * Per-zone tracker memory: the last tracker (code/password) connected in each Eureka
         * zone, keyed by zone index (1=Anemos, 2=Pagos, 3=Pyros, 4=Hydatos - see
         * Utils.GetIndexOfZone), plus the server ID it was joined on. Persisted so a plugin
         * reload/restart can silently rejoin the same tracker on returning to a zone whose
         * server ID still matches, instead of leaving that zone's tab empty. See
         * System/ZoneManager.cs for the reconnect logic.
         */
        public Dictionary<int, TrackerMemoryEntry> TrackerMemory = new();

        /*
         * Last known server ID seen per zone (1=Anemos, 2=Pagos, 3=Pyros, 4=Hydatos), persisted so
         * a plugin reload while already standing inside an instance doesn't lose it - the game only
         * reports the server ID on an actual zone-entry event (see ZoneManager.InitZoneDetour),
         * which won't refire just because the plugin restarted mid-instance. ZoneManager seeds its
         * in-memory copy from this on construction.
         */
        public Dictionary<int, ushort> LastServerIdPerZone = new();

        // 每次尋寶推算出新位置、更新地圖旗標時，順便呼叫 vnavmesh 的 "/vnav moveflag" 自動走向
        // 旗標。需要玩家自己裝 vnavmesh 外掛，沒裝的話這個指令送出去會被遊戲當成無效指令、不會
        // 有任何效果。預設關閉 - 自動移動角色的行為有其風險（可能把你帶去危險的地方），要玩家
        // 自己選擇開啟。
        public bool TreasureHuntAutoMoveFlag = false;

        // 每次挖到寶藏時，把找到當下的座標＋這一輪的完整提示鏈存一筆進來（見
        // TreasureHuntManager.OnTreasureFound），跨 session 持久化，供之後回頭校正距離等級的
        // 碼數區間。
        public List<TreasureFoundRecord> TreasureHuntHistory = new();

        /*
         * Server ID Configurations
         */
        public bool DisplayServerId = false;

        public bool DisplayServerIdInServerInfo = false;

        /*
         * Elemental Configurations
         */

        public bool DisplayElemental = true;

        public bool DisplayElementalToast = false;

        public bool ElementalCrowdsource = true;

        public bool ElementalAutoMark = false;

        public bool ElementalAlwaysClear = false;

        public PayloadOptions ElementalPayloadOptions { get; set;} = PayloadOptions.CopyToClipboard;

        /*
         * Relic Configurations
         */
        public List<uint> CompletedRelics { get; set; } = new();

        /*
         * Alarm Configurations
         */
        public List<EurekaAlarm> Alarms { get; set; } = new();

        public void Save() => DalamudApi.PluginInterface.SavePluginConfig(this);
    }

    [Serializable]
    public class TrackerMemoryEntry
    {
        public string Code = string.Empty;
        public string Password = string.Empty;
        public ushort ServerId;
    }
}
