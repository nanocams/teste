using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;

namespace InternalAPI
{
    public class GetStockListHist
    {
        private readonly ILogger _logger;
        private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        public GetStockListHist(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetStockQuote>();
        }


        [Function("GetStockListHist")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetStockListHist/{symbol}")] HttpRequestData req, string symbol)
        {
            _logger.LogInformation("Received request for list");

            try
            {
                var stockList = await GetListStocksHist(symbol);

                // monta resposta
                var responseObj = new
                {
                    stockList = stockList
                };

                var responseOk = req.CreateResponse(HttpStatusCode.OK);
                await responseOk.WriteAsJsonAsync(responseObj);
                return responseOk;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Internal server error.");
                return errorResponse;
            }
        }


        private async Task<object> GetListStocksHist(string symbol)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand($"""
                SELECT
                
                        stocks.symbol,
                        stocks.name,
                        previsions.price as expected,
                        prices.[close],
                        cast(previsions.[prevision_date] as date) as prevision_date
                    FROM
                        previsions,
                        stocks,
                        prices
                    WHERE
                        previsions.stock_id = stocks.id AND
                        prices.stock_id = previsions.stock_id AND
                        cast(previsions.[prevision_date] as date) = cast(prices.[date] as date) AND
                        ('{symbol}' = '_ALL' OR stocks.symbol = '{symbol}')
                """, conn);

            var response = new List<object>();

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                int i = 0;
                while (await reader.ReadAsync())
                {
                    try
                    {
                        response.Add(new
                        {
                            symbol = reader["symbol"]?.ToString() ?? "",
                            name = reader["name"]?.ToString() ?? "",
                            price = reader["expected"],
                            close = reader["close"],
                            date = reader["prevision_date"]
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Erro ao ler linha da base de dados: {ex.Message}");
                    }

                }
            }

            return JsonConvert.SerializeObject(response, Formatting.Indented);
        }
    }
}
