using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using EurekaHelper.Windows;
using EurekaHelper.XIV;

namespace EurekaHelper.System
{
    public class FateManager : IDisposable
    {
        private readonly EurekaHelper _plugin = null!;
        private List<IFate> lastFates = new();
        private IEurekaTracker EurekaTracker;

        // Fate IDs that have already had an auto-pop time attempt sent this zone visit - guards
        // AutoPopTimeForAlreadyActiveFates below against resending every tick while the "set
        // once" async call is in flight.
        private readonly HashSet<ushort> _autoPopAttempted = new();

        // Ovni (1424) and Tristitia (1422) have no real "next possible spawn" prediction and no
        // reliable way to observe their actual completion time - so instead of trying to detect
        // the real end, the assumption is: this FATE typically runs for about 15 minutes once it
        // starts, so treat (discovery time + 15 min) as its assumed "death" moment, then apply the
        // normal 30-minute cooldown on top of that. If a "被...打倒了" chat line later confirms
        // the NM was actually killed by players, that's a strictly better anchor than the assumed
        // one - switch to the real kill time (still +30 min cooldown) instead.
        private static readonly HashSet<ushort> AssumedDurationFateIds = new() { 1424, 1422 };
        private static readonly TimeSpan AssumedEventDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan SpecialFateRespawnDuration = TimeSpan.FromMinutes(30);

        // Matches e.g. "未確認飛行物體被<player>打倒了" - the system chat line for an enemy
        // actually being defeated, as opposed to the FATE simply completing/timing out (which
        // produces different chat text entirely, see the "目前進度 100%" progress line instead).
        private static readonly Regex KillConfirmationRegex = new(@"^(?<boss>.+?)被.+打倒了", RegexOptions.Compiled);

        public FateManager(EurekaHelper plugin)
        {
            _plugin = plugin;
            DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
            DalamudApi.ChatGui.ChatMessage += OnChatMessage;

            if (Utils.IsPlayerInEurekaZone(DalamudApi.ClientState.TerritoryType))
            {
                EurekaTracker = Utils.GetEurekaTracker(DalamudApi.ClientState.TerritoryType);
                DalamudApi.Framework.Update += OnUpdate;
            }
        }

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (EurekaTracker == null)
                return;

            var match = KillConfirmationRegex.Match(message.TextValue);
            if (!match.Success)
                return;

            var bossText = match.Groups["boss"].Value;
            foreach (var fateId in AssumedDurationFateIds)
            {
                var fate = EurekaTracker.GetFates().SingleOrDefault(f => f.FateId == fateId);
                if (fate == null)
                    continue;

                Loc.TryEurekaName(fate.BossName, out var localizedName);
                if (localizedName != bossText)
                    continue;

                // Real, precisely-timed kill confirmation beats the assumed-duration guess used
                // at discovery time - record it now (silently, since discovery already notified)
                // with the same 30-minute cooldown, anchored to this actual moment instead.
                fate.SetRespawnDuration(SpecialFateRespawnDuration);
                DisplayFatePop(fate, DateTimeOffset.Now.ToUnixTimeMilliseconds(), notify: false);
            }
        }

        private void OnTerritoryChanged(ushort territoryId)
        {
            if (Utils.IsPlayerInEurekaZone(territoryId))
            {
                // Auto Create Tracker is handled by ZoneManager.HandleZoneEntry instead of here -
                // that hook (InitZoneDetour) is what actually reads the server ID for this zone
                // entry, so triggering the auto-create from there avoids a race against this
                // (TerritoryChanged) event where ZoneManager.GetLastServerId(zoneIndex) could still
                // be returning last visit's (or zero) server ID, silently corrupting
                // Configuration.TrackerMemory with the wrong ID for future reload-rejoin matching.

                EurekaTracker = Utils.GetEurekaTracker(territoryId);
                _autoPopAttempted.Clear();
                DalamudApi.Framework.Update += OnUpdate;
            }
            else
            {
                DalamudApi.Framework.Update -= OnUpdate;
            }
        }

        private void OnUpdate(IFramework framework)
        {
            if (EurekaHelper.Config.DisplayFateProgress)
            {
                var instanceFates = DalamudApi.FateTable.Where(x => !Utils.IsBunnyFate(x.FateId)).ToList();
                foreach (var fate in instanceFates)
                {
                    EurekaFate eurekaFate = EurekaTracker.GetFates().SingleOrDefault(i => fate.FateId == i.FateId);
                    if (eurekaFate is null || eurekaFate.FateProgress == fate.Progress)
                        continue;

                    if (fate.Progress % 25 == 0)
                    {
                        eurekaFate.FateProgress = fate.Progress;
                        Loc.TryEurekaName(eurekaFate.BossName, out var bossName);
                        var sb = new SeStringBuilder()
                            .AddText($"{bossName}: ")
                            .Append(Utils.MapLink(eurekaFate.TerritoryId, eurekaFate.MapId, eurekaFate.FatePosition))
                            .AddText($" {Loc.Text("is at")} ")
                            .AddUiForeground(58)
                            .AddText($"{eurekaFate.FateProgress}%")
                            .AddUiForegroundOff();

                        EurekaHelper.PrintMessage(sb.BuiltString);
                    }
                }
            }

            AutoPopTimeForAlreadyActiveFates();

            if (DalamudApi.FateTable.SequenceEqual(lastFates))
                return;

            var currFates = DalamudApi.FateTable.Except(lastFates).ToList();
            var newFates = EurekaTracker.GetFates().Where(i => currFates.Select(c => c.FateId).Contains(i.FateId)).ToList();

            foreach (var fate in newFates)
            {
                if (AssumedDurationFateIds.Contains(fate.FateId))
                    HandleSpecialFateDiscovery(fate, notify: true);
                else
                    DisplayFatePop(fate);
            }

            lastFates = DalamudApi.FateTable.ToList();
        }

        // See AssumedDurationFateIds - anchors the cooldown to (discovery + AssumedEventDuration)
        // rather than the discovery moment itself, since these two FATEs typically keep running
        // for a while after they start. OnChatMessage will later overwrite this with the real
        // kill time if a "被...打倒了" confirmation line shows up before that assumed point.
        private static void HandleSpecialFateDiscovery(EurekaFate fate, bool notify)
        {
            fate.SetRespawnDuration(SpecialFateRespawnDuration);
            var assumedKillTimestamp = DateTimeOffset.Now.Add(AssumedEventDuration).ToUnixTimeMilliseconds();
            DisplayFatePop(fate, assumedKillTimestamp, notify);
        }

        // DisplayFatePop's AutoPopFate logic only fires off the newFates diff, which is a one-shot
        // edge trigger against lastFates - an NM that was ALREADY active the moment you zoned in
        // (someone else triggered it before you arrived) is caught by that diff too (lastFates
        // starts empty on zone entry), but only if the tracker connection has already finished
        // (re)connecting by that exact tick. Since zone entry now kicks off an async tracker
        // reconnect/rebuild (see ZoneManager.HandleZoneEntry) that can still be in flight when
        // this first runs, the connection-not-ready check in DisplayFatePop silently drops the
        // pop-time write and the fate is never flagged as "new" again afterwards. This runs every
        // tick (idempotent via _autoPopAttempted) so it catches the fate on whichever tick the
        // connection actually becomes ready, rather than depending on winning that race.
        private void AutoPopTimeForAlreadyActiveFates()
        {
            if (!EurekaHelper.Config.AutoPopFate)
                return;

            var connection = PluginWindow.GetConnection();
            if (!connection.IsConnected() || !connection.CanModify())
                return;

            var activeFateIds = new HashSet<ushort>(DalamudApi.FateTable.Where(x => !Utils.IsBunnyFate(x.FateId)).Select(x => x.FateId));
            var trackerFates = connection.GetTracker().GetFates();

            foreach (var eurekaFate in EurekaTracker.GetFates())
            {
                if (!activeFateIds.Contains(eurekaFate.FateId) || _autoPopAttempted.Contains(eurekaFate.FateId))
                    continue;

                var trackerFate = trackerFates.Find(x => x.IncludeInTracker && x.FateId == eurekaFate.FateId);
                if (trackerFate is null)
                    continue;

                if (trackerFate.IsPopped() && !(EurekaHelper.Config.AutoPopFateWithinRange && trackerFate.IsRespawnTimeWithinRange(TimeSpan.FromMinutes(5))))
                    continue;

                _autoPopAttempted.Add(eurekaFate.FateId);

                if (AssumedDurationFateIds.Contains(eurekaFate.FateId))
                {
                    HandleSpecialFateDiscovery(eurekaFate, notify: false);
                    continue;
                }

                _ = Task.Run(async () => await connection.SetPopTime((ushort)eurekaFate.TrackerId, DateTimeOffset.Now.ToUnixTimeMilliseconds()));
            }
        }

        // popTimestamp overrides what gets written as the pop time on the shared tracker (defaults
        // to now) - used by Ovni/Tristitia's virtual/confirmed-kill handling below, where the
        // "true" pop moment isn't simply "whenever we noticed". notify controls the toast/chat/
        // sound entirely, for the same two fates' silent kill-confirmation correction.
        public static void DisplayFatePop(EurekaFate fate, long? popTimestamp = null, bool notify = true)
        {
            Loc.TryEurekaName(fate.BossName, out var bossName);
            var sb = new SeStringBuilder()
                .AddText($"{bossName}: ")
                .Append(Utils.MapLink(fate.TerritoryId, fate.MapId, fate.FatePosition));

            var timestamp = popTimestamp ?? DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (!fate.IsBunnyFate)
            {
                if (notify && EurekaHelper.Config.DisplayToastPop)
                    DalamudApi.ToastGui.ShowQuest(sb.BuiltString);

                if (notify && EurekaHelper.Config.PlayPopSound)
                    SoundManager.PlayNMSoundEffect();

                if (notify && EurekaHelper.Config.DisplayFatePop)
                {
                    DalamudApi.PluginInterface.RemoveChatLinkHandler(fate.FateId);
                    if (EurekaHelper.Config.PayloadOptions != PayloadOptions.Nothing)
                    {
                        DalamudLinkPayload payload = DalamudApi.PluginInterface.AddChatLinkHandler(fate.FateId, (i, m) =>
                        {
                            Utils.SetFlagMarker(fate, randomizeCoords: EurekaHelper.Config.RandomizeMapCoords);

                            switch (EurekaHelper.Config.PayloadOptions)
                            {
                                case PayloadOptions.CopyToClipboard:
                                    Utils.CopyToClipboard(Utils.RandomFormattedText(fate));
                                    break;

                                default:
                                case PayloadOptions.ShoutToChat:
                                    Utils.SendMessage(Utils.RandomFormattedText(fate));
                                    break;
                            }
                        });

                        var text = EurekaHelper.Config.PayloadOptions switch
                        {
                            PayloadOptions.ShoutToChat => Loc.Text("shout"),
                            PayloadOptions.CopyToClipboard => Loc.Text("copy"),
                            _ => Loc.Text("shout")
                        };

                        sb.AddText(" ");
                        sb.AddUiForeground(32);
                        sb.Add(payload);
                        sb.AddText(Loc.Format("[Click to {0}]", text));
                        sb.Add(RawPayload.LinkTerminator);
                        sb.AddUiForegroundOff();
                    }

                    EurekaHelper.PrintMessage(sb.BuiltString);
                }

                if (EurekaHelper.Config.AutoPopFate)
                {
                    if (PluginWindow.GetConnection().IsConnected() && PluginWindow.GetConnection().CanModify())
                    {
                        var trackerFate = PluginWindow.GetConnection().GetTracker().GetFates().Find(x => x.IncludeInTracker && x.FateId == fate.FateId);

                        if (trackerFate is null)
                            return;

                        if (!trackerFate.IsPopped() || (EurekaHelper.Config.AutoPopFateWithinRange && trackerFate.IsRespawnTimeWithinRange(TimeSpan.FromMinutes(5))))
                        {
                            _ = Task.Run(async () =>
                            {
                                await PluginWindow.GetConnection().SetPopTime((ushort)fate.TrackerId, timestamp);
                            });
                        }
                    }
                }
            }
            else
            {
                if (EurekaHelper.Config.DisplayBunnyFates)
                {
                    EurekaHelper.PrintMessage(sb.BuiltString);
                    SoundManager.PlayBunnySoundEffect();
                }
            }
        }

        public void Dispose()
        {
            DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
            DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
            DalamudApi.Framework.Update -= OnUpdate;
        }
    }
}
