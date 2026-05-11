using ClickHouseUI;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/clickhouse"));

app.UseClickHouseDashboard("/clickhouse", options =>
{
    options.ConnectionString =
        builder.Configuration.GetConnectionString("ClickHouse")
        ?? "Host=localhost;Port=8123;User=default;Database=default";
    options.Title = "ClickHouseUI Sample";
});

app.Run();
