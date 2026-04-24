using Azure;
using InternalAPI.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace InternalAPI;

public class TriggerPrevisionPrice
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    //private readonly string _previsionPriceUrl = Environment.GetEnvironmentVariable("PREVISIONPRICE_URL");
    //private readonly string _previsionPriceCode = Environment.GetEnvironmentVariable("PREVISIONPRICE_CODE"); 

    public TriggerPrevisionPrice(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TriggerPrevisionPrice>();
    }

    [Function("TriggerPrevisionPrice")]
    public async Task Run([TimerTrigger("%SCHEDULETIMERTRIGGER_PREVISION%")] TimerInfo myTimer)
    {
        _logger.LogInformation("Start prevision, Timer trigger function executed at: {executionTime}", DateTime.Now);
        
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
       
        List<string> symbols = await SearchSymbolsExisted();

        foreach (string symbol in symbols)
        {
            await TriggerFunctionPrevisionPrice(symbol);
        }
        
        _logger.LogInformation("Close prevision, Timer trigger function executed at: " + DateTime.Now);

    }

    private async Task TriggerFunctionPrevisionPrice(string symbol)
    {
        try
        {
            var PredictionURLMLNET = Environment.GetEnvironmentVariable("PredictionsMLNETURL");
            var PredictionKETMLNET = Environment.GetEnvironmentVariable("PredictionsMLNETURLKey");
            _logger.LogInformation("Start Prediction call.");

            var PredictionUrl = $"{PredictionURLMLNET}symbol={symbol.ToUpper()}&code={PredictionKETMLNET}";
            var PredictionResp = await _httpClient.GetAsync(PredictionUrl);
            _logger.LogInformation("Prediction call response - " + PredictionResp.StatusCode);

            if (PredictionResp.IsSuccessStatusCode)
            {
                //var PredictionJson = await PredictionResp.Content.ReadAsStringAsync();
                _logger.LogInformation("Insert made to prevision ");
                //PredictionObject = JsonConvert.DeserializeObject<PredictionResponse>(PredictionJson);
            }
            else
            {
                _logger.LogInformation($"Failed to Insert: {PredictionResp.StatusCode}");
            }
        }
        catch (TaskCanceledException taskCancel)
        {
            _logger.LogInformation("call timeout");
            _logger.LogInformation(taskCancel.Message);
        }
        catch (Exception expred)
        {
            _logger.LogInformation(expred.Message);
        }

        //var url_prevision = $"{_previsionPriceUrl}?symbol={symbol}&top={top}&code={_previsionPriceCode}";
        //try
        //{
        //    var response = await _httpClient.GetAsync(url_prevision);
        //    if (response.IsSuccessStatusCode)
        //    {
        //        log.LogInformation($"Insert made to prevision ");
        //    }
        //    else
        //    {
        //        log.LogWarning($"Failed to Insert: {response.StatusCode}");
        //    }
        //}
        //catch (Exception ex)
        //{
        //    log.LogError($"Error calling function: {ex.Message}");
        //}
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
}