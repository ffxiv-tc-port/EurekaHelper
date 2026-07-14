using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
    // Only feeds known, confidently-mapped named NM keys (see BossNameByShortKey) into
    // EurekaFate.SetKill - the "o_<zone>_<level>_<hash>" keys used for mutant-monster events have
    // no decodable name in this database and are intentionally skipped rather than guessed.
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

        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(15);

        // Best-effort short-key -> EurekaFate.BossName map, reverse-engineered from a handful of
        // observed /eureka/state/history entries (h_<key>_<timestamp>). Not exhaustive - any
        // NM/zone not listed here is simply never updated from this source. Extend as more
        // short-keys are observed in the wild.
        private static readonly Dictionary<string, string> BossNameByShortKey = new(StringComparer.OrdinalIgnoreCase)
        {
            ["arthro"] = "King Arthro",
            ["lamebrix"] = "Lamebrix Strikebocks",
            ["pazuzu"] = "Pazuzu",
            ["molech"] = "Molech",
            ["penthesilea"] = "Penthesilea",
            ["goldemar"] = "King Goldemar",
            ["yinyang"] = "Ying-Yang",
            ["skoll"] = "Skoll",
            ["ceto"] = "Ceto",

            // Confirmed via screenshots of the site's own NM list (nickname shown next to name),
            // not directly observed in a history entry yet - short-key spelling here is a
            // best guess following the same convention as the confirmed entries above.
            ["cassie"] = "Copycat Cassie",
            ["kc"] = "Copycat Cassie",
            ["louhi"] = "Louhi",
            ["pw"] = "Provenance Watcher",
            ["provenancewatcher"] = "Provenance Watcher",
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

                ApplySpawn(prop.Name, timestamp, notify: false);
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
                var line = await reader.ReadLineAsync();
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

        private void ApplySpawn(string shortKey, long timestamp, bool notify)
        {
            if (shortKey.StartsWith("o_"))
                return; // mutant-monster synthetic id - not decodable to a specific mob

            if (!BossNameByShortKey.TryGetValue(shortKey, out var bossName))
                return;

            for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            {
                // While physically standing in this zone, our own in-game detection (FateManager)
                // is authoritative and already fires the same notification - applying this
                // external report on top would just be a redundant/conflicting second event for
                // something we already recorded ourselves.
                if (zoneIndex == ZoneManager.CurrentZoneIndex)
                    continue;

                var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                var fate = connection.GetTracker()?.GetFates().FirstOrDefault(f => f.BossName == bossName);
                if (fate == null)
                    continue;

                // Only ever move the pop time forward - never let a stale report from this
                // external source overwrite a more recent local one.
                if (timestamp <= fate.GetKilledAt())
                    continue;

                fate.SetKill(timestamp);

                // Fire the same toast/chat/sound notification as a normally-detected pop, even if
                // we're not currently standing in that zone - this is the whole point of pulling
                // in a second data source, so it's worth surfacing regardless of location.
                if (notify)
                    FateManager.DisplayFatePop(fate);
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
            try { _listenTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
            try { _triggeringListenTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
            _httpClient.Dispose();
            _cts.Dispose();
        }
    }
}
