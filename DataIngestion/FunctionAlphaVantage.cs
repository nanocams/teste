using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace DataIngestion
{
    public class FunctionAlphaVantage
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        private readonly string _apiKey = Environment.GetEnvironmentVariable("AlphaVantageApiKey");
        private readonly string _alphavantageApiUrl = Environment.GetEnvironmentVariable("AlphavantageApiUrl");
        private readonly string _key = Environment.GetEnvironmentVariable("APIKey");
        private readonly string URL_BASE = Environment.GetEnvironmentVariable("URL_BASE");

        public FunctionAlphaVantage(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FunctionAlphaVantage>();
        }

        [Function("FunctionAlphaVantage")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "FunctionAlphaVantage/{symbol}")] HttpRequestData req,
            string symbol)
        {
            _logger.LogInformation($"Fetching Alpha Vantage overview for: {symbol}");

            var url = $"{_alphavantageApiUrl}/query?function=OVERVIEW&symbol={symbol}&apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch data from Alpha Vantage.");
                return new BadRequestObjectResult("Alpha Vantage API call failed.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var overview = JsonConvert.DeserializeObject<CompanyOverview>(json);

            if (overview == null || string.IsNullOrEmpty(overview.Symbol))
            {
                _logger.LogWarning("No valid data returned from Alpha Vantage.");
                _logger.LogWarning("check data on twelvedata.");

                string UrlStock = URL_BASE + "profile?symbol=" + symbol + "&apikey=" + _key;

                var responseStock = await _httpClient.GetAsync(UrlStock);
                var contentStock = await responseStock.Content.ReadAsStringAsync();

                _logger.LogWarning(contentStock.ToString());

                var dataStock = JsonConvert.DeserializeObject<CompanyOverview>(contentStock);

                if (dataStock == null || string.IsNullOrEmpty(dataStock.Symbol))
                {
                    return new NotFoundObjectResult("Company data not found.");
                }
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                int sectorId = await InsertOrGetId(conn, "sectors", "designation", overview.Sector);
                int industryId = await InsertOrGetId(conn, "industries", "designation", overview.Industry);

                var stockCmd = new SqlCommand(
                    "INSERT INTO stocks (symbol, name, sector_id, industry_id, description) " +
                    "VALUES (@symbol, @name, @sector_id, @industry_id,@description)",
                    conn);

                stockCmd.Parameters.AddWithValue("@symbol", overview.Symbol);
                stockCmd.Parameters.AddWithValue("@name", overview.Name ?? "");
                stockCmd.Parameters.AddWithValue("@sector_id", sectorId);
                stockCmd.Parameters.AddWithValue("@industry_id", industryId);
                stockCmd.Parameters.AddWithValue("@description", overview.Description ?? "");

                await stockCmd.ExecuteNonQueryAsync();
            }

            return new OkObjectResult($"Company data for {symbol} inserted successfully.");
        }

        private async Task<int> InsertOrGetId(SqlConnection conn, string table, string column, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;

            var checkCmd = new SqlCommand($"SELECT id FROM {table} WHERE {column} = @value", conn);
            checkCmd.Parameters.AddWithValue("@value", value);
            var result = await checkCmd.ExecuteScalarAsync();

            if (result != null && int.TryParse(result.ToString(), out int id))
                return id;

            var insertCmd = new SqlCommand($"INSERT INTO {table} ({column}) VALUES (@value); SELECT SCOPE_IDENTITY();", conn);
            insertCmd.Parameters.AddWithValue("@value", value);
            var newId = await insertCmd.ExecuteScalarAsync();
            return newId != null ? Convert.ToInt32(newId) : 0;
        }

        public class CompanyOverview
        {
            public string Symbol { get; set; }
            public string Name { get; set; }
            public string Sector { get; set; }
            public string Industry { get; set; }
            public string Description { get; set; }
        }
    }
}