using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;

namespace DataIngestion;

public class FunctionTwelveData
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _twelveDataUrl = Environment.GetEnvironmentVariable("ApiUrl");
    private readonly string _twelveApiKey = Environment.GetEnvironmentVariable("APIKey");
    private readonly string _alphaVantageUrl = Environment.GetEnvironmentVariable("AlphaVantageFunctionUrl");
    private readonly string _alphaVantageFunctionSecret = Environment.GetEnvironmentVariable("AlphaVantageFunctionSecret");

    public FunctionTwelveData(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<FunctionTwelveData>();
    }

    [Function("FunctionTwelveData")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequestData req)
    {
        string symbol = req.Query["symbol"];
        string interval = req.Query["interval"] ?? "1day";
        _logger.LogInformation("Symbol selected Ingest: " + symbol);
        _logger.LogInformation("Interval " + interval);

        if (string.IsNullOrEmpty(symbol))
        {
            return new BadRequestObjectResult("Missing symbol parameter.");
        }

        symbol = symbol.ToUpper();

        try
        {
            int stockId = await GetStockId(symbol);
            _logger.LogInformation("StockID " + stockId);
            if (stockId == 0)
            {
                _logger.LogInformation("Stock not found. Calling Alpha Vantage Function.");

                await TriggerAlphaVantageAsync(symbol);

                // Retry to confirm the stock was inserted into DB
                int retries = 3;
                while (retries-- > 0)
                {
                    await Task.Delay(1000);
                    stockId = await GetStockId(symbol);
                    if (stockId != 0) break;
                }

                if (stockId == 0)
                {
                    _logger.LogError("Stock creation failed even after AlphaVantage call and retries.");
                    return new BadRequestObjectResult("Stock creation failed after AlphaVantage call.");
                }
            }

            // Get the latest price date from DB
            DateTime? lastDate = await GetLastPriceDate(stockId);
            // Get the latest price date from DB
            DateTime? firstDate = await GetFirstPriceDate(stockId);
            _logger.LogInformation("Call historical data.");
            // Build the request URL for historical data
            string url = $"{_twelveDataUrl}/time_series?symbol={symbol}&interval={interval}&outputsize=130&apikey={_twelveApiKey}";
            _logger.LogInformation("url: " + url);
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Receive response.");
            var parsed = JsonConvert.DeserializeObject<TwelveDataResponse>(json);
            if (parsed?.Values == null)
                return new BadRequestObjectResult("Failed to retrieve data from TwelveData.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            foreach (var item in parsed.Values)
            {
                if (!DateTime.TryParse(item.datetime, out DateTime date))
                    continue;

                     if (lastDate.HasValue && date <= lastDate.Value)
              //  if (lastDate.HasValue && date >= firstDate.Value)
                    continue;

                var cmd = new SqlCommand("INSERT INTO prices ([date], [open], [high], [low], [close], volume, stock_id, provider_id) VALUES (@date, @open, @high, @low, @close, @volume, @stock_id, 1)", conn);
                cmd.Parameters.AddWithValue("@date", date);
                cmd.Parameters.AddWithValue("@open", decimal.Parse(item.open, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@high", decimal.Parse(item.high, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@low", decimal.Parse(item.low, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@close", decimal.Parse(item.close, CultureInfo.InvariantCulture));
                if (!long.TryParse(item.volume, out long volume))
                {
                    _logger.LogWarning("Invalid volume '{volumeStr}' for {symbol} at {date}", item.volume, symbol, item.datetime);
                    continue;
                }
                cmd.Parameters.AddWithValue("@volume", volume);
                cmd.Parameters.AddWithValue("@stock_id", stockId);
                await cmd.ExecuteNonQueryAsync();
            }

            return new OkObjectResult("Prices ingested successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in FunctionTwelveData");
            return new ObjectResult("Internal server error.") { StatusCode = 500 };
        }
    }

    private async Task<int> GetStockId(string symbol)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT id FROM stocks WHERE symbol = @symbol", conn);
        cmd.Parameters.AddWithValue("@symbol", symbol);
        var result = await cmd.ExecuteScalarAsync();
        return result != null && int.TryParse(result.ToString(), out var id) ? id : 0;
    }

    private async Task<DateTime?> GetLastPriceDate(int stockId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT MAX([date]) FROM prices WHERE stock_id = @stock_id", conn);
        cmd.Parameters.AddWithValue("@stock_id", stockId);
        var result = await cmd.ExecuteScalarAsync();
        return result != DBNull.Value && result != null ? (DateTime?)result : null;
    }
    private async Task<DateTime?> GetFirstPriceDate(int stockId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT MIN([date]) FROM prices WHERE stock_id = @stock_id", conn);
        cmd.Parameters.AddWithValue("@stock_id", stockId);
        var result = await cmd.ExecuteScalarAsync();
        return result != DBNull.Value && result != null ? (DateTime?)result : null;
    }

    private async Task TriggerAlphaVantageAsync(string symbol)
    {
        var url = $"{_alphaVantageUrl}/{symbol}?code=" + _alphaVantageFunctionSecret;
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AlphaVantage ingestion failed for symbol: {symbol}", symbol);
        }
    }

    public class TwelveDataResponse
    {
        public DataMeta? Meta { get; set; }
        public List<PriceData>? Values { get; set; }
    }

    public struct DataMeta
    {
        public string Symbol { get; set; }
        public string Interval { get; set; }
        public string currency { get; set; }
        public string exchange_timezone { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string type { get; set; }

    }

    public class PriceData
    {
        public string datetime { get; set; }
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string volume { get; set; }
    }
}
