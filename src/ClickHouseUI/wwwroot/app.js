/* ClickHouseUI - dashboard frontend
 *
 * Tiny hash-router with five views: overview, metrics, tables, slow-queries, explain.
 * No build step - everything is shipped embedded in the NuGet package.
 */
(function () {
  const BASE = (window.__CH_UI__ && window.__CH_UI__.basePath) || "/clickhouse";
  const view = document.getElementById("view");
  const statusDot  = document.getElementById("status-dot");
  const statusText = document.getElementById("status-text");

  const state = {
    charts: {},                  // active Chart.js instances keyed by canvas id
    metricsHistory: { ts: [], inserts: [], selects: [], failed: [], memory: [] },
    pollers: [],                 // setInterval handles per view (cleared on route change)
  };

  // ------------------- helpers -------------------

  async function api(path, init) {
    const res = await fetch(BASE + path, Object.assign({ headers: { "content-type": "application/json" } }, init || {}));
    if (!res.ok) {
      setStatus(false);
      const text = await res.text().catch(() => "");
      throw new Error(`API ${path} failed: ${res.status} ${text}`);
    }
    setStatus(true);
    return res.json();
  }

  function setStatus(ok) {
    statusDot.className = "dot " + (ok ? "dot-ok" : "dot-err");
    statusText.textContent = ok ? "connected" : "disconnected";
  }

  function el(html) {
    const t = document.createElement("template");
    t.innerHTML = html.trim();
    return t.content.firstElementChild;
  }

  function fmtNumber(n) {
    if (n === null || n === undefined || n === "") return "-";
    const num = typeof n === "number" ? n : Number(n);
    if (Number.isNaN(num)) return String(n);
    return num.toLocaleString();
  }

  function fmtUptime(seconds) {
    if (!seconds) return "-";
    const d = Math.floor(seconds / 86400);
    const h = Math.floor((seconds % 86400) / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${d}d ${h}h ${m}m`;
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({ "&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;" }[c]));
  }

  function clearPollers() {
    state.pollers.forEach(clearInterval);
    state.pollers = [];
    Object.values(state.charts).forEach((c) => c.destroy());
    state.charts = {};
  }

  // ------------------- router -------------------

  const routes = {
    overview: renderOverview,
    metrics:  renderMetrics,
    tables:   renderTables,
    "slow-queries": renderSlowQueries,
    explain:  renderExplain,
  };

  function route() {
    const hash = (location.hash || "#/overview").replace(/^#\//, "");
    const name = (hash.split("/")[0]) || "overview";
    document.querySelectorAll(".nav").forEach((a) => a.classList.toggle("active", a.dataset.route === name));
    clearPollers();
    view.innerHTML = '<div class="loading">Loading...</div>';
    const fn = routes[name] || renderOverview;
    Promise.resolve(fn()).catch((err) => {
      console.error(err);
      view.innerHTML = `<div class="error">${escapeHtml(err.message)}</div>`;
    });
  }

  window.addEventListener("hashchange", route);

  // ------------------- views -------------------

  async function renderOverview() {
    const data = await api("/api/overview");
    view.innerHTML = "";
    view.appendChild(el(`<h1>Overview</h1>`));
    view.appendChild(el(`<div class="subtitle">ClickHouse server snapshot</div>`));

    const cards = el(`<div class="cards"></div>`);
    cards.appendChild(stat("Version", data.version || "-"));
    cards.appendChild(stat("Uptime", fmtUptime(data.uptimeSeconds)));
    cards.appendChild(stat("Databases", fmtNumber(data.databases)));
    cards.appendChild(stat("Tables", fmtNumber(data.tables)));
    view.appendChild(cards);

    view.appendChild(el(`<h2>Storage</h2>`));
    const disks = el(`<div class="panel"><table><thead><tr>
      <th>Name</th><th>Path</th><th>Total</th><th>Free</th><th>Used %</th>
    </tr></thead><tbody></tbody></table></div>`);
    const dtbody = disks.querySelector("tbody");
    (data.disks || []).forEach((d) => {
      const pct = Number(d.used_percent || 0);
      const cls = pct > 90 ? "badge err" : pct > 75 ? "badge warn" : "badge ok";
      dtbody.appendChild(el(`<tr>
        <td>${escapeHtml(d.name)}</td>
        <td class="code">${escapeHtml(d.path)}</td>
        <td class="num">${escapeHtml(d.total)}</td>
        <td class="num">${escapeHtml(d.free)}</td>
        <td class="num"><span class="${cls}">${pct.toFixed(1)}%</span></td>
      </tr>`));
    });
    view.appendChild(disks);

    if (data.clusters && data.clusters.length) {
      view.appendChild(el(`<h2>Clusters</h2>`));
      const c = el(`<div class="panel"><table><thead><tr>
        <th>Cluster</th><th>Host</th><th>Port</th><th>Shard</th><th>Replica</th><th>Local</th>
      </tr></thead><tbody></tbody></table></div>`);
      const tb = c.querySelector("tbody");
      data.clusters.forEach((r) => {
        tb.appendChild(el(`<tr>
          <td>${escapeHtml(r.cluster)}</td>
          <td class="code">${escapeHtml(r.host_address)}</td>
          <td class="num">${escapeHtml(r.port)}</td>
          <td class="num">${escapeHtml(r.shard_num)}</td>
          <td class="num">${escapeHtml(r.replica_num)}</td>
          <td>${r.is_local ? '<span class="badge ok">yes</span>' : "<span class='badge'>no</span>"}</td>
        </tr>`));
      });
      view.appendChild(c);
    }
  }

  function stat(label, value, sub) {
    const card = el(`<div class="card">
      <div class="card-label">${escapeHtml(label)}</div>
      <div class="card-value">${escapeHtml(value)}</div>
      ${sub ? `<div class="card-sub">${escapeHtml(sub)}</div>` : ""}
    </div>`);
    return card;
  }

  async function renderMetrics() {
    view.innerHTML = "";
    view.appendChild(el(`<h1>Live Metrics</h1>`));
    view.appendChild(el(`<div class="subtitle">Refreshes every 2 seconds from system.metrics / events / asynchronous_metrics</div>`));

    const cards = el(`<div class="cards" id="m-cards"></div>`);
    view.appendChild(cards);

    const charts = el(`<div class="chart-grid">
      <div class="chart-card"><div class="chart-title">Query rate (events/sec)</div><canvas id="c-queries"></canvas></div>
      <div class="chart-card"><div class="chart-title">Insert vs Select (events/sec)</div><canvas id="c-insert-select"></canvas></div>
      <div class="chart-card"><div class="chart-title">Memory tracking (bytes)</div><canvas id="c-memory"></canvas></div>
      <div class="chart-card"><div class="chart-title">Network throughput (bytes/sec)</div><canvas id="c-net"></canvas></div>
    </div>`);
    view.appendChild(charts);

    state.metricsHistory = { ts: [], queries: [], inserts: [], selects: [], failed: [], memory: [], netIn: [], netOut: [] };
    let prevEvents = null;
    let prevTs = 0;

    async function tick() {
      try {
        const data = await api("/api/metrics");
        const evMap = mapBy(data.events, "event", "value");
        const mtMap = mapBy(data.metrics, "metric", "value");
        const asMap = mapBy(data.asyncMetrics, "metric", "value");

        const now = Date.now();
        const dt = prevTs ? Math.max(1, (now - prevTs) / 1000) : 1;

        const rates = prevEvents ? {
          queries: rate(evMap.Query, prevEvents.Query, dt),
          inserts: rate(evMap.InsertQuery, prevEvents.InsertQuery, dt),
          selects: rate(evMap.SelectQuery, prevEvents.SelectQuery, dt),
          failed:  rate(evMap.FailedQuery, prevEvents.FailedQuery, dt),
          netIn:   rate(evMap.NetworkReceiveBytes, prevEvents.NetworkReceiveBytes, dt),
          netOut:  rate(evMap.NetworkSendBytes, prevEvents.NetworkSendBytes, dt),
        } : { queries: 0, inserts: 0, selects: 0, failed: 0, netIn: 0, netOut: 0 };

        prevEvents = evMap;
        prevTs = now;

        // KPI cards
        cards.innerHTML = "";
        cards.appendChild(stat("Queries / sec", rates.queries.toFixed(1)));
        cards.appendChild(stat("Inserts / sec", rates.inserts.toFixed(1)));
        cards.appendChild(stat("Selects / sec", rates.selects.toFixed(1)));
        cards.appendChild(stat("Failed / sec",  rates.failed.toFixed(2)));
        cards.appendChild(stat("Active Queries", fmtNumber(mtMap.Query || 0)));
        cards.appendChild(stat("Memory Tracking", humanBytes(mtMap.MemoryTracking || 0)));
        cards.appendChild(stat("Background Merges", fmtNumber(mtMap.BackgroundMergesAndMutationsPoolTask || 0)));
        cards.appendChild(stat("Active Parts", fmtNumber(mtMap.PartsActive || 0)));

        // History buffer (keep last 60 samples)
        const h = state.metricsHistory;
        h.ts.push(new Date().toLocaleTimeString());
        h.queries.push(rates.queries);
        h.inserts.push(rates.inserts);
        h.selects.push(rates.selects);
        h.failed.push(rates.failed);
        h.memory.push(Number(mtMap.MemoryTracking || 0));
        h.netIn.push(rates.netIn);
        h.netOut.push(rates.netOut);
        Object.keys(h).forEach((k) => { if (h[k].length > 60) h[k].shift(); });

        upsertLine("c-queries", h.ts, [{ label: "queries/s", data: h.queries, color: "#ffd86b" }]);
        upsertLine("c-insert-select", h.ts, [
          { label: "inserts/s", data: h.inserts, color: "#4ade80" },
          { label: "selects/s", data: h.selects, color: "#60a5fa" },
        ]);
        upsertLine("c-memory", h.ts, [{ label: "MemoryTracking", data: h.memory, color: "#f472b6" }]);
        upsertLine("c-net", h.ts, [
          { label: "rx B/s", data: h.netIn,  color: "#22d3ee" },
          { label: "tx B/s", data: h.netOut, color: "#a78bfa" },
        ]);
      } catch (e) {
        console.error(e);
      }
    }

    await tick();
    state.pollers.push(setInterval(tick, 2000));
  }

  function mapBy(rows, keyCol, valCol) {
    const map = {};
    (rows || []).forEach((r) => { map[r[keyCol]] = Number(r[valCol]); });
    return map;
  }
  function rate(curr, prev, dt) {
    if (curr === undefined || prev === undefined) return 0;
    const diff = Number(curr) - Number(prev);
    return diff < 0 ? 0 : diff / dt;
  }
  function humanBytes(n) {
    n = Number(n) || 0;
    const u = ["B","KB","MB","GB","TB","PB"];
    let i = 0;
    while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
    return n.toFixed(n >= 100 || i === 0 ? 0 : 1) + " " + u[i];
  }

  function upsertLine(canvasId, labels, datasets) {
    const existing = state.charts[canvasId];
    if (existing) {
      existing.data.labels = labels;
      datasets.forEach((d, i) => {
        existing.data.datasets[i].data = d.data;
      });
      existing.update("none");
      return;
    }
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    state.charts[canvasId] = new Chart(ctx, {
      type: "line",
      data: {
        labels,
        datasets: datasets.map((d) => ({
          label: d.label,
          data: d.data,
          borderColor: d.color,
          backgroundColor: d.color + "22",
          borderWidth: 2,
          tension: 0.3,
          pointRadius: 0,
          fill: true,
        })),
      },
      options: {
        animation: false,
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { labels: { color: "#98a1b3", boxWidth: 10 } } },
        scales: {
          x: { ticks: { color: "#6b7384", maxTicksLimit: 6 }, grid: { color: "#262c38" } },
          y: { ticks: { color: "#6b7384" }, grid: { color: "#262c38" } },
        },
      },
    });
  }

  async function renderTables() {
    view.innerHTML = "";
    view.appendChild(el(`<h1>Tables &amp; Parts</h1>`));
    view.appendChild(el(`<div class="subtitle">Aggregated from system.parts (active parts only)</div>`));

    const data = await api("/api/tables");
    const panel = el(`<div class="panel">
      <div class="panel-header">
        <span>Tables</span>
        <div class="actions"><input id="t-filter" placeholder="filter table or database..." style="width:280px"></div>
      </div>
      <table>
        <thead><tr>
          <th>Database</th><th>Table</th><th class="num">Rows</th>
          <th class="num">Size</th><th class="num">Parts</th><th class="num">Compression</th>
          <th>Last modified</th><th></th>
        </tr></thead>
        <tbody id="tbl-rows"></tbody>
      </table>
    </div>`);
    view.appendChild(panel);

    const rows = data.tables || [];
    const tbody = panel.querySelector("#tbl-rows");
    const render = (filter) => {
      tbody.innerHTML = "";
      const f = (filter || "").trim().toLowerCase();
      const filtered = !f ? rows : rows.filter((r) => `${r.database}.${r.table}`.toLowerCase().includes(f));
      if (!filtered.length) { tbody.innerHTML = `<tr><td colspan="8" class="empty">No tables.</td></tr>`; return; }
      filtered.forEach((r) => {
        const tr = el(`<tr>
          <td>${escapeHtml(r.database)}</td>
          <td><strong>${escapeHtml(r.table)}</strong></td>
          <td class="num">${fmtNumber(r.total_rows)}</td>
          <td class="num">${escapeHtml(r.size || "-")}</td>
          <td class="num">${fmtNumber(r.parts_count)}</td>
          <td class="num">${r.compression_ratio ? r.compression_ratio + "x" : "-"}</td>
          <td class="muted">${escapeHtml(r.last_modified || "-")}</td>
          <td><button class="ghost" data-db="${escapeHtml(r.database)}" data-tbl="${escapeHtml(r.table)}">Parts</button></td>
        </tr>`);
        tr.querySelector("button").addEventListener("click", () => showParts(r.database, r.table));
        tbody.appendChild(tr);
      });
    };
    render("");
    panel.querySelector("#t-filter").addEventListener("input", (e) => render(e.target.value));
  }

  async function showParts(database, table) {
    const data = await api(`/api/parts?database=${encodeURIComponent(database)}&table=${encodeURIComponent(table)}`);
    const panel = el(`<div class="panel" style="margin-top:16px">
      <div class="panel-header"><span>Parts of <code>${escapeHtml(database)}.${escapeHtml(table)}</code></span></div>
      <table><thead><tr>
        <th>Partition</th><th>Part</th><th>Active</th><th class="num">Rows</th>
        <th class="num">Size</th><th class="num">Level</th><th>Modified</th>
      </tr></thead><tbody></tbody></table>
    </div>`);
    const tbody = panel.querySelector("tbody");
    (data.parts || []).forEach((p) => {
      tbody.appendChild(el(`<tr>
        <td class="code">${escapeHtml(p.partition)}</td>
        <td class="code">${escapeHtml(p.part)}</td>
        <td>${Number(p.active) ? '<span class="badge ok">active</span>' : '<span class="badge">inactive</span>'}</td>
        <td class="num">${fmtNumber(p.rows)}</td>
        <td class="num">${escapeHtml(p.size || "-")}</td>
        <td class="num">${fmtNumber(p.level)}</td>
        <td class="muted">${escapeHtml(p.modification_time || "-")}</td>
      </tr>`));
    });
    // Replace previously rendered parts panel if any
    const existing = view.querySelector(".panel.parts-panel");
    if (existing) existing.remove();
    panel.classList.add("parts-panel");
    view.appendChild(panel);
    panel.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function renderSlowQueries() {
    view.innerHTML = "";
    view.appendChild(el(`<h1>Slow Queries</h1>`));
    view.appendChild(el(`<div class="subtitle">Top finished queries by duration from system.query_log</div>`));

    const data = await api("/api/slow-queries");
    const panel = el(`<div class="panel">
      <div class="panel-header"><span>Last ${data.lookbackHours}h, top ${data.limit}</span></div>
      <table>
        <thead><tr>
          <th>When</th><th>User</th><th class="num">Duration</th><th class="num">Read</th>
          <th class="num">Memory</th><th class="num">Rows</th><th>Query</th>
        </tr></thead>
        <tbody></tbody>
      </table>
    </div>`);
    view.appendChild(panel);

    const tb = panel.querySelector("tbody");
    (data.queries || []).forEach((r) => {
      const dur = Number(r.query_duration_ms) || 0;
      const cls = dur > 5000 ? "badge err" : dur > 1000 ? "badge warn" : "badge ok";
      tb.appendChild(el(`<tr>
        <td class="muted">${escapeHtml(r.event_time)}</td>
        <td>${escapeHtml(r.user)}</td>
        <td class="num"><span class="${cls}">${dur.toLocaleString()} ms</span></td>
        <td class="num">${escapeHtml(r.read_size || "-")}</td>
        <td class="num">${escapeHtml(r.memory || "-")}</td>
        <td class="num">${fmtNumber(r.read_rows)}</td>
        <td class="code" style="max-width:520px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;"
            title="${escapeHtml(r.query)}">${escapeHtml(r.query)}</td>
      </tr>`));
    });
    if (!data.queries || !data.queries.length) {
      tb.appendChild(el(`<tr><td colspan="7" class="empty">No queries in the lookback window.</td></tr>`));
    }
  }

  async function renderExplain() {
    view.innerHTML = "";
    view.appendChild(el(`<h1>Visual EXPLAIN</h1>`));
    view.appendChild(el(`<div class="subtitle">Run EXPLAIN against any query and inspect the execution tree</div>`));

    const wrapper = el(`<div class="panel" style="padding:16px">
      <textarea id="ex-q" placeholder="SELECT count() FROM system.numbers WHERE number < 1000000"></textarea>
      <div class="flex-row" style="margin-top:10px">
        <select id="ex-kind" style="width:160px">
          <option value="PLAN">PLAN</option>
          <option value="PIPELINE">PIPELINE</option>
          <option value="INDEXES">INDEXES</option>
          <option value="SYNTAX">SYNTAX</option>
        </select>
        <span class="spacer"></span>
        <button id="ex-run">Run EXPLAIN</button>
      </div>
    </div>`);
    view.appendChild(wrapper);

    const out = el(`<div id="ex-out" style="margin-top:16px"></div>`);
    view.appendChild(out);

    wrapper.querySelector("#ex-run").addEventListener("click", async () => {
      const q = wrapper.querySelector("#ex-q").value;
      const kind = wrapper.querySelector("#ex-kind").value;
      out.innerHTML = '<div class="loading">Running EXPLAIN...</div>';
      try {
        const data = await api("/api/explain", { method: "POST", body: JSON.stringify({ query: q, kind }) });
        out.innerHTML = "";
        if (data.error) {
          out.appendChild(el(`<div class="error">${escapeHtml(data.error)}</div>`));
          return;
        }
        const panel = el(`<div class="panel"><div class="panel-header"><span>${escapeHtml(data.kind)} tree</span></div><div class="tree" id="ex-tree" style="padding:14px 18px"></div></div>`);
        out.appendChild(panel);
        const treeRoot = panel.querySelector("#ex-tree");
        (data.tree || []).forEach((n) => treeRoot.appendChild(renderTreeNode(n)));
        if (!(data.tree || []).length) {
          treeRoot.appendChild(el(`<div class="empty">No tree returned. Raw output below.</div>`));
        }
        const raw = el(`<div class="panel" style="margin-top:12px"><div class="panel-header"><span>Raw EXPLAIN output</span></div><pre style="padding:14px 18px; margin:0; white-space:pre-wrap; font-family:var(--mono); font-size:12px; color:var(--text-dim);">${escapeHtml(data.raw)}</pre></div>`);
        out.appendChild(raw);
      } catch (e) {
        out.innerHTML = `<div class="error">${escapeHtml(e.message)}</div>`;
      }
    });
  }

  function renderTreeNode(node) {
    const wrap = el(`<div class="tree-node">
      <div><span class="tree-label">${escapeHtml(node.label || "")}</span></div>
    </div>`);
    if (node.details && node.details.length) {
      const d = document.createElement("div");
      d.className = "tree-details";
      d.innerHTML = node.details.map(escapeHtml).join("<br>");
      wrap.appendChild(d);
    }
    if (node.children && node.children.length) {
      const c = document.createElement("div");
      c.className = "tree-children";
      node.children.forEach((ch) => c.appendChild(renderTreeNode(ch)));
      wrap.appendChild(c);
    }
    return wrap;
  }

  // Kick things off
  if (!location.hash) location.hash = "#/overview";
  route();
})();
