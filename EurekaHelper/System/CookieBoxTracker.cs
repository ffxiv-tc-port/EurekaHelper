using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EurekaHelper.XIV;

namespace EurekaHelper.System
{
    // Read-only integration with a third-party community tracker
    // (https://github.com/CooKieBox0501/Eureka-Tracker), whose /eureka/state node in its Firebase
    // Realtime Database is readable without authentication despite the site itself requiring
    // Discord login. Feeds its NM pop times into our own zone trackers to keep them as accurate
    // as possible even when we're not the ones reporting kills.
    //
    // Deliberately uses a single persistent Server-Sent-Events connection (Firebase's REST
    // streaming protocol) instead of polling - per explicit instruction not to hit the API
    // repeatedly, this opens one connection and stays on it, reconnecting only on drop.
    //
    // Only feeds known, mapped short keys (see BossNameByShortKey) into EurekaFate.SetKill - this
    // now covers every zone's full NM roster (both the named "lord"/"bao" keys and the
    // "o_<zone>_<level>_<hash>" keys used for the rest), reverse-engineered from the site's own
    // source. Anything not in that map (Bunny Fates, Ovni/Tristitia) is intentionally skipped.
    public class CookieBoxTracker : IDisposable
    {
        private const string BaseUrl = "https://eureka-tracker-64cc3-default-rtdb.asia-southeast1.firebasedatabase.app";
        private const string StreamPath = "/eureka/state.json";

        // Separate top-level Firebase path (sibling of /eureka/state, not nested under it) used
        // for the "即將可觸發" precondition-grinding tracker on the site - confirmed by reading
        // the site's own index.html source (github.com/CooKieBox0501/Eureka-Tracker): schema is
        // eureka/triggering/<nmId>/<discordId> = <timestamp>. Needs its own persistent SSE
        // connection since it lives outside the /eureka/state subtree our other stream watches.
        private const string TriggeringStreamPath = "/eureka/triggering.json";

        // Baldesion Arsenal subtree - sibling of /eureka/state, holds live tower-occupancy
        // reports, the two BA-only NMs' real spawnedAt/killedAt/isNatural (jellyfish = Ovni,
        // desk = Tristitia - neither has a NM_DATA entry so they're absent from
        // BossNameByShortKey/spawns above), and the community host/organizer schedule. Confirmed
        // against the site's own source (index.html, getBaRef()).
        private const string BaStreamPath = "/eureka/ba.json";

        private static readonly Dictionary<string, string> BaBossNameByEncounterKey = new(StringComparer.OrdinalIgnoreCase)
        {
            ["jellyfish"] = "Ovni",
            ["desk"] = "Tristitia",
        };

        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(15);

        // How old a spawn report's timestamp can be before it's treated as a backfilled record
        // (recorded silently) rather than a live discovery (which notifies).
        private static readonly TimeSpan BackfillThreshold = TimeSpan.FromMinutes(5);

        // A remote report within this tolerance of what we already have (earlier OR later) is
        // treated as "the same spawn, just observed with slightly different precision" and
        // ignored - our own local timestamp (however it was obtained) stays authoritative.
        // Anything outside the window - whether the remote report is earlier or later - is
        // treated as genuinely new/corrected information and overwrites the local value.
        private static readonly TimeSpan ReconcileTolerance = TimeSpan.FromMinutes(5);

        // Best-effort short-key -> EurekaFate.BossName map. The "lord"/"bao" category entries
        // (named short keys like "arthro") were reverse-engineered from observed
        // /eureka/state/history entries; the "o_<zone>_<level>_<hash>" entries were instead
        // extracted directly from the site's own NM_DATA table (github.com/CooKieBox0501/
        // Eureka-Tracker index.html) and matched to our EurekaFate entries by FateLevel, which
        // lines up 1:1 with NM_DATA's "level" field per zone (confirmed against every "lord"/"bao"
        // entry's level too, e.g. arthro=29, cassie/louhi=35). Ovni/Tristitia have no NM_DATA
        // entry (not a trackable "NM" on the site) and Bunny Fates aren't tracked there either.
        private static readonly Dictionary<string, string> BossNameByShortKey = new(StringComparer.OrdinalIgnoreCase)
        {
            // "lord" category
            ["pazuzu"] = "Pazuzu",
            ["louhi"] = "Louhi",
            ["penthesilea"] = "Penthesilea",
            ["pw"] = "Provenance Watcher",
            ["provenancewatcher"] = "Provenance Watcher", // best-guess alias, not directly observed

            // "bao" (八寶) category
            ["arthro"] = "King Arthro",
            ["cassie"] = "Copycat Cassie",
            ["kc"] = "Copycat Cassie", // best-guess alias, not directly observed
            ["lamebrix"] = "Lamebrix Strikebocks",
            ["yinyang"] = "Ying-Yang",
            ["skoll"] = "Skoll",
            ["molech"] = "Molech",
            ["goldemar"] = "King Goldemar",
            ["ceto"] = "Ceto",

            // "other" category (regular field NMs) - Anemos
            ["o_anemos_1_2959"] = "Sabotender Corrido",
            ["o_anemos_2_2093"] = "The Lord of Anemos",
            ["o_anemos_3_5872"] = "Teles",
            ["o_anemos_4_6031"] = "The Emperor of Anemos",
            ["o_anemos_5_3368"] = "Callisto",
            ["o_anemos_6_5722"] = "Number",
            ["o_anemos_7_2923"] = "Jahannam",
            ["o_anemos_8_2668"] = "Amemet",
            ["o_anemos_9_6986"] = "Caym",
            ["o_anemos_10_7975"] = "Bombadeel",
            ["o_anemos_11_5228"] = "Serket",
            ["o_anemos_12_7277"] = "Judgemental Julika",
            ["o_anemos_13_4187"] = "The White Rider",
            ["o_anemos_14_942"] = "Polyphemus",
            ["o_anemos_15_4295"] = "Simurgh's Strider",
            ["o_anemos_16_5231"] = "King Hazmat",
            ["o_anemos_17_4403"] = "Fafnir",
            ["o_anemos_18_9636"] = "Amarok",
            ["o_anemos_19_8804"] = "Lamashtu",

            // "other" category - Pagos
            ["o_pagos_20_4005"] = "The Snow Queen",
            ["o_pagos_21_8448"] = "Taxim",
            ["o_pagos_22_5316"] = "Ash Dragon",
            ["o_pagos_23_7297"] = "Glavoid",
            ["o_pagos_24_9984"] = "Anapos",
            ["o_pagos_25_175"] = "Hakutaku",
            ["o_pagos_26_8076"] = "King Igloo",
            ["o_pagos_27_5784"] = "Asag",
            ["o_pagos_28_9276"] = "Surabhi",
            ["o_pagos_30_3536"] = "Mindertaur/Eldertaur",
            ["o_pagos_31_6136"] = "Holy Cow",
            ["o_pagos_32_570"] = "Hadhayosh",
            ["o_pagos_33_4840"] = "Horus",
            ["o_pagos_34_3437"] = "Arch Angra Mainyu",

            // "other" category - Pyros
            ["o_pyros_35_7030"] = "Leucosia",
            ["o_pyros_36_3335"] = "Flauros",
            ["o_pyros_37_989"] = "The Sophist",
            ["o_pyros_38_1244"] = "Graffiacane",
            ["o_pyros_39_2267"] = "Askalaphos",
            ["o_pyros_40_6992"] = "Grand Duke Batym",
            ["o_pyros_41_2613"] = "Aetolus",
            ["o_pyros_42_9107"] = "Lesath",
            ["o_pyros_43_853"] = "Eldthurs",
            ["o_pyros_44_8032"] = "Iris",
            ["o_pyros_46_1074"] = "Dux",
            ["o_pyros_47_1030"] = "Lumber Jack",
            ["o_pyros_48_1869"] = "Glaukopis",

            // "other" category - Hydatos
            ["o_hydatos_50_8658"] = "Khalamari",
            ["o_hydatos_51_7072"] = "Stegodon",
            ["o_hydatos_53_2207"] = "Piasa",
            ["o_hydatos_54_5861"] = "Frostmane",
            ["o_hydatos_55_2653"] = "Daphne",
            ["o_hydatos_57_4205"] = "Leuke",
            ["o_hydatos_58_4972"] = "Barong",
        };

        private static readonly Dictionary<string, int> ZoneIndexByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["anemos"] = 1,
            ["pagos"] = 2,
            ["pyros"] = 3,
            ["hydatos"] = 4,
        };

        private static readonly Regex ResetKeyPattern = new(@"^h_reset_(\w+)_\d+$", RegexOptions.Compiled);

        private readonly HttpClient _httpClient = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly HashSet<string> _seenHistoryKeys = new();
        private readonly Task _listenTask;
        private readonly Task _triggeringListenTask;
        private readonly Task _baListenTask;

        // Raw merged state for each BA subtree node - kept as JObject (rather than parsed
        // structs) because Firebase "patch" events can update a single field at a time (e.g.
        // "jellyfish/killedAt") and merging into the existing object is simpler than tracking
        // partial-struct updates. Read/written only under _baLock.
        private readonly object _baLock = new();
        private JObject _towerRaw = new();
        private JObject _jellyfishRaw = new();
        private JObject _deskRaw = new();
        private readonly Dictionary<string, JObject> _scheduleByPushId = new();
        private bool _baFirstEventProcessed;

        // Precondition-triggering mirror: nmId -> set of Discord user IDs currently marked as
        // grinding toward that NM's spawn. Only a count is surfaced to the UI (see
        // GetTriggeringCount) - individual Discord IDs aren't resolved to display names here.
        private readonly Dictionary<string, HashSet<string>> _triggeringByNmId = new();
        private readonly object _triggeringLock = new();

        // Currently-spawned NMs that someone has reported and is actively on a pull timer for
        // (/eureka/state/activeEvents/evt_<nmId>_<spawnedAt>), keyed by the raw event key so
        // multiple concurrent reports of the same boss (e.g. stale duplicate events) are tracked
        // separately - the UI only needs a count and the tooltip lists the individual reporters.
        // Cleared when the site removes the event node (kill/cancel reported) - see ProcessNode.
        private readonly Dictionary<string, ActiveTrigger> _activeTriggersByEventKey = new();
        private readonly object _activeTriggersLock = new();

        public readonly struct ActiveTrigger
        {
            public string BossName { get; init; }
            public string ReporterName { get; init; }
            public int PullSecondsLeft { get; init; }
        }

        // Whether we've processed at least one event since the CURRENT connection was
        // established. Firebase always sends a full "put" at "/" as the first event of every new
        // SSE connection, containing the entire existing state (including old spawns/history from
        // long before we connected) - that one must be applied silently. Reset per-reconnect (see
        // ConnectAndListen), not just once ever, so a dropped-and-restored connection doesn't
        // treat its fresh snapshot as "old news" and stay silent forever.
        private bool _firstEventProcessed;

        public CookieBoxTracker()
        {
            _listenTask = Task.Run(() => ListenLoop(_cts.Token, StreamPath, HandleEvent, () => _firstEventProcessed = false));
            _triggeringListenTask = Task.Run(() => ListenLoop(_cts.Token, TriggeringStreamPath, HandleTriggeringEvent, null));
            _baListenTask = Task.Run(() => ListenLoop(_cts.Token, BaStreamPath, HandleBaEvent, () => _baFirstEventProcessed = false));
        }

        // Tower occupancy as last reported on the site's 塔內狀況 panel. HasPeople is null when
        // no report has ever been received (site's "狀態未知").
        public (bool? HasPeople, long At, string ReportedBy) GetBaTowerStatus()
        {
            lock (_baLock)
            {
                if (_towerRaw.Count == 0)
                    return (null, 0, null);

                return ((bool?)_towerRaw["hasPeople"], (long?)_towerRaw["at"] ?? 0, (string)_towerRaw["by"]);
            }
        }

        // Ovni's (未確認飛行物體) live spawnedAt/killedAt/isNatural/pull-timer state, straight
        // from the community tracker's own reports - the same fields FateManager otherwise has to
        // *guess* locally (see AssumedDurationFateIds). KilledAt/PullTargetMs are 0 when absent.
        public (long SpawnedAt, long KilledAt, bool IsNatural, long PullTargetMs) GetBaJellyfishState()
        {
            lock (_baLock) return ExtractEncounterState(_jellyfishRaw);
        }

        // Tristitia's (兵武塔調查支援) equivalent of GetBaJellyfishState.
        public (long SpawnedAt, long KilledAt, bool IsNatural, long PullTargetMs) GetBaDeskState()
        {
            lock (_baLock) return ExtractEncounterState(_deskRaw);
        }

        private static (long SpawnedAt, long KilledAt, bool IsNatural, long PullTargetMs) ExtractEncounterState(JObject raw) => (
            (long?)raw["spawnedAt"] ?? 0,
            (long?)raw["killedAt"] ?? 0,
            (bool?)raw["isNatural"] ?? false,
            (long?)raw["etPullTargetMs"] ?? (long?)raw["pullTargetMs"] ?? 0);

        // Upcoming (not-yet-ended) 主催排班表 entries, soonest first.
        public List<(string SponsorName, long StartAt, long EndAt, string Note, string FinalStatus)> GetBaUpcomingSchedule(int maxCount)
        {
            lock (_baLock)
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return _scheduleByPushId.Values
                    .Select(o => (
                        SponsorName: (string)o["sponsorName"] ?? "?",
                        StartAt: (long?)o["startAt"] ?? 0,
                        EndAt: (long?)o["endAt"] ?? 0,
                        Note: (string)o["note"] ?? string.Empty,
                        FinalStatus: (string)o["finalStatus"] ?? string.Empty))
                    .Where(x => x.EndAt == 0 || x.EndAt >= nowMs)
                    .OrderBy(x => x.StartAt)
                    .Take(maxCount)
                    .ToList();
            }
        }

        // How many people are currently marked as grinding the precondition kills toward this
        // boss's spawn (the "即將可觸發" panel's "觸發中N人" on the site), summed across every
        // short-key alias that maps to this BossName.
        public int GetTriggeringCount(string bossName)
        {
            lock (_triggeringLock)
            {
                var total = 0;
                foreach (var (nmId, uids) in _triggeringByNmId)
                {
                    if (BossNameByShortKey.TryGetValue(nmId, out var mappedName) && mappedName == bossName)
                        total += uids.Count;
                }
                return total;
            }
        }

        // Looked up by the tracker UI (per fate row, via fate.BossName) to show how many people
        // currently have an active pull timer running on that NM (and, in the tooltip, who).
        public List<ActiveTrigger> GetActiveTriggers(string bossName)
        {
            lock (_activeTriggersLock)
            {
                var result = new List<ActiveTrigger>();
                foreach (var trigger in _activeTriggersByEventKey.Values)
                {
                    if (trigger.BossName == bossName)
                        result.Add(trigger);
                }
                return result;
            }
        }

        // On-demand catch-up: does a single plain GET of the current /spawns snapshot (not the
        // persistent SSE stream) and applies any entries reported within the last `window` -
        // for backfilling pops that happened while disconnected, without waiting for a fresh
        // "put"/"patch" to arrive naturally. Applied silently (no toast/chat notification) since
        // this is a manual bulk resync, not a live discovery.
        public async Task<int> ResyncRecentSpawnsAsync(TimeSpan window)
        {
            var json = await _httpClient.GetStringAsync(BaseUrl + "/eureka/state/spawns.json", _cts.Token);
            var spawns = JObject.Parse(json);

            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)window.TotalMilliseconds;
            var applied = 0;

            foreach (var prop in spawns.Properties())
            {
                if (prop.Value.Type != JTokenType.Integer)
                    continue;

                var timestamp = (long)prop.Value;
                if (timestamp < cutoff)
                    continue;

                ApplySpawn(prop.Name, timestamp, notify: false, skipCurrentZone: false);
                applied++;
            }

            DalamudApi.Log.Information($"[CookieBoxTracker] Manual resync: found {applied} spawn(s) within the last {window.TotalHours:0.#}h");
            return applied;
        }

        private async Task ListenLoop(CancellationToken token, string path, Action<string, string> onEvent, Action onReconnect)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    onReconnect?.Invoke();
                    await ConnectAndListen(token, path, onEvent);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DalamudApi.Log.Warning($"[CookieBoxTracker] Stream disconnected ({path}), retrying in {ReconnectDelay.TotalSeconds}s: {ex.Message}");
                }

                try { await Task.Delay(ReconnectDelay, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ConnectAndListen(CancellationToken token, string path, Action<string, string> onEvent)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Add("Accept", "text/event-stream");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            DalamudApi.Log.Information($"[CookieBoxTracker] Connected to community tracker stream ({path})");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);

            string eventName = null;
            while (!token.IsCancellationRequested)
            {
                // Must pass token here, not just check it in the loop condition: ReadLineAsync()
                // with no token awaits the next SSE line with no way to abort mid-read, so on a
                // long-lived push stream, cancelling _cts didn't unblock this at all - Dispose()'s
                // .Wait(2s) was hitting its full timeout on every listen task instead of returning
                // immediately.
                var line = await reader.ReadLineAsync(token);
                if (line == null)
                    break; // stream closed by server

                if (line.StartsWith("event: "))
                    eventName = line["event: ".Length..];
                else if (line.StartsWith("data: "))
                    onEvent(eventName, line["data: ".Length..]);
            }

            DalamudApi.Log.Information($"[CookieBoxTracker] Stream closed by server ({path})");
        }

        private void HandleEvent(string eventName, string json)
        {
            DalamudApi.Log.Verbose($"[CookieBoxTracker] event: {eventName}, data: {json}");

            if (eventName != "put" && eventName != "patch")
                return;

            try
            {
                var payload = JObject.Parse(json);
                var path = (string)payload["path"];
                var data = payload["data"];

                if (path == null)
                    return;

                var isInitial = !_firstEventProcessed;
                _firstEventProcessed = true;

                var baseSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                ProcessNode(baseSegments, data, isInitial);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Warning($"[CookieBoxTracker] Failed to process event: {ex.Message}");
            }
        }

        // Firebase's REST stream doesn't consistently nest sub-paths as a JSON object graph - a
        // "patch" at "/" was observed carrying its data as a FLAT object whose own keys are
        // themselves relative paths containing "/" (e.g. {"spawns/penthesilea": 172839...,
        // "activeEvents/evt_x": {...}}), not {"spawns": {"penthesilea": ...}}. This walks either
        // shape uniformly: each JObject property's name is itself re-split on "/" and appended to
        // the running path, so by the time a leaf (non-object) value is reached, baseSegments is
        // always the fully resolved absolute path regardless of how it was nested/flattened.
        private void ProcessNode(string[] baseSegments, JToken node, bool isInitial)
        {
            // Must be checked before the general null early-return below, otherwise an
            // activeEvents/<evtKey> node being nulled out (kill/cancel reported) would be silently
            // dropped and the "someone is currently pulling this NM" state would never clear.
            if (baseSegments.Length >= 2 && baseSegments[0] == "activeEvents" &&
                (node == null || node.Type == JTokenType.Null))
            {
                RemoveActiveEvent(baseSegments[1]);
                return;
            }

            if (node == null || node.Type == JTokenType.Null)
                return;

            // A whole activeEvents/<evtKey> record (a pull timer being started, or the full
            // snapshot on connect) arrives as a nested object here - handle it as a unit instead
            // of recursing into its individual fields (nmId/pullSecondsLeft/reportedBy/...).
            if (baseSegments.Length == 2 && baseSegments[0] == "activeEvents" && node is JObject evtObj)
            {
                ApplyActiveEvent(baseSegments[1], evtObj);
                return;
            }

            if (node is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    var childSegments = baseSegments.Concat(prop.Name.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToArray();
                    ProcessNode(childSegments, prop.Value, isInitial);
                }
                return;
            }

            // Leaf value - baseSegments is now the fully resolved path, e.g. ["spawns","arthro"]
            // or ["history","h_reset_hydatos_..."]. Anything else (otherReporters, lastUpdated,
            // ...) is intentionally ignored - we only care about these subtrees.
            if (baseSegments.Length == 2 && baseSegments[0] == "spawns" && node.Type == JTokenType.Integer)
                ApplySpawn(baseSegments[1], (long)node, notify: !isInitial);
            else if (baseSegments.Length == 2 && baseSegments[0] == "history")
                ApplyHistoryKey(baseSegments[1], isInitial);
            else if (baseSegments.Length == 2 && baseSegments[0] == "newIslandMark" && node.Type == JTokenType.Integer)
                ApplyNewIslandMark(baseSegments[1], isInitial);
        }

        private void ApplyActiveEvent(string eventKey, JObject evt)
        {
            var nmId = (string)evt["nmId"];
            if (nmId == null || !BossNameByShortKey.TryGetValue(nmId, out var bossName))
                return;

            var reporterName = (string)evt["reportedBy"]?["name"] ?? "?";
            var pullSecondsLeft = (int?)evt["pullSecondsLeft"] ?? 0;

            lock (_activeTriggersLock)
            {
                _activeTriggersByEventKey[eventKey] = new ActiveTrigger
                {
                    BossName = bossName,
                    ReporterName = reporterName,
                    PullSecondsLeft = pullSecondsLeft,
                };
            }
        }

        private void RemoveActiveEvent(string eventKey)
        {
            lock (_activeTriggersLock)
                _activeTriggersByEventKey.Remove(eventKey);
        }

        private void ApplySpawn(string shortKey, long timestamp, bool notify, bool skipCurrentZone = true)
        {
            if (!BossNameByShortKey.TryGetValue(shortKey, out var bossName))
                return;

            for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            {
                // While physically standing in this zone, our own in-game detection (FateManager)
                // already recorded whatever we personally witnessed there, and that's more
                // trustworthy than a third party's report - a live external event overwriting it
                // (even just to update the timestamp, without notifying) risks clobbering the
                // precise time we actually saw with a less precise/delayed one from someone else.
                // Only skipped for the manual "sync recent pops" resync (skipCurrentZone: false),
                // which is a deliberate catch-up action, not a live event that could race with
                // something we just witnessed ourselves.
                if (skipCurrentZone && zoneIndex == ZoneManager.CurrentZoneIndex)
                    continue;

                var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                var fate = connection.GetTracker()?.GetFates().FirstOrDefault(f => f.BossName == bossName);
                if (fate == null)
                    continue;

                if (!ReconcileKillTime(connection, fate, timestamp))
                    continue;

                // A live event can still carry an old timestamp - e.g. someone using the site's
                // "出現回報" button to backfill a pop that happened a while ago rather than one
                // just discovered. That's worth recording (it's still newer than what we had), but
                // not worth a notification as if it were just spotted right now.
                var isBackfill = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp > BackfillThreshold.TotalMilliseconds;

                // Fire the same toast/chat/sound notification as a normally-detected pop, even if
                // we're not currently standing in that zone - this is the whole point of pulling
                // in a second data source, so it's worth surfacing regardless of location.
                if (notify && !isBackfill)
                    FateManager.DisplayFatePop(fate);
            }
        }

        // Shared by ApplySpawn (regular NMs) and ReconcileBaEncounter (Ovni/Tristitia): applies a
        // remote kill/pop timestamp to a local EurekaFate only if it differs from what we already
        // have by more than ReconcileTolerance either way, then persists it to the shared tracker
        // backend so it survives a plugin reload. Returns whether it was actually applied (the
        // caller uses this to decide whether the change is also worth a notification).
        private static bool ReconcileKillTime(EurekaConnectionManager connection, EurekaFate fate, long timestamp)
        {
            var localKilledAt = fate.GetKilledAt();
            if (localKilledAt != -1 && Math.Abs(timestamp - localKilledAt) <= ReconcileTolerance.TotalMilliseconds)
                return false;

            fate.SetKill(timestamp);

            if (fate.TrackerId is { } trackerId && connection.IsConnected() && connection.CanModify())
                _ = Task.Run(async () => await connection.SetPopTime(trackerId, timestamp));

            return true;
        }

        private void HandleBaEvent(string eventName, string json)
        {
            DalamudApi.Log.Verbose($"[CookieBoxTracker] ba event: {eventName}, data: {json}");

            if (eventName != "put" && eventName != "patch")
                return;

            try
            {
                var payload = JObject.Parse(json);
                var path = (string)payload["path"];
                var data = payload["data"];

                if (path == null)
                    return;

                var isInitial = !_baFirstEventProcessed;
                _baFirstEventProcessed = true;

                var baseSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                ProcessBaNode(baseSegments, data, isInitial);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Warning($"[CookieBoxTracker] Failed to process ba event: {ex.Message}");
            }
        }

        // Same flat-or-nested path walking approach as ProcessNode, but for the /eureka/ba
        // schema: towerState/jellyfish/desk are each a single object (patched a field at a time
        // or replaced/cleared wholesale), schedule/<pushId> is one object per host-schedule row.
        private void ProcessBaNode(string[] baseSegments, JToken node, bool isInitial)
        {
            if (baseSegments.Length == 2 && baseSegments[0] == "schedule")
            {
                if (node == null || node.Type == JTokenType.Null)
                {
                    lock (_baLock) _scheduleByPushId.Remove(baseSegments[1]);
                }
                else if (node is JObject scheduleObj)
                {
                    lock (_baLock) _scheduleByPushId[baseSegments[1]] = (JObject)scheduleObj.DeepClone();
                }

                return;
            }

            if (baseSegments.Length == 1 && (baseSegments[0] == "jellyfish" || baseSegments[0] == "desk"))
            {
                if (node == null || node.Type == JTokenType.Null)
                {
                    lock (_baLock)
                    {
                        if (baseSegments[0] == "jellyfish") _jellyfishRaw = new JObject();
                        else _deskRaw = new JObject();
                    }
                }
                else if (node is JObject encObj)
                {
                    lock (_baLock)
                    {
                        if (baseSegments[0] == "jellyfish") _jellyfishRaw = (JObject)encObj.DeepClone();
                        else _deskRaw = (JObject)encObj.DeepClone();
                    }
                }

                if (!isInitial)
                    ReconcileBaEncounter(baseSegments[0]);

                return;
            }

            if (baseSegments.Length == 1 && baseSegments[0] == "towerState")
            {
                lock (_baLock)
                    _towerRaw = node is JObject towerObj ? (JObject)towerObj.DeepClone() : new JObject();

                return;
            }

            if (node == null || node.Type == JTokenType.Null)
                return;

            if (node is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    var childSegments = baseSegments.Concat(prop.Name.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToArray();
                    ProcessBaNode(childSegments, prop.Value, isInitial);
                }
                return;
            }

            // Leaf value patching a single field of an existing jellyfish/desk/towerState object
            // (e.g. a "patch" carrying just "jellyfish/killedAt").
            if (baseSegments.Length == 2 && (baseSegments[0] == "jellyfish" || baseSegments[0] == "desk"))
            {
                lock (_baLock)
                {
                    var target = baseSegments[0] == "jellyfish" ? _jellyfishRaw : _deskRaw;
                    target[baseSegments[1]] = node;
                }

                if (!isInitial)
                    ReconcileBaEncounter(baseSegments[0]);
            }
            else if (baseSegments.Length == 2 && baseSegments[0] == "towerState")
            {
                lock (_baLock) _towerRaw[baseSegments[1]] = node;
            }
        }

        // Applies the community tracker's own observed killedAt (real kill or natural FATE
        // timeout, per isNatural) for Ovni/Tristitia to our local EurekaFate - this is strictly
        // better than FateManager's local guess (AssumedDurationFateIds), which only estimates a
        // "death" moment because it has no way to directly observe the FATE's real completion.
        private void ReconcileBaEncounter(string encounterKey)
        {
            var (_, killedAt, isNatural, _) = encounterKey == "jellyfish" ? GetBaJellyfishState() : GetBaDeskState();
            if (killedAt <= 0)
                return;

            var bossName = BaBossNameByEncounterKey[encounterKey];
            var respawnDuration = isNatural ? FateManager.NaturalTimeoutRespawnDuration : FateManager.ConfirmedKillRespawnDuration;

            for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            {
                var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                var fate = connection.GetTracker()?.GetFates().FirstOrDefault(f => f.BossName == bossName);
                if (fate == null)
                    continue;

                if (ReconcileKillTime(connection, fate, killedAt))
                    fate.SetRespawnDuration(respawnDuration);
            }
        }

        // The very first snapshot received on connect contains every reset key from this
        // service's entire history - only record those as seen, don't act on them (that would
        // reset every zone's tracker the instant the stream connects). Only keys that show up
        // AFTER that initial snapshot represent an actual just-happened reset.
        private void ApplyHistoryKey(string key, bool isInitial)
        {
            var isNew = _seenHistoryKeys.Add(key);
            if (!isNew || isInitial)
                return;

            var match = ResetKeyPattern.Match(key);
            if (!match.Success || !ZoneIndexByName.TryGetValue(match.Groups[1].Value, out var zoneIndex))
                return;

            var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
            var fates = connection.GetTracker()?.GetFates();
            if (fates == null)
                return;

            foreach (var fate in fates)
                fate.ResetKill();

            DalamudApi.Log.Information($"[CookieBoxTracker] Zone reset detected ({match.Groups[1].Value}) - cleared local pop times for zone index {zoneIndex}");
        }

        // /eureka/state/newIslandMark/<zone> is written by the site the moment a "new island"
        // reset is reported - it lands before the corresponding history/h_reset_<zone>_... entry
        // (which ApplyHistoryKey also reacts to), so acting on it here clears local pop times a
        // little sooner. Reacting to both is harmless - EurekaFate.ResetKill() is idempotent.
        private void ApplyNewIslandMark(string zoneName, bool isInitial)
        {
            if (isInitial)
                return;

            if (!ZoneIndexByName.TryGetValue(zoneName, out var zoneIndex))
                return;

            var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
            var fates = connection.GetTracker()?.GetFates();
            if (fates == null)
                return;

            foreach (var fate in fates)
                fate.ResetKill();

            DalamudApi.Log.Information($"[CookieBoxTracker] New island mark detected ({zoneName}) - cleared local pop times for zone index {zoneIndex}");
        }

        private void HandleTriggeringEvent(string eventName, string json)
        {
            DalamudApi.Log.Verbose($"[CookieBoxTracker] triggering event: {eventName}, data: {json}");

            if (eventName != "put" && eventName != "patch")
                return;

            try
            {
                var payload = JObject.Parse(json);
                var path = (string)payload["path"];
                var data = payload["data"];

                if (path == null)
                    return;

                var baseSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // A "put" at the root is a full replace of the whole /triggering tree (e.g. the
                // very first event on a fresh connection) - clear first so any nmId/uid that was
                // removed server-side while we were disconnected doesn't linger forever.
                if (baseSegments.Length == 0 && eventName == "put")
                {
                    lock (_triggeringLock)
                        _triggeringByNmId.Clear();
                }

                ProcessTriggeringNode(baseSegments, data);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Warning($"[CookieBoxTracker] Failed to process triggering event: {ex.Message}");
            }
        }

        // Same flat-or-nested path walking approach as ProcessNode above, but for the simpler
        // 2-level /triggering/<nmId>/<discordId> schema.
        private void ProcessTriggeringNode(string[] baseSegments, JToken node)
        {
            if (node == null || node.Type == JTokenType.Null)
            {
                lock (_triggeringLock)
                {
                    if (baseSegments.Length == 1)
                    {
                        _triggeringByNmId.Remove(baseSegments[0]);
                    }
                    else if (baseSegments.Length >= 2 && _triggeringByNmId.TryGetValue(baseSegments[0], out var uids))
                    {
                        uids.Remove(baseSegments[1]);
                        if (uids.Count == 0)
                            _triggeringByNmId.Remove(baseSegments[0]);
                    }
                }
                return;
            }

            if (node is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    var childSegments = baseSegments.Concat(prop.Name.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToArray();
                    ProcessTriggeringNode(childSegments, prop.Value);
                }
                return;
            }

            // Leaf value (a timestamp) at the fully resolved [nmId, discordId] path.
            if (baseSegments.Length < 2)
                return;

            lock (_triggeringLock)
            {
                if (!_triggeringByNmId.TryGetValue(baseSegments[0], out var uids))
                {
                    uids = new HashSet<string>();
                    _triggeringByNmId[baseSegments[0]] = uids;
                }
                uids.Add(baseSegments[1]);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();

            // ConnectAndListen's SSE read loop awaits reader.ReadLineAsync() with no cancellation
            // token - on a long-lived push stream, that can sit waiting for the server's next line
            // indefinitely, so _cts.Cancel() alone doesn't unblock it. CancelPendingRequests() tears
            // down the in-flight HttpClient request/stream directly, which makes that pending read
            // throw immediately instead of each .Wait() below blocking the caller (Dalamud's plugin
            // disable, on the main/framework thread) for its full timeout.
            _httpClient.CancelPendingRequests();

            try { _listenTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
            try { _triggeringListenTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
            try { _baListenTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
            _httpClient.Dispose();
            _cts.Dispose();
        }
    }
}
