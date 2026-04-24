using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;
using System.Linq;

namespace InternalAPI;

public class PrevisionsPrice
{
    private readonly ILogger<PrevisionsPrice> _logger;
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string apiKey = Environment.GetEnvironmentVariable("API_KEY_ML");
    private readonly string endpoint = Environment.GetEnvironmentVariable("ENDPOINT_ML");

    public PrevisionsPrice(ILogger<PrevisionsPrice> logger)
    {
        _logger = logger;
    }

    [Function("PrevisionsPrice")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string _symbol = req.Query["symbol"];
        int _top = int.Parse(req.Query["top"]);

        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using (var client = new HttpClient(handler))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var dataJson = await JsonDataRrevision(_symbol,_top);
          //  var dataJson = await JsonDataRrevision("AAPL", 2);
            var content = new StringContent(dataJson);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                var response = await client.PostAsync(endpoint, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API call failed: {response.StatusCode}");
                    return new ObjectResult(responseContent) { StatusCode = (int)response.StatusCode };
                }
                // Parse response JSON
                var dates = new List<string>();
                List<OutPrevision> listPrevision = new List<OutPrevision>();

                // Parse o JSON
                var jsonObject = JObject.Parse(dataJson);
                var listValuesPrev = JsonConvert.DeserializeObject<List<decimal>>(responseContent);
                var listInputData = jsonObject["input_data"]["data"].ToObject<List<StockData>>();

                // Obter a data máxima
                var maxDate = listInputData
                    .Select(item => DateTime.Parse(item.date.ToString())).Max();

                DateTime initDate = (maxDate.DayOfWeek == DayOfWeek.Friday)? maxDate.AddDays(3): maxDate.AddDays(1);

                while (dates.Count < listValuesPrev.Count) { 
                    if(initDate.DayOfWeek != DayOfWeek.Saturday && initDate.DayOfWeek != DayOfWeek.Sunday)
                    {
                        dates.Add(initDate.ToString("yyyy-MM-dd"));
                    }
                    initDate = initDate.AddDays(1);
                }

                for (int i = 0; i < listValuesPrev.Count; i++)
                {
                    listPrevision.Add(new OutPrevision
                    {
                        date = dates[i],
                        prevised_value = listValuesPrev[i]
                    });
                }

                string getSymbol = listInputData.Select(i => i.symbol.ToString()).First();
                var stock_id = await GetStockId(getSymbol);
                var createDate = DateTime.UtcNow;
               
                foreach (var item in listPrevision)
                {
                    DateTime? existDate = await iFExistDate(stock_id, item.date);
                    if (!DateTime.TryParse(item.date, out DateTime date))
                        continue;
                    if (existDate.HasValue && date == existDate.Value)
                        continue;
                    using (SqlConnection conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        var query = @"EXEC SP_INS_PREDICTED_PRICE(@prevision_date, @price, @stock_id)";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@prevision_date", item.date);
                            cmd.Parameters.AddWithValue("@price", item.prevised_value);
                            cmd.Parameters.AddWithValue("@stock_id", stock_id);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                return new OkObjectResult("data entered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }

    }
   
    public async Task<string> JsonDataRrevision(string symbol, int top)
    {
        var columns = new List<string> { "date", "open", "high", "low", "volume", "symbol" };
        var index = new List<int>();
        var data = new List<StockData>();

        try
        {
            int stockId = await GetStockId(symbol);
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var query = @"SELECT TOP (@top) p.[date], p.[open], p.[high], p.[low], p.[volume], p.stock_id, s.symbol
                              FROM prices p
                              JOIN stocks s ON p.stock_id = s.id
                              WHERE p.stock_id = @stockId
                              ORDER BY p.[date] DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@stockId", stockId);
                    cmd.Parameters.AddWithValue("@top", top);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        int i = 0;
                        while (await reader.ReadAsync())
                        {
                            try
                            {
                                data.Add(new StockData
                                {
                                    date = Convert.ToDateTime(reader["date"]).ToString("yyyy-MM-dd"),
                                    open = Convert.ToDecimal(reader["open"]),
                                    high = Convert.ToDecimal(reader["high"]),
                                    low = Convert.ToDecimal(reader["low"]),
                                    volume = Convert.ToInt64(reader["volume"]),
                                    symbol = Convert.ToString(reader["symbol"])
                                });
                                index.Add(i++);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Erro ao ler linha da base de dados: {ex.Message}");
                            }

                        }
                    }
                }

            }

            var response = new PrevisionRequest
            {
                input_data = new InputData
                {
                    columns = columns,
                    index = index,
                    data = data
                }
            };

            string json = JsonConvert.SerializeObject(response, Formatting.Indented);
         //   Console.WriteLine(json);
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao gerar JSON: {ex.Message}");
            return null;
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

    private async Task<DateTime?> iFExistDate(int stockId, string date_prev)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT prevision_date FROM previsions WHERE stock_id = @stock_id and prevision_date = @date_prev", conn);
        cmd.Parameters.AddWithValue("@stock_id", stockId);
        cmd.Parameters.AddWithValue("@date_prev", date_prev);
        var result = await cmd.ExecuteScalarAsync();
        return result != DBNull.Value && result != null ? (DateTime?)result : null;
    }

    public static DateTime DataUtil(DateTime currentdata)
    {
       
         if (currentdata.DayOfWeek == DayOfWeek.Saturday)
        {
            return currentdata.AddDays(2);
        }
        else if (currentdata.DayOfWeek == DayOfWeek.Sunday)
        {
            return currentdata.AddDays(1);
        }
        else
        {
            // Caso contrário, retorna o dia atual
            return currentdata;
        }
            
    }

    public class InputData
    {
        public List<string> columns { get; set; }
        public List<int> index { get; set; }
        public List<StockData> data { get; set; }
    }

    public class StockData
    {
        public string date { get; set; }
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public long volume { get; set; }
        public string symbol { get; set; }
    }

    public class PrevisionRequest
    {
        public InputData input_data { get; set; }
    }

    public class OutPrevision
    {
        public string date { get; set; }
        public decimal prevised_value { get; set; }
    }


}