using OutOfMemoryWorkbook.Services;

if (QueryMiniExcelBenchmarkCommand.WasRequested(args))
{
    Environment.ExitCode = await QueryMiniExcelBenchmarkCommand.ExecuteAsync(
        args,
        Directory.GetCurrentDirectory());
    return;
}

if (ExportBenchmarkCommand.WasRequested(args))
{
    Environment.ExitCode = await ExportBenchmarkCommand.ExecuteAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IInventoryDataSource, InventoryDataSource>();
builder.Services.AddSingleton<IInventoryExportService, InventoryExportService>();
builder.Services.AddSingleton<IExportMeasurementService, ExportMeasurementService>();
builder.Services.AddSingleton<IQueryMiniExcelBenchmarkService>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var databasePath = Path.Combine(
        environment.ContentRootPath,
        "work",
        "query-benchmark.db");

    return new QueryMiniExcelBenchmarkService(databasePath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
