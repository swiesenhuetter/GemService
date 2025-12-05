using GemService;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("WorkerOptions")
);

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();
