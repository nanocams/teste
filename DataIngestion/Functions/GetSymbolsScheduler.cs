using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DataIngestion.Functions;

public class GetSymbolsScheduler
{
    private readonly ILogger _logger;
    private readonly ISymbolIngestionService _service;

    public GetSymbolsScheduler(ILoggerFactory loggerFactory, ISymbolIngestionService service)
    {
        _logger = loggerFactory.CreateLogger<GetSymbolsScheduler>();
        _service = service;
    }

    [Function("GetSymbolsScheduler")]
    public async Task Run([TimerTrigger("%FunctionSchedule%")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);
        
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        await _service.IngestSymbolsAsync();
        _logger.LogInformation("Symbol ingestion scheduler finished.");
    }
}