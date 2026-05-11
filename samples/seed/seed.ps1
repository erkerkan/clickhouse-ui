<#
.SYNOPSIS
    Executes samples/seed/seed.sql against a ClickHouse HTTP endpoint.

.DESCRIPTION
    Parses the SQL file into individual statements (splitting on top-level
    semicolons) and POSTs each one to the ClickHouse HTTP interface. Works
    without `clickhouse-client` installed locally.

.PARAMETER Url
    ClickHouse HTTP endpoint, e.g. http://localhost:8123 or http://clickhouse.internal:8123

.PARAMETER User
    ClickHouse user. Defaults to 'default'.

.PARAMETER Password
    ClickHouse password. Defaults to empty string.

.EXAMPLE
    ./seed.ps1 -Url http://localhost:8123

.EXAMPLE
    ./seed.ps1 -Url http://clickhouse.internal:8123 -User admin -Password 's3cret'
#>
param(
    [string]$Url = "http://localhost:8123",
    [string]$User = "default",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"
$sqlPath = Join-Path $PSScriptRoot "seed.sql"
if (-not (Test-Path $sqlPath)) { throw "seed.sql not found next to seed.ps1" }

$sql = Get-Content $sqlPath -Raw

# Remove SQL line comments so semicolons inside `-- foo;` don't break splitting.
$sqlClean = ($sql -split "`n" | ForEach-Object { ($_ -split '--')[0] }) -join "`n"

# Split on semicolons that are not inside single-quoted strings.
$statements = New-Object System.Collections.Generic.List[string]
$current = New-Object System.Text.StringBuilder
$inString = $false
foreach ($ch in $sqlClean.ToCharArray()) {
    if ($ch -eq "'") { $inString = -not $inString }
    if ($ch -eq ';' -and -not $inString) {
        $stmt = $current.ToString().Trim()
        if ($stmt.Length -gt 0) { $statements.Add($stmt) }
        $current.Clear() | Out-Null
    } else {
        $current.Append($ch) | Out-Null
    }
}
$tail = $current.ToString().Trim()
if ($tail.Length -gt 0) { $statements.Add($tail) }

$headers = @{ "X-ClickHouse-User" = $User }
if ($Password.Length -gt 0) { $headers["X-ClickHouse-Key"] = $Password }

Write-Host "Seeding $Url - $($statements.Count) statements" -ForegroundColor Cyan

$i = 0
foreach ($stmt in $statements) {
    $i++
    $preview = $stmt -replace "\s+", " "
    if ($preview.Length -gt 90) { $preview = $preview.Substring(0, 87) + "..." }
    Write-Host ("[{0,3}/{1}] {2}" -f $i, $statements.Count, $preview)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $null = Invoke-RestMethod -Uri $Url -Method Post -Body $stmt -Headers $headers -ContentType "text/plain"
        $sw.Stop()
        Write-Host ("       OK ({0} ms)" -f $sw.ElapsedMilliseconds) -ForegroundColor DarkGray
    } catch {
        $sw.Stop()
        $errBody = ""
        if ($_.Exception.Response) {
            try {
                $r = $_.Exception.Response.GetResponseStream()
                $errBody = (New-Object System.IO.StreamReader($r)).ReadToEnd()
            } catch {}
        }
        Write-Host ("       FAIL ({0} ms): {1}" -f $sw.ElapsedMilliseconds, $errBody.Trim()) -ForegroundColor Red
        Write-Host "       (continuing with next statement)" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "Seed complete. Open the ClickHouseUI dashboard to see the data." -ForegroundColor Green
