# Changelog

All notable changes to this project are documented in this file. The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] — 2026-05-11

### Fixed

- README rendering on NuGet.org: screenshots and license badge use absolute `raw.githubusercontent.com` URLs so they render correctly inside the package details page. The HTML `<table>` grid was replaced with plain Markdown for the same reason.

### Notes

No code changes versus 0.1.0 — this is a docs-only release. The middleware and frontend are byte-identical.

## [0.1.0] — 2026-05-11

### Added

- Initial public release.
- Single-line ASP.NET Core middleware: `app.UseClickHouseDashboard(connectionString)`.
- **Overview** tab — server version, uptime, disks, configured clusters.
- **Live Metrics** tab — 2-second polling charts for query / insert / select rate, memory tracking, and network throughput, sourced from `system.metrics` + `system.events` + `system.asynchronous_metrics`.
- **Tables & Parts** tab — every user table sorted by disk size, with compression ratio and part count, plus a one-click drill-down into individual MergeTree parts.
- **Slow Queries** tab — top finished queries from `system.query_log` in the last *N* hours.
- **Visual EXPLAIN** tab — interactive tree view for `EXPLAIN PLAN / PIPELINE / INDEXES / SYNTAX`.
- Embedded frontend (HTML, CSS, vanilla JS, Chart.js) shipped inside the assembly — no `UseStaticFiles`, no Node.js build step.
- Custom auth predicate via `ClickHouseDashboardOptions.Authorize`.
- Demo seed (`samples/seed/seed.sql` + `seed.ps1`) covering five `generateRandom` tables and the UK Land Registry public dataset.

### Fixed

- `Tables & Parts` query no longer trips `ILLEGAL_AGGREGATION` on ClickHouse 25.x / 26.x by renaming aggregate aliases away from their source column names.
- Numeric column headers are now right-aligned alongside their values.

### Security

- Dashboard authorization defaults: when `AllowAnonymous=false` and no predicate is supplied, the middleware now requires an authenticated user instead of returning 403 for everyone.
- Base-path matching no longer leaks `/clickhouse-other` into `/clickhouse`.

[Unreleased]: https://github.com/erkerkan/clickhouse-ui/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/erkerkan/clickhouse-ui/releases/tag/v0.1.1
[0.1.0]: https://github.com/erkerkan/clickhouse-ui/releases/tag/v0.1.0
