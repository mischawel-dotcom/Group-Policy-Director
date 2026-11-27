using GroupPolicyDirector.Infrastructure;
using GroupPolicyDirector.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Allow running as a Windows Service when installed via MSI / Setup.ps1.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GroupPolicyDirectorWorker";
});

builder.Services.AddGpdInfrastructure();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
