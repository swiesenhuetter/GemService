using GemService;
using Serilog;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, lc) => lc
.ReadFrom.Configuration(builder.Configuration)
.ReadFrom.Services(services));

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("WorkerOptions")
);

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();
