using DataIngestion.Aplication.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DataIngestion.Functions;

public class TwelveDataFunction
{
    private readonly ILogger<TwelveDataFunction> _logger;
    private readonly IPriceIngestionService _priceIngestionService;

    public TwelveDataFunction(IPriceIngestionService priceIngestionService, ILogger<TwelveDataFunction> logger)
    {
        _logger = logger;
        _priceIngestionService = priceIngestionService;
    }

    [Function("TwelveDataFunction")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {

        var symbol = req.Query["symbol"];
        var interval = req.Query["interval"] ?? "1day";

        if (string.IsNullOrEmpty(symbol))
            return new BadRequestObjectResult("Missing symbol.");

        await _priceIngestionService.IngestPricesAsync(symbol, interval);

        return new OkObjectResult("Prices ingestion ingested.");

    }
}