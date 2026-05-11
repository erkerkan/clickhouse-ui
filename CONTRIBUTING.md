# Contributing to ClickHouseUI

Thanks for considering a contribution! ClickHouseUI is intentionally small and we want to keep it that way, but bug reports, fixes, docs and well-scoped features are very welcome.

## Quick start

```bash
git clone https://github.com/erkerkan/clickhouse-ui
cd clickhouse-ui
dotnet build
dotnet run --project samples/ClickHouseUI.Sample
```

Then open `http://localhost:5188/clickhouse`.

To get some data into the dashboard, run the seed script against your local ClickHouse:

```powershell
./samples/seed/seed.ps1 -Url http://localhost:8123
```

## What we accept

Yes please:

- Bug fixes (with a clear repro in the PR description)
- New small features that work read-only against `system.*` tables
- Performance improvements
- Docs, screenshots, examples
- Translations of the dashboard UI (the strings live in `src/ClickHouseUI/wwwroot/app.js`)

Probably no:

- Anything that breaks the "one NuGet, one line" promise
- Adding a build step / Node toolchain for the frontend
- Renaming or restructuring the public API surface without a discussion issue first
- Features that belong in the Pro edition (Query Killer, schema migration, alerting)

If in doubt, **open an issue describing the change before writing code**. Saves both sides time.

## Coding style

- C# 12, file-scoped namespaces, `var` when the right-hand side makes the type obvious.
- `internal` is the default; keep the public surface minimal.
- Comments should explain *why*, not *what*.
- Add XML docs to anything new on the public API.
- Run `dotnet format` before committing if you touched several files.

## Frontend

- No build step. Vanilla JS + a single embedded Chart.js.
- All files under `src/ClickHouseUI/wwwroot/` are embedded into the DLL by MSBuild — restart the host (no hot reload) when you change them.
- Keep the SPA tiny. Pull-request size is a feature here.

## Testing your change

```bash
dotnet build
dotnet run --project samples/ClickHouseUI.Sample
```

Hit each tab against a ClickHouse with the seed data loaded. If you changed the middleware itself, also verify:

- A path outside the mount (`/`) still flows to the next middleware.
- A path that *starts with* the mount name but isn't actually under it (e.g. `/clickhousex`) does **not** hit the dashboard.
- An invalid `EXPLAIN` query returns 500 + a JSON body, not a YSOD.

## Commits & PRs

- One logical change per commit.
- Commit subjects in the imperative ("Fix off-by-one in part counter", not "Fixed...").
- Reference the issue number in the PR description if one exists.
- The CI workflow must pass.

## License

By contributing, you agree your work is licensed under the [MIT License](LICENSE), the same as the rest of the project.
