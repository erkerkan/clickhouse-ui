# Launch posts

Copy-paste templates for announcing ClickHouseUI. Each one tuned to its venue. Edit the demo GIF link once you have it.

---

## 1. Hacker News — "Show HN"

**Title** (HN guidelines: prefix with "Show HN:", under 80 chars, no marketing fluff):

```
Show HN: ClickHouseUI – a drop-in ASP.NET Core dashboard for ClickHouse
```

**Body** (HN prefers short, no emojis, link the repo, explain what and why):

```
Hi HN — I built ClickHouseUI because every time I had to debug a ClickHouse
cluster at 3 AM I ended up either copy-pasting SELECTs into clickhouse-client
or spinning up a full Grafana stack just to look at system.parts.

It ships as a single NuGet. One line in Program.cs:

    app.UseClickHouseDashboard("Host=localhost;Port=8123;User=default;Database=default");

You get five tabs — Overview, Live Metrics, Tables & Parts, Slow Queries, and
a visual EXPLAIN that renders the plan tree interactively. The whole frontend
(HTML, CSS, vanilla JS, Chart.js) is embedded into the DLL — no
UseStaticFiles, no Node.js build step, ~230 KB on disk total.

Backend hits the standard system.* tables through ClickHouse.Client (ADO.NET).
Read-only by default. Auth-agnostic — drops behind whatever middleware you
already have, or use the Authorize predicate.

It's MIT, .NET 8, working against ClickHouse 22.3+. Tested on 25.x and 26.x.

Repo + screenshots: https://github.com/erkerkan/clickhouse-ui
NuGet: https://www.nuget.org/packages/ClickHouseUI/

Happy to take feedback, especially on the EXPLAIN tree parsing — that one
was tricky to get right across ClickHouse versions.
```

**Timing tip:** Post around 13:00–15:00 UTC (Tuesday–Thursday) for best EU+US overlap. Don't ask for upvotes (auto-flag).

---

## 2. Reddit — r/dotnet

**Title:**

```
I built a one-line embeddable ClickHouse dashboard for ASP.NET Core — feedback welcome
```

**Body:**

```
Hey folks — sharing something I've been working on.

ClickHouseUI is a NuGet package that adds a Hangfire-style web dashboard to
any ASP.NET Core app, but for ClickHouse instead of background jobs:

    app.UseClickHouseDashboard("Host=...;Port=8123;User=...");

That's the whole integration. Five tabs:

- Overview — version, uptime, disks, clusters
- Live Metrics — 2-second polling charts
- Tables & Parts — sizes, compression, drill-down into MergeTree parts
- Slow Queries — top finishers from system.query_log
- Visual EXPLAIN — paste a query, see the plan rendered as an interactive tree

Tech choices that might be interesting:

- Single net8.0 assembly. The frontend (HTML+CSS+JS+Chart.js) is
  embedded with <EmbeddedResource> and served by the middleware itself —
  no UseStaticFiles required.
- Vanilla JS SPA. No Node, no bundler. Total payload ~230 KB.
- ClickHouse.Client (ADO.NET) for transport.
- The middleware is auth-agnostic. Mount it behind whatever auth pipeline
  you already have, or use the Authorize predicate on the options.

MIT, source on GitHub: https://github.com/erkerkan/clickhouse-ui
NuGet: https://www.nuget.org/packages/ClickHouseUI/

Roadmap has a Pro edition planned (Query Killer, schema migrations,
alerting, multi-cluster) but Community will stay read-only and free forever.

Would love thoughts — especially on the API surface, since this is the kind
of thing I want to be stable from day one.
```

---

## 3. Reddit — r/clickhouse

**Title:**

```
[Tool] ClickHouseUI — embeddable .NET dashboard, drops into any ASP.NET Core app
```

**Body:**

```
For the .NET folks on this sub: I built a small read-only dashboard that
embeds into any ASP.NET Core app via a single NuGet package.

Source: github.com/erkerkan/clickhouse-ui

What it shows:

- system.metrics / events / asynchronous_metrics with 2-second polling
- system.parts aggregated per table, with compression ratio and a parts
  drill-down (active vs inactive, level, partition)
- system.query_log top-by-duration with the usual columns
- EXPLAIN PLAN/PIPELINE/INDEXES/SYNTAX rendered as a tree

Tested against 22.3+, currently running it daily on 25.x and 26.x.

Not trying to replace Grafana or Tabix — different niche. This one is for
"I'm a .NET dev, I want monitoring inside the app I already deployed".

Feedback / feature requests very welcome. Especially curious if anyone has
strong opinions on what should live in a free dashboard vs a Pro tier.
```

---

## 4. LinkedIn — English

**Post:**

```
🛠️ Just open-sourced ClickHouseUI — a drop-in dashboard for ClickHouse that
embeds into any ASP.NET Core application with a single line of code:

    app.UseClickHouseDashboard("Host=...;Port=8123;...");

What you get out of the box:

→ Live metrics — query / insert / select rates, memory, network throughput
→ Tables & Parts explorer — sizes, compression, MergeTree parts
→ Slow Queries — top expensive queries from system.query_log
→ Visual EXPLAIN — execution plans rendered as an interactive tree

The entire frontend (HTML + CSS + JS + Chart.js) is embedded inside one DLL.
No separate static files. No Node.js build step. Drops behind your existing
auth pipeline.

MIT-licensed, .NET 8, available now on NuGet.

GitHub: https://github.com/erkerkan/clickhouse-ui
NuGet: https://www.nuget.org/packages/ClickHouseUI/

If you're running ClickHouse and any of your services are .NET, I'd love
your feedback. Stars and bug reports both very welcome.

#dotnet #clickhouse #aspnetcore #opensource #observability #devtools
```

---

## 5. LinkedIn — Türkçe

**Post:**

```
🛠️ Açık kaynak yaptım: ClickHouseUI — ClickHouse için drop-in ASP.NET Core dashboard.

Tek satırlık entegrasyon:

    app.UseClickHouseDashboard("Host=...;Port=8123;...");

Kullanıcı arayüzünde ne var:

→ Anlık metrikler — query / insert / select hızı, memory, network throughput
→ Tablo ve part explorer — boyut, sıkıştırma oranı, MergeTree parts
→ Slow queries — system.query_log üzerinden en yavaş sorgular
→ Görsel EXPLAIN — query planını interaktif ağaç olarak gösterir

Tüm frontend (HTML + CSS + JS + Chart.js) tek bir DLL içinde gömülü. Ayrı
statik dosya yok, Node.js build step yok. Mevcut auth pipeline'ınızın
arkasına direk takılır.

MIT lisanslı, .NET 8 üzerinde çalışıyor, NuGet'te.

GitHub: https://github.com/erkerkan/clickhouse-ui
NuGet: https://www.nuget.org/packages/ClickHouseUI/

ClickHouse + .NET kullanıyorsanız feedback / hata raporu / star — hepsi
çok değerli. İlk sürüm, geri bildirim altın değerinde.

#dotnet #clickhouse #aspnetcore #opensource #devtools
```

---

## 6. Twitter / X

**Tweet 1 (hook):**

```
Just shipped ClickHouseUI 🚀

A drop-in dashboard for ClickHouse that you bolt onto any ASP.NET Core app
with a single line:

    app.UseClickHouseDashboard("Host=...;Port=8123;...");

Live metrics, slow queries, table explorer, visual EXPLAIN — all from one
NuGet, no Node.js, no build step.

🧵
```

**Tweet 2:**

```
The whole frontend (HTML + CSS + JS + Chart.js) is embedded in the assembly
via <EmbeddedResource> and served by the middleware itself.

Total payload: ~230 KB.
Total integration code in your app: 1 line.

That's the design goal.
```

**Tweet 3:**

```
Tabs:

- Overview: version, uptime, disks, clusters
- Live Metrics: 2-second polling from system.metrics + events
- Tables & Parts: drill into MergeTree parts
- Slow Queries: top finishers from system.query_log
- Visual EXPLAIN: PLAN/PIPELINE/INDEXES as an interactive tree
```

**Tweet 4 (call-to-action):**

```
MIT, .NET 8, ClickHouse 22.3+

Repo: github.com/erkerkan/clickhouse-ui
NuGet: nuget.org/packages/ClickHouseUI

Stars, bug reports, PRs — all very welcome.

#dotnet #clickhouse #aspnetcore #opensource
```

---

## 7. dev.to — long-form article

**Title:**

```
How I built a ClickHouse dashboard that ships as a single NuGet package
```

**Outline:**

1. The problem — debugging ClickHouse from .NET sucks today
2. The constraints I gave myself: one DLL, one line, no Node toolchain
3. Embedded static assets via `<EmbeddedResource>` + custom middleware
4. Why I chose vanilla JS over Blazor WebAssembly (size, startup time)
5. Parsing ClickHouse's `EXPLAIN` text output into a tree
6. The bug ClickHouse 26 taught me about aliasing aggregates
7. What's next — Pro edition, Query Killer, multi-cluster

**Tags:** `dotnet`, `clickhouse`, `opensource`, `aspnetcore`

---

## 8. Devnot / Medium TR — Türkçe makale

**Başlık:**

```
Tek NuGet paketiyle ClickHouse dashboard'u: ClickHouseUI nasıl yaptım
```

**Anahat:**

1. Problem — .NET'ten ClickHouse'u izlemek bugün acıklı bir deneyim
2. Tasarım kısıtlamaları: tek DLL, tek satır, Node.js yok
3. `<EmbeddedResource>` ile static asset'leri DLL'e gömmek
4. Blazor WebAssembly yerine vanilla JS — neden
5. ClickHouse `EXPLAIN` çıktısını ağaç yapısına çevirmek
6. ClickHouse 26'nın bana öğrettiği alias bug'ı
7. Sonraki adımlar — Pro edition, multi-cluster

---

## 9. ClickHouse community Slack — `#community` kanalı

**Mesaj:**

```
👋 Hey folks — built a small open-source thing I figured this channel might
find useful.

ClickHouseUI is a Hangfire-style web dashboard for ClickHouse, distributed
as a single NuGet package for ASP.NET Core. One line in Program.cs:

    app.UseClickHouseDashboard("Host=...;Port=8123;...");

You get live metrics from system.metrics/events, a table & parts explorer,
slow queries from system.query_log, and a visual EXPLAIN tree.

GitHub: https://github.com/erkerkan/clickhouse-ui
MIT licensed, .NET 8, ClickHouse 22.3+.

Read-only by design. Auth-agnostic.

If anyone uses .NET and ClickHouse together I'd love feedback. Bug reports
and feature requests welcome.
```

---

## 10. Awesome-list PRs

**For awesome-clickhouse** ([github.com/Altinity/awesome-clickhouse](https://github.com/Altinity/awesome-clickhouse)) — find the relevant section (`Tools` / `GUI` / `Monitoring`) and add:

```markdown
* [ClickHouseUI](https://github.com/erkerkan/clickhouse-ui) - Drop-in
  ASP.NET Core middleware dashboard. Live metrics, table explorer, slow
  queries and visual EXPLAIN, in a single NuGet package. MIT-licensed.
```

**For awesome-dotnet** ([github.com/quozd/awesome-dotnet](https://github.com/quozd/awesome-dotnet)) — `Database` section:

```markdown
* [ClickHouseUI](https://github.com/erkerkan/clickhouse-ui) - Drop-in
  ASP.NET Core middleware that adds a ClickHouse dashboard (metrics,
  tables, slow queries, visual EXPLAIN) with one line of code.
```

**For awesome-aspnetcore** ([github.com/jasonhua95/awesome-aspnetcore](https://github.com/jasonhua95/awesome-aspnetcore)) — `Tools` section:

```markdown
* [ClickHouseUI](https://github.com/erkerkan/clickhouse-ui) - One-line
  embeddable ClickHouse dashboard middleware. Hangfire-style UX, single
  NuGet, no Node.js build step.
```

---

## Timing recommendation

Day 1 (Mon–Wed): Twitter thread + LinkedIn EN + LinkedIn TR
Day 2: Reddit r/dotnet
Day 3: Hacker News "Show HN" (best traffic peak — make sure repo looks polished)
Day 4: Reddit r/clickhouse + ClickHouse Slack
Week 2: dev.to + Devnot article + awesome-* PRs

Don't post everywhere on the same day — looks spammy and you can't react to feedback on five threads at once.
