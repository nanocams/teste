using DataIngestion.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using static DataIngestion.FunctionTwelveData;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DataIngestion;

public class GetSymbolsDataSCH
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _twelveDataUrl = Environment.GetEnvironmentVariable("ApiUrl");
    private readonly string _twelveApiKey = Environment.GetEnvironmentVariable("APIKey");
    private readonly string _dataIngestionUrl = Environment.GetEnvironmentVariable("DataIngestionUrl");
    private readonly string _dataIngestionCode = Environment.GetEnvironmentVariable("FunctionTwelveDataCode");

    public GetSymbolsDataSCH(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GetSymbolsDataSCH>();
    }

    [Function("GetSymbolsDataSCH")]
    public async Task Run([TimerTrigger("%FunctionSchedule%")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        string url = $"{_twelveDataUrl}/stocks";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Receive response.");

            var parsed = JsonConvert.DeserializeObject<SymbolGenData>(json);
            _logger.LogInformation("Check parsed data.");
            if (parsed?.Data != null)
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    _logger.LogInformation("Start checking list of values.");
                    foreach (var stock in parsed.Data)
                    {
                        int ExistStock = 0;
                        var cmd = new SqlCommand("select COUNT(*) from stocksinfo where symbol =@stockid and mic_code=@mic_code and figi_code=@figi_code", conn);
                        cmd.Parameters.AddWithValue("@stockid", stock.Symbol);
                        cmd.Parameters.AddWithValue("@mic_code", stock.Mic_Code);
                        cmd.Parameters.AddWithValue("@figi_code", stock.Figi_Code);

                        ExistStock = (int)await cmd.ExecuteScalarAsync();
                        _logger.LogInformation("symbol exist?." + ExistStock);
                        _logger.LogInformation("symbol?." + stock.Symbol);

                        if (ExistStock == 0)
                        {
                            var CmdAdd = new SqlCommand("INSERT INTO stocksinfo (symbol, name, currency, exchange, mic_code, country, type, figi_code, cfi_code, isin, cusip) VALUES (@symbol, @name, @currency, @exchange, @mic_code, @country, @type, @figi_code, @cfi_code, @isin, @cusip)", conn);
                            CmdAdd.Parameters.AddWithValue("@symbol", stock.Symbol);
                            CmdAdd.Parameters.AddWithValue("@name", stock.Name);
                            CmdAdd.Parameters.AddWithValue("@currency", stock.Currency);
                            CmdAdd.Parameters.AddWithValue("@exchange", stock.Exchange);
                            CmdAdd.Parameters.AddWithValue("@mic_code", stock.Mic_Code);
                            CmdAdd.Parameters.AddWithValue("@country", stock.Country);
                            CmdAdd.Parameters.AddWithValue("@type", stock.Type);
                            CmdAdd.Parameters.AddWithValue("@figi_code", stock.Figi_Code);
                            CmdAdd.Parameters.AddWithValue("@cfi_code", stock.Cfi_Code);
                            CmdAdd.Parameters.AddWithValue("@isin", stock.Isin);
                            CmdAdd.Parameters.AddWithValue("@cusip", stock.Cusip);
                            _logger.LogInformation("sql command - " + CmdAdd.CommandText);
                            await CmdAdd.ExecuteNonQueryAsync();

                            _logger.LogInformation("symbol added.");
                        }
                        //else
                        //{
                        //    var CmdUpd = new SqlCommand("UPDATE stocksinfo set name = @name, currency = @currency, exchange = @exchange, mic_code = @mic_code, country = @country, type = @type, figi_code = @figi_code, cfi_code = @cfi_code, isin = @isin, cusip = @cusip  where symbol = @symbol", conn);
                        //    CmdUpd.Parameters.AddWithValue("@symbol", stock.Symbol);
                        //    CmdUpd.Parameters.AddWithValue("@name", stock.Name);
                        //    CmdUpd.Parameters.AddWithValue("@currency", stock.Currency);
                        //    CmdUpd.Parameters.AddWithValue("@exchange", stock.Exchange);
                        //    CmdUpd.Parameters.AddWithValue("@mic_code", stock.Mic_Code);
                        //    CmdUpd.Parameters.AddWithValue("@country", stock.Country);
                        //    CmdUpd.Parameters.AddWithValue("@type", stock.Type);
                        //    CmdUpd.Parameters.AddWithValue("@figi_code", stock.Figi_Code);
                        //    CmdUpd.Parameters.AddWithValue("@cfi_code", stock.Cfi_Code);
                        //    CmdUpd.Parameters.AddWithValue("@isin", stock.Isin);
                        //    CmdUpd.Parameters.AddWithValue("@cusip", stock.Cusip);

                        //    CmdUpd.ExecuteNonQuery();

                        //    _logger.LogInformation("symbol updated.");
                        //}

                        if (stock.Exchange == "NYSE" || stock.Exchange == "NASDAQ")
                        {
                            try
                            {
                                _logger.LogInformation("start ingestion");
                                await TriggerIngestion(stock.Symbol);
                            }
                            catch (Exception ingestionError)
                            {
                                _logger.LogInformation(ingestionError.Message);
                                continue;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {

            _logger.LogInformation("there was an error.");
            _logger.LogInformation(ex.Message);
        }
    }

    private async Task TriggerIngestion(string symbol)
    {
        var ingestionUrl = $"{_dataIngestionUrl}?symbol={symbol}&interval=1day&code={_dataIngestionCode}";
        var response = await _httpClient.GetAsync(ingestionUrl);
        _logger.LogInformation("Ingestion response: {status}", response.StatusCode);
    }
}