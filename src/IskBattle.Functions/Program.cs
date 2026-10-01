using IskBattle.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.ConfigureFunctionsApplicationInsights();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
