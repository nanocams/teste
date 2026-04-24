using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Predictions.Model;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;


namespace Predictions;

public class PredictionMLNET
{
    private readonly ILogger<PredictionMLNET> _logger;
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _folderPathtTrainModels = Environment.GetEnvironmentVariable("FolderPathtTrainModels");
    private readonly string _trainModelsBlobCNN = Environment.GetEnvironmentVariable("TrainModelsBlobCNN");
    private readonly string _trainModelsBlobContainer = Environment.GetEnvironmentVariable("TrainModelsBlobContainer");
    private readonly string _trainModelsBlobFolder = Environment.GetEnvironmentVariable("TrainModelsBlobFolder");

    public PredictionMLNET(ILogger<PredictionMLNET> logger)
    {
        _logger = logger;
    }

    [Function("PredictionMLNET")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        try
        {
            string symbol = req.Query["symbol"].ToString().ToUpper();
            _logger.LogInformation("Receive symbol." + symbol);

            //var ModelPath = _folderPathtTrainModels + symbol + ".zip";
            var blobName = _trainModelsBlobFolder + "/" + symbol + ".zip";
            _logger.LogInformation("Symbol path. " + blobName);

            var blobClient = new BlobContainerClient(_trainModelsBlobCNN, _trainModelsBlobContainer);
            //blobClient.CreateIfNotExists();
            var blob = blobClient.GetBlobClient(blobName);

            if (!blob.Exists())
            {
                return new NotFoundObjectResult("Company data model not found.");
            }
            
            //if (!File.Exists(ModelPath))
            //{
            //    return new NotFoundObjectResult("Company data model not found.");
            //}

            int stockId = await GetStockId(symbol);
            _logger.LogInformation("Get symbol ID." + stockId);

            if (stockId == 0)
            {
                return new NotFoundObjectResult("Company data not found.");
            }

            using var memoryStream = new MemoryStream();
            await blob.DownloadToAsync(memoryStream);
            memoryStream.Position = 0;

            var mlContext = new MLContext();

            _logger.LogInformation("Recover training model.");


            ITransformer trainedModel = mlContext.Model.Load(memoryStream, out var inputSchema); ;
            //using (var stream = new FileStream(ModelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            //{
            //    trainedModel = mlContext.Model.Load(stream, out var modelInputSchema);
            //}

            List<PriceDataHist> LastPricesStock = new List<PriceDataHist>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var StockPricesCmd = new SqlCommand("SELECT TOP (4) [date],[open],[high],[low],[close],[volume],[provider_id],[stock_id] FROM [dbo].[prices] WHERE stock_id = @symbolid ORDER BY date DESC", conn);
            StockPricesCmd.Parameters.AddWithValue("@symbolid", stockId);

            var result = await StockPricesCmd.ExecuteReaderAsync();
            _logger.LogInformation("Receive database info.");

            if (result != null)
            {
                while (await result.ReadAsync())
                {
                    var PricesData = new PriceDataHist
                    {
                        DatePrice = result.GetDateTime(result.GetOrdinal("date")),
                        Open = result.GetDecimal(result.GetOrdinal("open")),
                        High = result.GetDecimal(result.GetOrdinal("high")),
                        Low = result.GetDecimal(result.GetOrdinal("low")),
                        Close = result.GetDecimal(result.GetOrdinal("close")),
                        Volume = result.GetInt64(result.GetOrdinal("volume"))
                    };

                    LastPricesStock.Add(PricesData);
                }
                _logger.LogInformation("LastPricesStock count.");

                if (LastPricesStock.Count() > 0)
                {
                    _logger.LogInformation("LastPricesStock exists." + LastPricesStock.Count());
                    LastPricesStock.Reverse();
                    List<PricesLast3Days> Last3DaysPricesList = new List<PricesLast3Days>();

                    if (LastPricesStock.Count() >= 3)
                    {
                        PricesLast3Days lastdays = new PricesLast3Days
                        {
                            PriceT3 = (float)LastPricesStock[0].Close,
                            PriceT2 = (float)LastPricesStock[1].Close,
                            PriceT1 = (float)LastPricesStock[2].Close,
                            PriceT = (float)LastPricesStock[3].Close
                        };

                        Last3DaysPricesList.Add(lastdays);
                    }
                    else
                    {
                        return new NotFoundObjectResult("Company data not found.");
                    }

                    if (Last3DaysPricesList.Count() > 0)
                    {
                        _logger.LogInformation("Start prediction");

                        var engine = mlContext.Model.CreatePredictionEngine<PricesLast3Days, StockPrediction>(trainedModel);
                        _logger.LogInformation("Step 4");

                        var TodayPlus1 = engine.Predict(Last3DaysPricesList.Last());
                        _logger.LogInformation("Step 5");

                        var Next2Day = new PricesLast3Days
                        {
                            PriceT3 = Last3DaysPricesList.Last().PriceT2,
                            PriceT2 = Last3DaysPricesList.Last().PriceT1,
                            PriceT1 = TodayPlus1.ValorPrevisto
                        };

                        var TodayPlus2 = engine.Predict(Next2Day);

                        _logger.LogInformation("Step 6");

                        _logger.LogInformation("Previsao 1 - " + TodayPlus1.ValorPrevisto);
                        _logger.LogInformation("Previsao 2 - " + TodayPlus2.ValorPrevisto);

                        List<OutPrevision> listPrevision = new List<OutPrevision>();

                        listPrevision.Add(new OutPrevision
                        {
                            date = DateTime.UtcNow.AddDays(1).ToString(),
                            prevised_value = Convert.ToDecimal(TodayPlus1.ValorPrevisto)
                        });

                        listPrevision.Add(new OutPrevision
                        {
                            date = DateTime.UtcNow.AddDays(2).ToString(),
                            prevised_value = Convert.ToDecimal(TodayPlus2.ValorPrevisto)
                        });
                        //for (int i = 1; i <= 2;  i++)
                        //{
                        //    listPrevision.Add(new OutPrevision
                        //    {
                        //        date = DateTime.UtcNow.AddDays(i).ToString(),
                        //        prevised_value = Convert.ToDecimal(TodayPlus1.ValorPrevisto)
                        //    });
                        //}

                        var createDate = DateTime.UtcNow;

                        foreach (var item in listPrevision)
                        {
                            //DateTime? existDate = await iFExistDate(stock_id, item.date);
                            //if (!DateTime.TryParse(item.date, out DateTime date))
                            //    continue;
                            //if (existDate.HasValue && date == existDate.Value)
                            //    continue;
                            using (SqlConnection connprev = new SqlConnection(_connectionString))
                            {
                                await connprev.OpenAsync();
                                var query = @"INSERT INTO previsions (create_date, prevision_date, price, stock_id)
                                  VALUES (@createDate, @prevision_date, @price, @stock_id)";
                                using (SqlCommand cmd = new SqlCommand(query, connprev))
                                {
                                    cmd.Parameters.AddWithValue("@createDate", createDate);
                                    cmd.Parameters.AddWithValue("@prevision_date", item.date);
                                    cmd.Parameters.AddWithValue("@price", item.prevised_value);
                                    cmd.Parameters.AddWithValue("@stock_id", stockId);

                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        return new OkObjectResult("data entered successfully.");
                        //return new OkObjectResult(new
                        //{
                        //    PrecoDia1 = TodayPlus1.ValorPrevisto,
                        //    PrecoDia2 = TodayPlus2.ValorPrevisto
                        //});
                    }
                    else
                    {
                        return new NotFoundObjectResult("Company data not found.");
                    }
                }
                else
                {
                    return new NotFoundObjectResult("Company data not found.");
                }
            }
            else
            {
                return new NotFoundObjectResult("Company data not found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        return new OkObjectResult("Welcome to Azure Functions!");
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
}