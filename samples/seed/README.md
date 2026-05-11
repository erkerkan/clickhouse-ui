# Demo seed data

A self-contained script that populates a fresh `demo` database with realistic
synthetic data plus an optional public dataset, then runs a batch of warm-up
queries so every tab of ClickHouseUI has something interesting to show.

## What you get

| Table | Rows | Source | Purpose |
|---|---|---|---|
| `demo.events` | 5 M | `generateRandom` | Largest table — drives Live Metrics & Tables view |
| `demo.users` | 250 K | `generateRandom` | Join target |
| `demo.orders` | 2 M | `generateRandom` | Partitioned, multiple parts |
| `demo.logs` | 3 M | `generateRandom` | Daily-partitioned, many parts |
| `demo.metrics_5m` | 500 K | `generateRandom` | Pre-aggregated example |
| `demo.uk_price_paid` | ~27 M | UK Land Registry (HTTP) | Real public dataset, biggest table |

After the inserts the script also runs ~20 analytical queries (group-by, joins,
sorts, uniqExact) so `system.query_log` is populated and the **Slow Queries**
view has at least a few interesting outliers.

Total disk footprint: ~1-2 GB.
Total wall-clock time: ~1-3 min depending on your network for the UK dataset.

## Run it

### Option A — PowerShell (no extra tools required)

```powershell
cd samples/seed
./seed.ps1 -Url http://localhost:8123
# or against a remote server:
./seed.ps1 -Url http://clickhouse.internal:8123 -User default -Password ''
```

### Option B — clickhouse-client

```bash
clickhouse-client --host localhost --queries-file samples/seed/seed.sql
```

### Option C — curl

```bash
# Note: HTTP requires --data-binary or it will strip newlines.
curl -X POST 'http://localhost:8123/?multi_statements=1' \
     --data-binary @samples/seed/seed.sql
```

## Skip the S3 download

If you don't need the UK Property Prices dataset (e.g. no internet on the box),
just delete the `CREATE TABLE demo.uk_price_paid` block and the matching
`INSERT` from `seed.sql`. The rest of the script is fully offline.

## Reset / re-run

The script uses `DROP TABLE IF EXISTS` for each table, so you can run it
repeatedly to start clean.

```sql
DROP DATABASE IF EXISTS demo;
```
