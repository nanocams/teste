using DataIngestion.Aplication.Interfaces;
using DataIngestion.Aplication.Services;
using DataIngestion.Infrastructure.ApiClients;
using DataIngestion.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();

//--------------------------------- new ---------------------------------
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Application
        services.AddScoped<IPriceIngestionService, PriceIngestionService>();
      //  services.AddScoped<ISymbolIngestionService, SymbolIngestionService>();
        // Infrastructure
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<ITwelveDataClient, TwelveDataClient>();



       

        // Infrastructure
        services.AddScoped<ISymbolRepository, SymbolRepository>();
        services.AddScoped<IIngestionTriggerClient, IngestionTriggerClient>();

    })
    .Build();

host.Run();
//------------------------------------------------------------------

builder.Build().Run();




