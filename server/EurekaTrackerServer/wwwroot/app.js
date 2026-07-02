const ZONE_NAMES = { 732: "Anemos", 763: "Pagos", 795: "Pyros", 827: "Hydatos" };
const POP_WINDOW_MS = 2 * 60 * 60 * 1000; // matches EurekaFate.IsPopped()'s flat 2h window

const app = document.getElementById("app");
const statusBar = document.getElementById("status-bar");

const path = location.pathname.replace(/^\/|\/$/g, "");

if (path === "") {
  renderLanding();
} else {
  renderTracker(path.toUpperCase());
}

async function renderLanding() {
  app.innerHTML = `
    <p>Create a tracker for a zone, then share the link with your party.</p>
    <div class="zone-grid">
      ${Object.entries(ZONE_NAMES)
        .map(([id, name]) => `<div class="zone-card" data-zone="${id}">${name}</div>`)
        .join("")}
    </div>
  `;

  app.querySelectorAll(".zone-card").forEach((card) => {
    card.addEventListener("click", async () => {
      const zoneId = Number(card.dataset.zone);
      const res = await fetch("/api/instances", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ zoneId }),
      });
      const { id } = await res.json();
      location.href = `/${id}`;
    });
  });
}

async function renderTracker(id) {
  app.innerHTML = `<p class="loading">Connecting…</p>`;

  const zonesRes = await fetch("/api/zones");
  const zones = await zonesRes.json();

  const protocol = location.protocol === "https:" ? "wss:" : "ws:";
  const ws = new WebSocket(`${protocol}//${location.host}/ws/${id}`);

  let zoneId = null;
  let killTimes = {};
  let isEditing = false;

  ws.addEventListener("open", () => {
    // Nothing to send on connect - server pushes the initial state.
  });

  ws.addEventListener("close", () => {
    setStatus(`<span style="color:var(--popped)">Disconnected</span>`);
  });

  ws.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);

    switch (message.type) {
      case "initial":
        if (message.zoneId === 0) {
          app.innerHTML = `<p>Tracker not found.</p>`;
          ws.close();
          return;
        }
        zoneId = message.zoneId;
        killTimes = message.killTimes || {};
        renderBoard(zones, zoneId, id, killTimes, ws, isEditing);
        updateCounts(message.viewers, message.editors);
        break;

      case "kill_times":
        killTimes = message.killTimes || {};
        renderBoard(zones, zoneId, id, killTimes, ws, isEditing);
        break;

      case "viewers":
        updateCounts(message.count, null);
        break;

      case "editors":
        updateCounts(null, message.count);
        break;

      case "error":
        if (message.message === "not_found") {
          app.innerHTML = `<p>Tracker not found.</p>`;
          ws.close();
        }
        break;
    }
  });

  window.__setEditing = (checked) => {
    isEditing = checked;
    ws.send(JSON.stringify({ type: "set_editing", editing: checked }));
    if (zoneId !== null) renderBoard(zones, zoneId, id, killTimes, ws, isEditing);
  };

  window.__markKilled = (monsterId) => {
    if (!isEditing) return;
    ws.send(JSON.stringify({ type: "set_kill_time", monsterId, time: Date.now() }));
  };

  window.__resetOne = (monsterId) => {
    if (!isEditing) return;
    ws.send(JSON.stringify({ type: "reset_kill", monsterId }));
  };

  window.__resetAll = () => {
    if (!isEditing) return;
    if (!confirm("Reset all kill times for this tracker?")) return;
    ws.send(JSON.stringify({ type: "reset_all" }));
  };

  window.__copyLink = () => {
    navigator.clipboard.writeText(location.href);
  };
}

let lastViewers = 0;
let lastEditors = 0;

function updateCounts(viewers, editors) {
  if (viewers !== null) lastViewers = viewers;
  if (editors !== null) lastEditors = editors;
  setStatus(
    `<span>${lastViewers} viewing</span>` +
      (lastEditors > 0 ? ` <span class="editing-badge">· ${lastEditors} editing</span>` : "")
  );
}

function setStatus(html) {
  statusBar.innerHTML = html;
}

function renderBoard(zones, zoneId, trackerId, killTimes, ws, isEditing) {
  const zone = zones.find((z) => z.zoneId === zoneId);
  const monsters = (zone ? zone.monsters : []).slice().sort((a, b) => a.level - b.level);
  const now = Date.now();

  const rows = monsters
    .map((m) => {
      const killedAt = killTimes[m.id];
      const popped = killedAt && now - killedAt < POP_WINDOW_MS;
      const statusText = popped ? formatElapsed(now - killedAt) + " ago" : "Ready";

      return `
        <tr class="${popped ? "popped" : "ready"}">
          <td>${m.level}</td>
          <td>${m.bossName}</td>
          <td class="status">${statusText}</td>
          <td>
            <div class="row-actions">
              <button ${isEditing ? "" : "disabled"} onclick="__markKilled(${m.id})">Killed now</button>
              <button ${isEditing ? "" : "disabled"} onclick="__resetOne(${m.id})">Reset</button>
            </div>
          </td>
        </tr>
      `;
    })
    .join("");

  app.innerHTML = `
    <div class="toolbar">
      <span class="code-pill">${trackerId}</span>
      <button onclick="__copyLink()">Copy link</button>
      <label class="toggle">
        <input type="checkbox" ${isEditing ? "checked" : ""} onchange="__setEditing(this.checked)" />
        Enable editing
      </label>
      ${isEditing ? '<button class="danger" onclick="__resetAll()">Reset all</button>' : ""}
    </div>

    <table>
      <thead>
        <tr><th>Lv</th><th>NM</th><th>Status</th><th></th></tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>

    <p class="hint">
      Anyone with this link can turn on editing - there's no password. Turn editing off when
      you're done to avoid accidental clicks.
    </p>
  `;
}

function formatElapsed(ms) {
  const totalMinutes = Math.floor(ms / 60000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}
