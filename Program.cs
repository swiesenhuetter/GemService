using GemService;
using Serilog;


var builder = Host.CreateApplicationBuilder(args);

Environment.SetEnvironmentVariable("APP_BASE_DIRECTORY", AppContext.BaseDirectory);

builder.Services.AddSerilog((services, lc) => lc
.ReadFrom.Configuration(builder.Configuration)
.ReadFrom.Services(services)
.MinimumLevel.Information()
.WriteTo.Console());

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("WorkerOptions")
);

builder.Services.AddHostedService<Worker>();

// Only add Windows service when NOT debugging
if (!args.Contains("--console"))
{
    builder.Services.AddWindowsService();
}

var host = builder.Build();
host.Run();
