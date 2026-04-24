using DataBaseManager.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
builder.Services.AddDbContext<AppDbContext>(options =>
{
   // options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnectionString"));
     options.UseSqlServer(connectionString);
});

builder.Build().Run();
