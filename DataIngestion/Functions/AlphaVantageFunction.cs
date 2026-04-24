using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DataIngestion.Functions;

public class AlphaVantageFunction
{
    private readonly ILogger<AlphaVantageFunction> _logger;

    public AlphaVantageFunction(ILogger<AlphaVantageFunction> logger)
    {
        _logger = logger;
    }

    [Function("AlphaVantageFunction")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}