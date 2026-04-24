using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


class Program
{
    static void Main()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices((context, services) =>
            {
                services
                 .AddApplicationInsightsTelemetryWorkerService()
                 .ConfigureFunctionsApplicationInsights()
                 .AddHttpClient();
            })
            .Build();

        host.Run();
    }
}

//var builder = FunctionsApplication.CreateBuilder(args);

//builder.ConfigureFunctionsWebApplication();

//builder.Services
//    .AddApplicationInsightsTelemetryWorkerService()
//    .ConfigureFunctionsApplicationInsights();

//builder.Services.AddHttpClient();

//builder.Build().Run();
