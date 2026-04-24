using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DataIngestion;

public class UpdateDataProviders
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _dataIngestionUrl = Environment.GetEnvironmentVariable("DataIngestionUrl");
    private readonly string _dataIngestionCode = Environment.GetEnvironmentVariable("FunctionTwelveDataCode");

    public UpdateDataProviders(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<UpdateDataProviders>();
    }

    // esta Função permite atualizar os dados na base de dados de segunda à sexta-feira as 21h45 
    [Function("UpdateDataProviders")]
    public async Task RunAsync([TimerTrigger("0 45 21 * * 1-5")] TimerInfo myTimer)
    {
        _logger.LogInformation("Update Data Provider, Timer trigger function executed at: {executionTime}", DateTime.Now);

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        List<string> symbols = await SearchSymbolsExisted();
        _logger.LogInformation("Symbols exist : " + symbols.Count());

        foreach (string symbol in symbols) {
            _logger.LogInformation("Symbol : " + symbol);
            await TriggerFunctionTwelveData(symbol);
        }

        _logger.LogInformation("Close Update Data Provider, Timer trigger function executed at: ", DateTime.Now);

        // prevision of price

    }

    private async Task<List<string>> SearchSymbolsExisted()
    {
        var results = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT symbol FROM stocks", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }


    private async Task TriggerFunctionTwelveData(string symbol)
    {
        var ingestionUrl = $"{_dataIngestionUrl}?symbol={symbol}&interval=1day&code={_dataIngestionCode}";
        try
        {
            var response = await _httpClient.GetAsync(ingestionUrl);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Update made to {symbol}");
            }
            else
            {
                _logger.LogWarning($"Failed to update {symbol}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calling function to {symbol}: {ex.Message}");
        }
    }
}