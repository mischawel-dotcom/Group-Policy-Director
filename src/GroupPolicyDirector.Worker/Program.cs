using GroupPolicyDirector.Infrastructure;
using GroupPolicyDirector.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGpdInfrastructure();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
