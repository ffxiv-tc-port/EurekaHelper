using Dalamud.Game.Gui.Dtr;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using System;

namespace EurekaHelper.System
{
    public class ZoneManager
    {
        private delegate nint InitZoneDelegate(nint a1, int a2, nint a3);
        private readonly IDtrBarEntry _dtrBarEntry;
        private readonly IDtrBarEntry _dtrToggleEntry;

        public ZoneManager()
        {
            DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
            InitZoneHook?.Enable();

            var dtrBarTitle = "Eureka Helper";
            try
            {
                _dtrBarEntry = DalamudApi.DtrBar.Get(dtrBarTitle);
            }
            catch (ArgumentException ex)
            {
                for (var i = 0; i < 5; i++)
                {
                    DalamudApi.Log.Error(ex, $"Failed to acquire DtrBarEntry {dtrBarTitle}, trying {dtrBarTitle}{i}");
                    try
                    {
                        _dtrBarEntry = DalamudApi.DtrBar.Get($"{dtrBarTitle}{i}");
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    break;
                }
            }

            // 典籍: a standalone DTR entry that just opens/closes the main plugin window on
            // click, so the window is reachable without a slash command or the Dalamud plugin
            // installer's config-gear button.
            var dtrToggleTitle = "Eureka Helper 典籍";
            try
            {
                _dtrToggleEntry = DalamudApi.DtrBar.Get(dtrToggleTitle);
            }
            catch (ArgumentException ex)
            {
                for (var i = 0; i < 5; i++)
                {
                    DalamudApi.Log.Error(ex, $"Failed to acquire DtrBarEntry {dtrToggleTitle}, trying {dtrToggleTitle}{i}");
                    try
                    {
                        _dtrToggleEntry = DalamudApi.DtrBar.Get($"{dtrToggleTitle}{i}");
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    break;
                }
            }

            if (_dtrToggleEntry != null)
            {
                _dtrToggleEntry.Text = "典籍";
                _dtrToggleEntry.Tooltip = "開啟 Eureka Helper";
                _dtrToggleEntry.OnClick = () => EurekaHelper.Plugin.PluginWindow.IsOpen ^= true;
                _dtrToggleEntry.Shown = true;
            }
        }

        [Signature("E8 ?? ?? ?? ?? 45 33 C0 48 8D ?? ?? 8B ?? E8 ?? ?? ?? ?? 48 8D ??", DetourName = nameof(InitZoneDetour))]
        private readonly Hook<InitZoneDelegate> InitZoneHook = null!;

        private nint InitZoneDetour(nint a1, int a2, nint a3)
        {
            try
            {
                ushort serverId = MemoryHelper.Read<ushort>(a3);
                ushort zoneId = MemoryHelper.Read<ushort>(a3 + 2);
                var zoneName = Utils.GetZoneName(zoneId);

                if (zoneName != null)
                {
                    if (EurekaHelper.Config.DisplayServerId)
                        EurekaHelper.PrintMessage(Loc.Format("{0} Server ID: {1}", zoneName, serverId));

                    if (EurekaHelper.Config.DisplayServerIdInServerInfo)
                    {
                        if (_dtrBarEntry != null)
                        {
                            _dtrBarEntry.Text = Loc.Format("Server ID: {0}", serverId);
                            _dtrBarEntry.Shown = true;
                        }
                    }
                }
                else
                {
                    if (_dtrBarEntry != null)
                    {
                        _dtrBarEntry.Text = "";
                        _dtrBarEntry.Shown = false;
                    }
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(Loc.Format("Something went wrong. Please contact the author.\n{0}", ex.Message));
            }

            return InitZoneHook.Original(a1, a2, a3);
        }

        public void Dispose()
        {
            InitZoneHook?.Dispose();
            _dtrBarEntry?.Remove();
            _dtrToggleEntry?.Remove();
        }
    }
}
