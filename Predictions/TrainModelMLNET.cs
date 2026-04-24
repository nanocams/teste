using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Predictions.Model;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Predictions;

public class TrainModelMLNET
{
    private readonly ILogger _logger;
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly string _folderPathtTrainModels = Environment.GetEnvironmentVariable("FolderPathtTrainModels");
    private readonly string _trainModelsBlobCNN = Environment.GetEnvironmentVariable("TrainModelsBlobCNN");
    private readonly string _trainModelsBlobContainer = Environment.GetEnvironmentVariable("TrainModelsBlobContainer");
    private readonly string _trainModelsBlobFolder = Environment.GetEnvironmentVariable("TrainModelsBlobFolder");

    public TrainModelMLNET(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TrainModelMLNET>();
    }

    [Function("TrainModelMLNET")]
    public void Run([TimerTrigger("%TrainModelSchedule%")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        try
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                
                var StockPricesCmd = new SqlCommand("WITH RankedProducts AS (SELECT pr.[id],pr.[date],pr.[open],pr.[high],pr.[low],pr.[close],pr.[volume],pr.[provider_id],pr.[stock_id],st.symbol,ROW_NUMBER() OVER (PARTITION BY stock_id  ORDER BY [date] DESC) AS rn FROM [dbo].[prices] pr INNER JOIN [dbo].[stocks] st on pr.stock_id = st.id) SELECT * FROM RankedProducts WHERE rn <= 8", conn);

                var result = StockPricesCmd.ExecuteReader();
                _logger.LogInformation("Receive database info.");

                var StockPricesList = new List<TrainingModel>();

                if (result != null)
                {
                    _logger.LogInformation("database data exist");
                    while (result.Read())
                    {
                        var ListStockPrices = new TrainingModel
                        {
                            DatePrice = result.GetDateTime(result.GetOrdinal("date")),
                            Open = result.GetDecimal(result.GetOrdinal("open")),
                            High = result.GetDecimal(result.GetOrdinal("high")),
                            Low = result.GetDecimal(result.GetOrdinal("low")),
                            Close = result.GetDecimal(result.GetOrdinal("close")),
                            Volume = result.GetInt64(result.GetOrdinal("volume")),
                            Symbol = result.GetString(result.GetOrdinal("symbol"))
                        };

                        StockPricesList.Add(ListStockPrices);
                    }

                    _logger.LogInformation("stockprices list exist - " + StockPricesList.Count());

                    if (StockPricesList.Count() > 0)
                    {
                        _logger.LogInformation("start training");
                        var ListStocks = StockPricesList.Select(stock => stock.Symbol).Distinct().ToList();

                        foreach (var item in ListStocks)
                        {
                            _logger.LogInformation("item - " + item);

                            var ListStockPrices = StockPricesList.Where(pr => pr.Symbol == item).OrderBy(pr => pr.DatePrice).ToList();
                            _logger.LogInformation("ListStockPrices - " + ListStockPrices.Count());
                            _logger.LogInformation("ListStockPrices 0 - " + ListStockPrices[0].DatePrice);
                            _logger.LogInformation("ListStockPrices last - " + ListStockPrices.Last().DatePrice);

                            List<PricesLast3Days> Last3DaysPricesList = new List<PricesLast3Days>();

                            if (ListStockPrices.Count() == 8)
                            {
                                for (int i = 3; i < ListStockPrices.Count(); i=i+4)
                                {
                                    PricesLast3Days lastdays = new PricesLast3Days
                                    {
                                        PriceT3 = (float)ListStockPrices[i - 3].Close,
                                        PriceT2 = (float)ListStockPrices[i - 2].Close,
                                        PriceT1 = (float)ListStockPrices[i - 1].Close,
                                        PriceT = (float)ListStockPrices[i].Close
                                    };

                                    _logger.LogInformation("Value float" + lastdays.PriceT3);

                                    Last3DaysPricesList.Add(lastdays);
                                }
                            }

                            if (Last3DaysPricesList.Count() > 0)
                            {
                                var mlContext = new MLContext();

                                //mlContext.Log += (sender, e) => _logger.LogInformation($"[{e.Source}] {e.Message}");

                                var dataView = mlContext.Data.LoadFromEnumerable(Last3DaysPricesList);
                                _logger.LogInformation("Step 1");

                                var pipeline = mlContext.Transforms.Concatenate("Features", "PriceT3", "PriceT2", "PriceT1")
                                    .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                                    .Append(mlContext.Regression.Trainers.Sdca(labelColumnName: "PriceT", maximumNumberOfIterations: 10));
                                _logger.LogInformation("Step 2");

                                var model = pipeline.Fit(dataView);
                                _logger.LogInformation("Step 3");

                                var blobName = _trainModelsBlobFolder + "/" + item + ".zip";

                                using var memoryStream = new MemoryStream();
                                mlContext.Model.Save(model, dataView.Schema, stream: memoryStream);
                                memoryStream.Position = 0;


                                var blobClient = new BlobContainerClient(_trainModelsBlobCNN, _trainModelsBlobContainer);
                                blobClient.CreateIfNotExists();
                                var blob = blobClient.GetBlobClient(blobName);
                                blob.UploadAsync(memoryStream, overwrite: true);


                                _logger.LogInformation("model saved. " + blob.Name);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex.Message);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}