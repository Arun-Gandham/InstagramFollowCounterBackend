using FollowerCounter.Application;
using FollowerCounter.Infrastructure;
using FollowerCounter.Infrastructure.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddBackgroundWorkers(builder.Configuration);

var host = builder.Build();
host.Run();
