# EurekaTrackerServer

A small self-hosted replacement for `ffxiv-eureka.com`'s tracker backend
(create/share a live Eureka NM-pop tracker with your party). Built to be a
drop-in swap for the EurekaHelper plugin: point `Utils.EurekaTrackerLink`,
`EurekaConnectionManager.TrackerAPIUrl`, and `EurekaConnectionManager.TrackerUrl`
at your own domain and the rest of the plugin works unchanged.

## Why this exists

`ffxiv-eureka.com` is unmaintained (still online as of writing, but no source
available and no guarantee it stays up). This replaces just the backend —
REST endpoint to create/copy a tracker, WebSocket channel to sync NM
kill-times and visibility settings in real time — with a small ASP.NET Core
app and SQLite for persistence, instead of the original's Phoenix/Elixir +
Phoenix Channels stack.

## Protocol

### `POST /api/instances`
Create a tracker, or copy an existing one's kill-time state into a new one.

```json
// request
{ "zoneId": 1 }
// or
{ "zoneId": 1, "copyFrom": "ABC123" }

// response
{ "id": "ABC123", "password": "xxxxxxxxxxxx" }
```
`id` is the 6-character share code (Crockford-ish alphabet, no `0/O/1/I`).
`password` is only ever returned once, to the creator — write access to
the instance requires it.

### `GET /ws/{id}?password=<optional>`
WebSocket. If `password` matches the instance's stored password, the
connection is granted write access (`canModify: true` in the initial
payload); otherwise it's read-only and write messages are silently ignored.

Server → client, sent once on connect:
```json
{ "type": "initial", "zoneId": 1, "killTimes": {"12": 1730000000000}, "public": false, "dataCenterId": null, "canModify": true, "viewers": 2 }
```

Server → client, broadcast to everyone in the room on change:
```json
{ "type": "kill_times", "killTimes": { "12": 1730000000000 } }
{ "type": "visibility", "public": true, "dataCenterId": 3 }
{ "type": "viewers", "count": 2 }
```

Server → client, sent only to the requesting connection:
```json
{ "type": "password_set", "success": true, "password": "xxxxxxxxxxxx" }
{ "type": "error", "message": "..." }
```

Client → server (only take effect if this connection has `canModify`,
except `set_password` which is how a read-only connection upgrades itself):
```json
{ "type": "set_password", "password": "xxxxxxxxxxxx" }
{ "type": "set_kill_time", "monsterId": 12, "time": 1730000000000 }
{ "type": "reset_kill", "monsterId": 12 }
{ "type": "reset_all" }
{ "type": "set_visibility", "dataCenterId": 3 }   // null dataCenterId = private
```

## Local dev

```bash
cd EurekaTrackerServer
dotnet run
# REST + WS on http://localhost:5000 (or whatever ASPNETCORE_URLS you set)
```
SQLite file lands in `./data` (or `$DataDirectory` if set) next to the binary
when run outside Docker, or the `/data` volume when run in the container.

## Deploying on a VPS

Prerequisites: a VPS with Docker + Docker Compose, a domain's A/AAAA record
pointed at the VPS's IP, and ports 80/443 open (Caddy needs 80 for the ACME
HTTP challenge, then serves 443).

```bash
git clone <this-repo-or-just-copy-the-server-folder> && cd server
DOMAIN=tracker.yourdomain.com docker compose up -d --build
```

Caddy automatically requests and renews a Let's Encrypt certificate for
`$DOMAIN` and reverse-proxies to the app container — no manual certbot/nginx
config needed. Data persists in the `eureka-data` Docker volume across
restarts/upgrades; back it up (it's just a SQLite file) if you care about
keeping trackers between deploys.

To upgrade: `git pull && docker compose up -d --build`.

## What's intentionally NOT replicated

- **Public tracker listing / presence browsing.** The original protocol had
  hooks for this (`TrackerList`/`GetCurrentTrackers` in the plugin's
  `EurekaConnectionManager.cs`), but nothing in the current plugin UI actually
  calls it — dead code. Not worth reimplementing until something needs it.
- **Phoenix Channels envelope** (`[join_ref, ref, topic, event, payload]`,
  `phx_join`, `presence_diff`, manual 30s heartbeat). This server uses one
  flat JSON object per message instead — .NET's `ClientWebSocket` already
  handles WebSocket ping/pong at the OS level, so the plugin doesn't need to
  send its own heartbeat messages anymore either.
