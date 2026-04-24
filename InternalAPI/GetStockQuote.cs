using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Net;

namespace InternalAPI;

public class GetStockQuote
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new HttpClient{ Timeout = TimeSpan.FromMinutes(30)};
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _dataIngestionUrl = Environment.GetEnvironmentVariable("DataIngestionUrl");
    private readonly string _companyInfoUrl = Environment.GetEnvironmentVariable("AlphaVantageFunctionUrl"); //nova var de ambiente
    private readonly string _dataIngestionCode = Environment.GetEnvironmentVariable("FunctionTwelveDataCode");

    public GetStockQuote(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GetStockQuote>();
    }

    [Function("GetStockQuote")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes/{symbol}")] HttpRequestData req,
        string symbol)
    {
        _logger.LogInformation("Received request for symbol: {symbol}", symbol);

        try
        {
            int stockId = await GetStockId(symbol);

            // se o simbolo não existe, forca ingestao completa
            if (stockId == 0)
            {
                await TriggerIngestion(symbol);
                stockId = await GetStockId(symbol);

                if (stockId == 0)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Symbol not found after ingestion.");
                    return notFound;
                }
            }

            // busca historico atual
            var history = await GetHistoricalPrices(stockId);

            // se ainda não existem dados em 'prices', tenta ingestao
            if (!history.Any())
            {
                _logger.LogInformation($"No historical data for {symbol}. Triggering ingestion...");
                await TriggerIngestion(symbol);

                // tenta novamente buscar os dados apos ingestao
                history = await GetHistoricalPrices(stockId);

                // se ainda estiver vazio, aborta
                if (!history.Any())
                {
                    _logger.LogWarning($"Ingestion for {symbol} did not populate 'prices'. Aborting.");
                    var noDataResp = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noDataResp.WriteStringAsync("No price data available after ingestion.");
                    return noDataResp;
                }
            }
            
            // verifica se precisa atualizar (ultimo dado < hoje)
            var lastDateStr = history.Max(h => h.date);

            var lastDate = history.Max(h =>
            {
                //tenta fazer parse da data com seguranca
                if (DateTime.TryParse(h.date, out var parsed))
                    return parsed;
                else
                    return DateTime.MinValue; 
            });

            if (lastDate < DateTime.Today)
            {
                _logger.LogInformation("Historical data for {symbol} is outdated. Triggering ingestion...", symbol);
                await TriggerIngestion(symbol);
                history = await GetHistoricalPrices(stockId); // atualiza apos ingestao
            }

            // identifica preco de hoje (se existir)
            var today = history.FirstOrDefault(h => h.date == DateTime.Today.ToString("yyyy-MM-dd"))?.price ?? null;

            var predictions = await GetPrevisionsPrices(stockId);

            // busca dados da empresa via Alpha Vantage
            var company = await GetCompanyInfo(symbol);

            // monta resposta
            var responseObj = new
            {
                symbol = symbol.ToUpper(),
                company,
                todayPrice = today,
                history,
                predictions
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

    // chama a azure function de DataIngestion
    private async Task TriggerIngestion(string symbol)
    {
        var ingestionUrl = $"{_dataIngestionUrl}?symbol={symbol}&interval=1day&code={_dataIngestionCode}";
        var response = await _httpClient.GetAsync(ingestionUrl);
        _logger.LogInformation("Ingestion response: {status}", response.StatusCode);
    }

    private async Task<int> GetStockId(string symbol)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT id FROM stocks WHERE symbol = @symbol", conn);
        cmd.Parameters.AddWithValue("@symbol", symbol.ToUpper());

        var result = await cmd.ExecuteScalarAsync();
        return result != null && int.TryParse(result.ToString(), out var id) ? id : 0;
    }

    private async Task<object> GetCompanyInfo(string symbol)
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
            SELECT 
                s.name, 
                ISNULL(sec.designation, '') AS sector,
                ISNULL(ind.designation, '') AS industry,
                s.description
            FROM stocks s
            LEFT JOIN sectors sec ON s.sector_id = sec.id
            LEFT JOIN industries ind ON s.industry_id = ind.id
            WHERE s.symbol = @symbol";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@symbol", symbol.ToUpper());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new
                {
                    name = reader["name"]?.ToString() ?? "",
                    sector = reader["sector"]?.ToString() ?? "",
                    industry = reader["industry"]?.ToString() ?? "",
                    description = reader["description"]
                };
            }

            return new { name = "", sector = "", industry = "", description = "Not found in DB" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching company info from DB");
            return new { name = "", sector = "", industry = "", description = "Unavailable" };
        }
    }

    private async Task<List<PriceEntry>> GetHistoricalPrices(int stockId)
    {
        var prices = new List<PriceEntry>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT date, [close] FROM prices WHERE stock_id = @stock_id AND date >= DATEADD(day,-10, GETDATE()) ORDER BY date", conn);
        cmd.Parameters.AddWithValue("@stock_id", stockId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prices.Add(new PriceEntry
            {
                date = Convert.ToDateTime(reader["date"]).ToString("yyyy-MM-dd"),
                price = Convert.ToDecimal(reader["close"])
            });
        }

        if (prices.Count > 0 && prices.Last().date != DateTime.Today.ToString("yyyy-MM-dd")) {
            prices.Add(new PriceEntry
            {
                date = DateTime.Today.ToString("yyyy-MM-dd"),
                price = prices.Last().price
            });
        }

        return prices;
    }
    
    /// <summary>
    /// To get the prevision prices from table previsions
    /// </summary>
    /// <param name="stockId"></param>
    /// <returns></returns>
    private async Task<List<object>> GetPrevisionsPrices(int stockId)
    {
        var previsions = new List<object>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT [prevision_date], [price] FROM [previsions] WHERE [stock_id] = @stock_id AND [prevision_date] >= GETDATE() ORDER BY [prevision_date]", conn);
        cmd.Parameters.AddWithValue("@stock_id", stockId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            previsions.Add(new 
            {
                date = Convert.ToDateTime(reader["prevision_date"]).ToString("yyyy-MM-dd"),
                price = Convert.ToDecimal(reader["price"])
            });
        }

        if (previsions.Count == 0)
        {
            previsions.Add(new { date = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"), price = 0.ToString("F3") });
            previsions.Add(new { date = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd"), price = 0.ToString("F3") });
        }

        return previsions;
    }
}



// modelo que representa uma entrada de preco no histórico
public class PriceEntry
{
    public string date { get; set; }
    public decimal price { get; set; }
}