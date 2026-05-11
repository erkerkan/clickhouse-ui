-- =============================================================================
-- ClickHouseUI demo seed
--
-- Populates a fresh `demo` database with a mix of synthetic + real-world tables
-- and runs a batch of warm-up queries so the dashboard's Slow Queries / Live
-- Metrics views have something interesting to show.
--
-- Usage (clickhouse-client):
--   clickhouse-client --host localhost --queries-file samples/seed/seed.sql
--
-- Usage (HTTP, curl):
--   curl -X POST 'http://localhost:8123/' --data-binary @samples/seed/seed.sql
--
-- Total disk footprint: ~1-2 GB. Time: ~1-3 min depending on network.
-- =============================================================================

CREATE DATABASE IF NOT EXISTS demo;

-- -----------------------------------------------------------------------------
-- 1. Synthetic tables via generateRandom() - instant, no network required
-- -----------------------------------------------------------------------------

DROP TABLE IF EXISTS demo.events;
CREATE TABLE demo.events
(
    event_id       UUID,
    event_time     DateTime DEFAULT now(),
    event_type     LowCardinality(String),
    user_id        UInt64,
    session_id     UUID,
    country        LowCardinality(String),
    device         LowCardinality(String),
    browser        LowCardinality(String),
    properties     Map(String, String),
    revenue_cents  UInt32
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(event_time)
ORDER BY (event_type, user_id, event_time);

INSERT INTO demo.events
SELECT
    generateUUIDv4()                                                                       AS event_id,
    now() - INTERVAL number SECOND                                                         AS event_time,
    arrayElement(['view','click','purchase','signup','login','logout','add_to_cart','share'], 1 + (number % 8)) AS event_type,
    1000 + (number % 50000)                                                                AS user_id,
    generateUUIDv4()                                                                       AS session_id,
    arrayElement(['TR','US','DE','UK','FR','NL','BR','IN','JP','AU','CA'], 1 + (number % 11)) AS country,
    arrayElement(['mobile','desktop','tablet'], 1 + (number % 3))                          AS device,
    arrayElement(['chrome','firefox','safari','edge','opera'], 1 + (number % 5))           AS browser,
    map('utm_source', 'seed', 'page', concat('/p/', toString(number % 200)))               AS properties,
    if(number % 23 = 0, toUInt32(rand() % 10000), toUInt32(0))                             AS revenue_cents
FROM numbers(5_000_000);

DROP TABLE IF EXISTS demo.users;
CREATE TABLE demo.users
(
    user_id     UInt64,
    created_at  DateTime,
    email       String,
    plan        LowCardinality(String),
    country     LowCardinality(String),
    is_active   UInt8
)
ENGINE = MergeTree
ORDER BY user_id;

INSERT INTO demo.users
SELECT
    1000 + number                                                                          AS user_id,
    now() - INTERVAL (rand() % 7776000) SECOND                                             AS created_at,
    concat('user_', toString(number), '@example.com')                                      AS email,
    arrayElement(['free','starter','pro','enterprise'], 1 + (number % 4))                  AS plan,
    arrayElement(['TR','US','DE','UK','FR','NL','BR','IN','JP','AU','CA'], 1 + (number % 11)) AS country,
    if(number % 10 = 0, toUInt8(0), toUInt8(1))                                            AS is_active
FROM numbers(250_000);

DROP TABLE IF EXISTS demo.orders;
CREATE TABLE demo.orders
(
    order_id     UInt64,
    user_id      UInt64,
    created_at   DateTime,
    status       LowCardinality(String),
    currency     LowCardinality(String),
    amount_cents UInt32,
    line_count   UInt8
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(created_at)
ORDER BY (user_id, created_at);

INSERT INTO demo.orders
SELECT
    number                                                                                 AS order_id,
    1000 + (number % 250_000)                                                              AS user_id,
    now() - INTERVAL (number % 7776000) SECOND                                             AS created_at,
    arrayElement(['pending','paid','shipped','delivered','refunded','cancelled'], 1 + (number % 6)) AS status,
    arrayElement(['TRY','USD','EUR','GBP'], 1 + (number % 4))                              AS currency,
    toUInt32(500 + rand() % 50000)                                                         AS amount_cents,
    toUInt8(1 + (number % 5))                                                              AS line_count
FROM numbers(2_000_000);

DROP TABLE IF EXISTS demo.logs;
CREATE TABLE demo.logs
(
    ts        DateTime64(3),
    level     LowCardinality(String),
    service   LowCardinality(String),
    host      LowCardinality(String),
    message   String,
    trace_id  UUID
)
ENGINE = MergeTree
PARTITION BY toYYYYMMDD(ts)
ORDER BY (service, ts);

INSERT INTO demo.logs
SELECT
    now64(3) - INTERVAL (number % 604800) SECOND                                           AS ts,
    arrayElement(['INFO','INFO','INFO','INFO','WARN','ERROR','DEBUG'], 1 + (number % 7))   AS level,
    arrayElement(['api','worker','scheduler','auth','billing','search'], 1 + (number % 6)) AS service,
    concat('host-', toString(number % 20))                                                 AS host,
    concat('Request ', toString(number), ' processed in ', toString(rand() % 500), 'ms')   AS message,
    generateUUIDv4()                                                                       AS trace_id
FROM numbers(3_000_000);

DROP TABLE IF EXISTS demo.metrics_5m;
CREATE TABLE demo.metrics_5m
(
    bucket    DateTime,
    service   LowCardinality(String),
    metric    LowCardinality(String),
    value     Float64,
    p50       Float64,
    p95       Float64,
    p99       Float64
)
ENGINE = MergeTree
ORDER BY (service, metric, bucket);

INSERT INTO demo.metrics_5m
SELECT
    toStartOfInterval(now() - INTERVAL (number * 300) SECOND, INTERVAL 5 MINUTE)           AS bucket,
    arrayElement(['api','worker','db','cache','queue'], 1 + (number % 5))                  AS service,
    arrayElement(['latency_ms','rps','error_rate','cpu','memory'], 1 + (number % 5))       AS metric,
    rand() % 1000 / 10                                                                     AS value,
    rand() % 500 / 10                                                                      AS p50,
    rand() % 1500 / 10                                                                     AS p95,
    rand() % 3000 / 10                                                                     AS p99
FROM numbers(500_000);

-- -----------------------------------------------------------------------------
-- 2. (Optional) Real public dataset: UK property prices (~27M rows, ~600 MB)
-- Comment out if you don't want the S3 download. Takes 1-3 min over good link.
-- -----------------------------------------------------------------------------

DROP TABLE IF EXISTS demo.uk_price_paid;
CREATE TABLE demo.uk_price_paid
(
    price       UInt32,
    date        Date,
    postcode1   LowCardinality(String),
    postcode2   LowCardinality(String),
    type        Enum8('terraced'=1, 'semi-detached'=2, 'detached'=3, 'flat'=4, 'other'=0),
    is_new      UInt8,
    duration    Enum8('freehold'=1, 'leasehold'=2, 'unknown'=0),
    addr1       String,
    addr2       String,
    street      LowCardinality(String),
    locality    LowCardinality(String),
    town        LowCardinality(String),
    district    LowCardinality(String),
    county      LowCardinality(String)
)
ENGINE = MergeTree
ORDER BY (postcode1, postcode2, addr1, addr2);

INSERT INTO demo.uk_price_paid
SELECT
    toUInt32(price_string)                                          AS price,
    parseDateTimeBestEffortUSOrZero(time)::Date                     AS date,
    splitByChar(' ', postcode)[1]                                   AS postcode1,
    splitByChar(' ', postcode)[2]                                   AS postcode2,
    transform(a, ['T','S','D','F','O'], ['terraced','semi-detached','detached','flat','other']) AS type,
    b = 'Y'                                                         AS is_new,
    transform(c, ['F','L','U'], ['freehold','leasehold','unknown']) AS duration,
    addr1, addr2, street, locality, town, district, county
FROM url(
    'http://prod.publicdata.landregistry.gov.uk.s3-website-eu-west-1.amazonaws.com/pp-complete.csv',
    'CSV',
    'uuid_string String, price_string String, time String, postcode String, a String, b String, c String,
     addr1 String, addr2 String, street String, locality String, town String, district String, county String,
     d String, e String'
)
SETTINGS max_http_get_redirects = 10, input_format_allow_errors_num = 1000;

-- -----------------------------------------------------------------------------
-- 3. Warm-up queries - populate system.query_log with varied durations.
-- Some intentionally heavy so the Slow Queries view has 1-3 outliers.
-- -----------------------------------------------------------------------------

SELECT count() FROM demo.events;
SELECT count() FROM demo.users;
SELECT count() FROM demo.orders;
SELECT count() FROM demo.logs;
SELECT count() FROM demo.metrics_5m;

SELECT event_type, count() AS c FROM demo.events GROUP BY event_type ORDER BY c DESC;
SELECT country, uniqExact(user_id) AS uu FROM demo.events GROUP BY country ORDER BY uu DESC;
SELECT toDate(event_time) AS d, count() FROM demo.events GROUP BY d ORDER BY d;
SELECT browser, device, count() FROM demo.events GROUP BY browser, device ORDER BY 3 DESC;
SELECT status, count(), sum(amount_cents) / 100 AS revenue FROM demo.orders GROUP BY status;
SELECT plan, count() FROM demo.users GROUP BY plan;
SELECT service, level, count() FROM demo.logs GROUP BY service, level ORDER BY 3 DESC;
SELECT service, metric, avg(p99) FROM demo.metrics_5m GROUP BY service, metric ORDER BY 3 DESC LIMIT 20;

-- A few deliberately heavier queries (sortBy + uniqExact + cross-table join)
SELECT u.country, count() AS orders, sum(o.amount_cents)/100 AS revenue
FROM demo.orders o
JOIN demo.users u USING user_id
GROUP BY u.country
ORDER BY revenue DESC;

SELECT e.event_type, u.plan, uniqExact(e.user_id) AS users
FROM demo.events e
JOIN demo.users u USING user_id
GROUP BY e.event_type, u.plan
ORDER BY users DESC;

SELECT
    toStartOfHour(event_time) AS h,
    country,
    count() AS c,
    uniqExact(user_id) AS uu
FROM demo.events
WHERE event_time > now() - INTERVAL 7 DAY
GROUP BY h, country
ORDER BY h, country
LIMIT 100;

-- Force at least one >1s slow query (sort over all 5M events, no index help)
SELECT event_id, event_time, user_id
FROM demo.events
ORDER BY revenue_cents DESC, event_time ASC
LIMIT 50;

SELECT toString(properties) AS p, count() FROM demo.events GROUP BY p ORDER BY 2 DESC LIMIT 10;

OPTIMIZE TABLE demo.events FINAL;
OPTIMIZE TABLE demo.orders FINAL;
