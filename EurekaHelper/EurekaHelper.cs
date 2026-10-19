using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Numerics;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EurekaHelper.System;
using EurekaHelper.Windows;
using EurekaHelper.XIV;
using EurekaHelper.XIV.Zones;

namespace EurekaHelper;
    public class EurekaHelper : IDalamudPlugin
    {
        public string Name => "Eureka Helper";
        public static Configuration Config { get; private set; }
        public static EurekaHelper Plugin { get; private set; }
        public int CurrentDatacenterId { get; private set; } = 0;

        internal readonly WindowSystem WindowSystem;
        internal readonly PluginWindow PluginWindow;
        internal readonly RelicWindow RelicWindow;
        internal readonly AlarmWindow AlarmWindow;

        internal readonly FateManager FateManager;
        internal readonly ZoneManager ZoneManager;
        internal readonly ElementalManager ElementalManager;
        internal readonly InventoryManager InventoryManager;
        internal readonly AlarmManager AlarmManager;
        internal readonly TreasureHuntManager TreasureHuntManager;
        internal SplatoonManager SplatoonManager;
        internal HazardManager HazardManager;
        internal CookieBoxTracker CookieBoxTracker;

        public EurekaHelper(IDalamudPluginInterface pluginInterface)
        {
            Plugin = this;

            DalamudApi.Initialize(this, pluginInterface);
            Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
            Config.Initialize();

            Utils.BuildLgbData();

            FateManager = new(this);
            ZoneManager = new();
            ElementalManager = new();
            InventoryManager = new();
            AlarmManager = new();
            TreasureHuntManager = new();

            if (Config.EnableSplatoonAggroRanges)
                SplatoonManager = new();

            if (Config.EnableHazardMarkers)
                HazardManager = new();

            PluginWindow = new(this);
            RelicWindow = new(this);
            AlarmWindow = new(this);

            // Needs PluginWindow to exist first (it reads/writes each zone's live tracker via
            // PluginWindow.GetConnection).
            CookieBoxTracker = new();

            // Retry the tracker rejoin ZoneManager's constructor couldn't do yet (PluginWindow
            // didn't exist at that point) - covers reloading the plugin while already standing
            // inside an Eureka instance. See ZoneManager.TryRejoinCurrentZoneTracker.
            ZoneManager.TryRejoinCurrentZoneTracker();

            WindowSystem = new("Eureka Helper");
            WindowSystem.AddWindow(PluginWindow);
            WindowSystem.AddWindow(RelicWindow);
            WindowSystem.AddWindow(AlarmWindow);

            DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            DalamudApi.ClientState.Login += OnLogin;
            DalamudApi.Framework.RunOnTick(UpdateDatacenterId);
        }

#if DEBUG
        [Command("/testeh")]
        private void TestShout(string command, string argument)
        {
            var useGlobalChat = Config.GlobalUseChatSoundEffect;
            var showBunnyFate = Config.DisplayBunnyFates;
            var payload = Config.PayloadOptions;
            var delayTicks = 1;
            var delay = TimeSpan.FromMilliseconds(500);
            
            Config.DisplayBunnyFates = true;
            Config.GlobalUseChatSoundEffect = true;
            PrintMessage("[EH Test] Chat, then base");
            var bunnyfate = new EurekaFate(1425, null, 827, 515, "Test", "Bunny Boss", "Bun",
                new Vector2(14.0f, 21.5f), null, Vector2.Zero, EurekaWeather.None, EurekaWeather.None,
                EurekaElement.Unknown, EurekaElement.Unknown, false, 50, false, true);
            var realfate = new EurekaFate(1425, null, 827, 515, "Test", "Madge Boss", "Madge",
                new Vector2(14.0f, 21.5f), null, Vector2.Zero, EurekaWeather.None, EurekaWeather.None,
                EurekaElement.Unknown, EurekaElement.Unknown, false, 50, false, false);
            
            DalamudApi.Framework.RunOnTick(() => FateManager.DisplayFatePop(bunnyfate), delay * delayTicks++);
            DalamudApi.Framework.RunOnTick(() =>
            {
                Config.PayloadOptions = PayloadOptions.Nothing;
                FateManager.DisplayFatePop(realfate);
            }, delay * delayTicks++);
            DalamudApi.Framework.RunOnTick(() => FateManager.DisplayFatePop(bunnyfate), delay * delayTicks++);

            DalamudApi.Framework.RunOnTick(() => { Config.GlobalUseChatSoundEffect = false; }, delay * delayTicks++);

            DalamudApi.Framework.RunOnTick(() =>
            {
                Config.PayloadOptions = PayloadOptions.CopyToClipboard;
                FateManager.DisplayFatePop(realfate);
            }, delay * delayTicks++);

            DalamudApi.Framework.RunOnTick(() =>
            {
                Config.PayloadOptions = PayloadOptions.ShoutToChat;
                FateManager.DisplayFatePop(realfate);
            }, delay * delayTicks++);
            
            DalamudApi.Framework.RunOnTick(() =>
            {
                Config.DisplayBunnyFates = showBunnyFate;
                Config.GlobalUseChatSoundEffect = useGlobalChat;
                Config.PayloadOptions = payload;
            }, delay * delayTicks++);
            
            // TODO: add some additional test cases to generate alarms
        }
#endif

        [Command("/eurekahelper")]
        [Aliases(false, "/ehelper", "/eh")]
        [HelpMessage("Opens / Closes the configuration window")]
        private void ToggleConfig(string command, string argument) => DrawConfigUI();

        [Command("/arisu")]
        [Aliases(true, "/miniarisu", "/mercy")]
        [HelpMessage("Display next weather for Crab, Cassie & Skoll.")]
        private void Arisu(string command, string argument)
        {
            var crabWeatherTimes = EurekaPagos.GetWeatherForecast(EurekaWeather.Fog, 2);
            var cassieWeatherTimes = EurekaPagos.GetWeatherForecast(EurekaWeather.Blizzards, 2);
            var skollWeatherTimes = EurekaPyros.GetWeatherForecast(EurekaWeather.Blizzards, 2);

            PrintMessage(Loc.Text("Weather timers for important NMs:"));

            #region Crab/KA
            var crabTime1 = crabWeatherTimes[0];
            var crabTime2 = crabWeatherTimes[1];
            var crabBuiltString = Utils.ArisuStringbuilder("Crab/KA", "Fog", crabTime1, crabTime2);
            PrintMessage(crabBuiltString.BuiltString);
            DalamudApi.Log.Information(crabBuiltString.ToString());
            #endregion

            #region Cassie
            var cassieTime1 = cassieWeatherTimes[0];
            var cassieTime2 = cassieWeatherTimes[1];
            var cassieBuiltString = Utils.ArisuStringbuilder("Cassie", "Blizzards", cassieTime1, cassieTime2);
            PrintMessage(cassieBuiltString.BuiltString);
            DalamudApi.Log.Information(cassieBuiltString.ToString());
            #endregion

            #region Skoll
            var skollTime1 = skollWeatherTimes[0];
            var skollTime2 = skollWeatherTimes[1];
            var skollBuildString = Utils.ArisuStringbuilder("Skoll", "Blizzards", skollTime1, skollTime2);
            PrintMessage(skollBuildString.BuiltString);
            DalamudApi.Log.Information(skollBuildString.ToString());
            #endregion
        }

        [Command("/etrackers")]
        [HelpMessage("Attempts to get a tracker for the current instance in the same datacenter.")]
        private async void ETrackers(string command, string argument)
        {
            // 連線前先判斷：不支援的資料中心（台服＝陸行鳥，不在
            // Constants.DatacenterStringToEurekaId 裡，CurrentDatacenterId 恆為 0）不該每次執行
            // 都先白連一次 ffxiv-eureka.com 的 websocket 再關掉。
            if (CurrentDatacenterId == 0)
            {
                PrintMessage(Loc.Text("This datacenter is not supported currently. Please submit an issue if you think this is incorrect."));
                return;
            }

            // async void：await 之後擲出的例外會直接結束遊戲，所以整段包住。
            EurekaConnectionManager connectionManager = null;
            try
            {
                connectionManager = await EurekaConnectionManager.Connect();

                await connectionManager.Send(JArray.Parse(@$"[ ""1"", ""1"", ""datacenter:{CurrentDatacenterId}"", ""phx_join"", {{}} ]").ToString());
                await Task.Delay(500);

                var trackerList = connectionManager.GetCurrentTrackers();
                await connectionManager.Close();
                connectionManager = null;

                var filteredList = trackerList.Where(x => (int)x["relationships"]["zone"]["data"]["id"] == Utils.GetIndexOfZone(DalamudApi.ClientState.TerritoryType));
                if (!filteredList.Any())
                {
                    PrintMessage(Loc.Text("Unable to find any public trackers."));
                    return;
                }

                var sb = new SeStringBuilder()
                    .AddText(Loc.Text("Found"))
                    .AddUiForeground(58)
                    .AddText($" {filteredList.Count()} ")
                    .AddUiForegroundOff()
                    .AddText(Loc.Text("public trackers:"));
                PrintMessage(sb.BuiltString);

                foreach (var tracker in filteredList)
                    PrintMessage(Utils.CombineUrl(Constants.EurekaTrackerLink, tracker["id"].ToString()));
            }
            catch (Exception ex)
            {
                try
                {
                    DalamudApi.Log.Warning($"[ETrackers] 取得公開追蹤器清單失敗：{ex.Message}");
                    PrintMessage(Loc.Text("Failed to get the public tracker list from ffxiv-eureka.com."));
                    if (connectionManager != null)
                        await connectionManager.Close();
                }
                catch
                {
                    // 收尾本身失敗也不能再往外丟。
                }
            }
        }

        [Command("/erelic")]
        [HelpMessage("Opens / Closes the Eureka Relic helper window")]
        private void RelicHelper(string command, string argument) => RelicWindow.IsOpen ^= true;

        [Command("/ealarms")]
        [HelpMessage("Opens / Closes the Eureka Alarms window")]
        private void AlarmCommand(string command, string argument) => AlarmWindow.IsOpen ^= true;

#if DEBUG
        [Command("/edebug")]
        [DoNotShowInHelp]
        private void Debug(string command, string argument)
        {

        }
#endif

        private void DrawUI() => WindowSystem.Draw();
        private void DrawConfigUI() => PluginWindow.IsOpen ^= true;

        /// <summary>還沒送出的聊天訊息。<b>順序就是呼叫順序。</b></summary>
        private static readonly ConcurrentQueue<XivChatEntry> PendingChatLines = new();

        /// <summary>
        /// 這個外掛唯一的聊天輸出入口。
        /// </summary>
        /// <remarks>
        /// 🔴🔴 <b>為什麼要排到 framework 執行緒才送出。</b>
        /// 本 pin 的 Dalamud <c>ChatGui.Print</c> 只是把項目 <c>Enqueue</c> 進一個
        /// <b>沒有任何同步</b>的 <c>Queue&lt;XivChatEntry&gt;</c>，
        /// 而 <c>UpdateQueue</c> 在 framework 執行緒上 <c>TryDequeue</c>。從別的執行緒呼叫
        /// ⇒ 與 framework 執行緒並行改同一個 <c>Queue</c>，<b>失敗形式不是「訊息晚一點出現」
        /// 而是那個佇列本身壞掉</b>（而且壞掉之後受害的是所有外掛的聊天輸出）。
        /// <para>
        /// 🔴 本外掛從非 framework 執行緒進來的路徑：<c>/etrackers</c> 在 <c>await EurekaConnectionManager.Connect()</c> 之後的呼叫點
        /// （Dalamud 沒有 SynchronizationContext，續體一律在執行緒池）；<c>PluginWindow.CreateTracker</c>／<c>ExportTracker</c>
        /// 的成功訊息（同樣在 await 之後，本身又是從 <c>Task.Run</c> 裡呼叫的）；<c>FateManager.DisplayFatePop</c>：除了 framework 上的
        /// <c>OnUpdate</c>／<c>OnChatMessage</c> 之外，<c>CookieBoxTracker</c> 的 SSE 串流迴圈也會經由
        /// <c>ProcessNode → ApplySpawn(notify: true) → DisplayFatePop</c> 走到這裡。
        /// </para>
        /// <para>
        /// 📌 <c>IFramework.RunOnFrameworkThread</c> 在<b>已經是</b> framework 執行緒時就地同步執行
        /// ⇒ 指令、ImGui 回呼、<c>Framework.Update</c> 與
        /// <c>InitZoneDetour</c>（遊戲主執行緒）那些路徑不必等下一幀，當場就送出。
        /// </para>
        /// <para>
        /// 🔑 <b>為什麼還要自己排一個佇列</b>：Dalamud 的 <c>ThreadBoundTaskScheduler</c> 用
        /// <c>ConcurrentDictionary</c> 存待跑的工作、<c>Run()</c> 走訪它的 <c>Keys</c>
        /// ⇒ <b>不保證先進先出</b>。把每一次 <c>Print</c> 各自包成一個排程工作的話，
        /// <c>/arisu</c>（一次四行）與 <c>/etrackers</c>（標題行後面接一串追蹤器網址）
        /// 的順序會變成隨機的。自己排隊、到了 framework 執行緒一次排乾，
        /// 順序就與呼叫順序<b>逐字相同</b>。
        /// </para>
        /// <para>
        /// 📌 訊息內容<b>在呼叫端的執行緒上就組好了</b>（前綴、顏色與 <c>Config.ChatChannel</c>
        /// 都在進佇列之前讀完），排隊的只是「送出」這個動作。
        /// </para>
        /// </remarks>
        public static void PrintMessage(SeString message)
        {
            var sb = new SeStringBuilder()
                .AddUiForeground(60)
                .AddText($"[{Plugin.Name}] ")
                .AddUiForegroundOff()
                .Append(message);

            PendingChatLines.Enqueue(new XivChatEntry()
            {
                Type = Config.ChatChannel,
                Message = sb.BuiltString
            });

            _ = DalamudApi.Framework.RunOnFrameworkThread(static () =>
            {
                while (PendingChatLines.TryDequeue(out var entry))
                    DalamudApi.ChatGui.Print(entry);
            });
        }

        private void OnLogin()
        {
            DalamudApi.Framework.RunOnTick(UpdateDatacenterId);
        }
        
        private void UpdateDatacenterId()
        {
            if (DalamudApi.ObjectTable.LocalPlayer is null || !DalamudApi.ClientState.IsLoggedIn) return;
            CurrentDatacenterId = Utils.DatacenterToEurekaDatacenterId(DalamudApi.ObjectTable.LocalPlayer.CurrentWorld.Value.DataCenter.Value.Name.ExtractText());
        }
        
        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            DalamudApi.Dispose();
            FateManager.Dispose();
            ZoneManager.Dispose();
            ElementalManager.Dispose();
            InventoryManager.Dispose();
            AlarmManager.Dispose();
            TreasureHuntManager.Dispose();
            SplatoonManager?.Dispose();
            HazardManager?.Dispose();
            CookieBoxTracker?.Dispose();
            PluginWindow.DisposeAllConnections();
            DalamudApi.ChatGui.RemoveChatLinkHandler();
            DalamudApi.ClientState.Login -= OnLogin;
        }
    }