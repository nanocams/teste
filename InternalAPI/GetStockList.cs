using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net;
using static InternalAPI.PrevisionsPrice;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InternalAPI
{
    public class GetStockList
    {
        private readonly ILogger _logger;
        private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        public GetStockList(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetStockQuote>();
        }


        [Function("GetStockList")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetStockList/")] HttpRequestData req)
        {
            _logger.LogInformation("Received request for list");

            try
            {
                var stockList = await GetListStocks();

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

        private async Task<object> GetListStocks()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT * FROM stocks", conn);
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
                            description = reader["description"]
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
