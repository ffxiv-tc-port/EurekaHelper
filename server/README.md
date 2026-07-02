# EurekaTrackerServer

A small self-hosted replacement for `ffxiv-eureka.com` (create/share a live
Eureka NM-pop tracker with your party): a REST + WebSocket backend, plus a
static web frontend (`wwwroot/`) so trackers are viewable/editable in a
browser too, not just from the plugin. Default domain used throughout the
plugin/config is `ffxiv-eureka.lother.dev` — change it if you deploy
somewhere else.

## Why this exists

`ffxiv-eureka.com` is unmaintained (still online as of writing, but no source
available and no guarantee it stays up). This replaces it with a small
ASP.NET Core app + SQLite, instead of the original's Phoenix/Elixir +
Phoenix Channels stack.

## No password — "editing" is just a toggle

The original required a password (returned once, to the creator) to gain
write access. This version drops that entirely: anyone with the 6-character
share code can join AND edit — there's no access control. Each connected
client (plugin or web) has a local "editing" checkbox/toggle that broadcasts
an `editors` count to everyone else in the room, purely as a courtesy signal
("2 people currently have editing on") — it does not gate anything
server-side. Simpler sharing, less security; fine for a casual party tool,
not fine if you don't trust whoever you hand the link to.

## Protocol

### `POST /api/instances`
Create a tracker, or copy an existing one's kill-time state into a new one.

```json
// request
{ "zoneId": 1 }
// or
{ "zoneId": 1, "copyFrom": "ABC123" }

// response
{ "id": "ABC123" }
```
`id` is the 6-character share code (Crockford-ish alphabet, no `0/O/1/I`).

### `GET /api/instances?zoneId=1&dataCenterId=3`
List public tracker share codes for a zone + datacenter (used by the
plugin's `/etrackers` command). `{ "ids": ["ABC123", "DEF456"] }`

### `GET /api/zones`
Static NM roster per zone (`[{ "zoneId": 732, "monsters": [{ "id": 1,
"bossName": "Sabotender Corrido", "level": 1 }, ...] }, ...]`), used by the
web frontend to render monster names. Dumped once from the plugin's own
`EurekaHelper/XIV/Zones/*.cs` definitions — see `Data/zones.json` and
"Regenerating zones.json" below if the plugin's NM roster ever changes.

### `GET /ws/{id}`
WebSocket, no auth. Server → client, sent once on connect:
```json
{ "type": "initial", "zoneId": 1, "killTimes": {"12": 1730000000000}, "public": false, "dataCenterId": null, "viewers": 2, "editors": 0 }
```

Server → client, broadcast to everyone in the room on change:
```json
{ "type": "kill_times", "killTimes": { "12": 1730000000000 } }
{ "type": "visibility", "public": true, "dataCenterId": 3 }
{ "type": "viewers", "count": 2 }
{ "type": "editors", "count": 1 }
{ "type": "error", "message": "..." }
```

Client → server (all take effect immediately, no permission check):
```json
{ "type": "set_editing", "editing": true }
{ "type": "set_kill_time", "monsterId": 12, "time": 1730000000000 }
{ "type": "reset_kill", "monsterId": 12 }
{ "type": "reset_all" }
{ "type": "set_visibility", "dataCenterId": 3 }   // null dataCenterId = private
```

## Web frontend

`wwwroot/` is a small vanilla HTML/CSS/JS single-page app (no build step,
no framework) served directly by the ASP.NET Core app:
- `/` — pick a zone, creates a tracker, redirects to `/{id}`
- `/{id}` — live tracker view: NM list with level/name/kill-elapsed-time,
  an "Enable editing" checkbox (no password prompt), copy-link button,
  viewer/editor counts. Client-side router reads `location.pathname`;
  `app.MapFallbackToFile("index.html")` in `Program.cs` makes pretty
  `/{id}` URLs work without a real file existing at that path.

**Known simplification:** the frontend shows raw "killed N ago" / "ready"
(flat 2h window, matching `EurekaFate.IsPopped()`) rather than the plugin's
full respawn-condition countdown (weather/night/spawned-by prerequisites
before an NM can even prep). Porting that logic to JS would mean duplicating
`EurekaFate.GetRespawnRequirements()` and the per-zone weather tables — not
done here. The plugin itself still shows the full logic; this is "good
enough for a quick glance from a phone/browser," not a full plugin
replacement.

### Regenerating `Data/zones.json`

If NM data in `EurekaHelper/XIV/Zones/*.cs` changes, regenerate the roster by
temporarily building a throwaway console project that references those files
plus `EurekaFate.cs`/`EorzeaWeather.cs`/`EurekaElement.cs`/`EorzeaTime.cs`/
`IEurekaTracker.cs` (stub out `Utils.GetFatePositionFromLgb` and `Loc.Text` —
neither is needed for id/name/level), then:
```csharp
var zones = new (int ZoneId, IEurekaTracker Tracker)[] {
    (732, EurekaAnemos.GetTracker()), (763, EurekaPagos.GetTracker()),
    (795, EurekaPyros.GetTracker()), (827, EurekaHydatos.GetTracker()),
};
var result = zones.Select(z => new {
    zoneId = z.ZoneId,
    monsters = z.Tracker.GetFates().Where(f => f.IncludeInTracker)
        .Select(f => new { id = f.TrackerId, bossName = f.BossName, level = f.FateLevel }),
});
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
```
Reusing the real `GetFates()`/`IncludeInTracker` logic avoids hand-transcribing
~65 NM entries and getting one wrong.

## Local dev

```bash
cd EurekaTrackerServer
dotnet run
# REST + WS + static frontend on http://localhost:5000 (or whatever ASPNETCORE_URLS you set)
```
SQLite file lands in `./data` (or `$DataDirectory` if set) next to the binary
when run outside Docker, or the `/data` volume when run in the container.

## Deploying on a VPS

Prerequisites: a VPS with Docker + Docker Compose, a domain's A/AAAA record
pointed at the VPS's IP, and ports 80/443 open (Caddy needs 80 for the ACME
HTTP challenge, then serves 443).

```bash
git clone <this-repo-or-just-copy-the-server-folder> && cd server
DOMAIN=ffxiv-eureka.lother.dev docker compose up -d --build
```

Caddy automatically requests and renews a Let's Encrypt certificate for
`$DOMAIN` and reverse-proxies to the app container — no manual certbot/nginx
config needed. Data persists in the `eureka-data` Docker volume across
restarts/upgrades; back it up (it's just a SQLite file) if you care about
keeping trackers between deploys.

To upgrade: `git pull && docker compose up -d --build`.

If you deploy to a domain other than `ffxiv-eureka.lother.dev`, also update
the three hardcoded constants in the plugin:
`EurekaHelper/Utils.cs` (`Constants.EurekaTrackerLink`) and
`EurekaHelper/System/EurekaConnectionManager.cs`
(`TrackerBaseUrl`/`TrackerWebSocketBaseUrl`).

## What's intentionally NOT replicated

- **Passwords / write-access control.** See "No password" above.
- **Phoenix Channels envelope** (`[join_ref, ref, topic, event, payload]`,
  `phx_join`, `presence_diff`, manual 30s heartbeat). This server uses one
  flat JSON object per message instead — .NET's `ClientWebSocket` already
  handles WebSocket ping/pong at the OS level, so the plugin doesn't need to
  send its own heartbeat messages anymore either.
- **Full respawn-condition countdown in the web frontend** — see "Web
  frontend" above.
