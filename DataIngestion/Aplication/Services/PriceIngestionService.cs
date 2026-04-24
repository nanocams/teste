using DataIngestion.Aplication.Interfaces;
using DataIngestion.Infrastructure.ApiClients;
using DataIngestion.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Aplication.Services;


public class PriceIngestionService : IPriceIngestionService
{
    private readonly ITwelveDataClient _twelveClient;
    private readonly IStockRepository _stockRepo;
    private readonly IPriceRepository _priceRepo;


    public PriceIngestionService(
            ITwelveDataClient twelveClient,
            IStockRepository stockRepo,
            IPriceRepository priceRepo)
    {
        _twelveClient = twelveClient;
        _stockRepo = stockRepo;
        _priceRepo = priceRepo;
    }


    public async Task IngestAsync(string symbol, string interval)
    {
        // 1️ Obter ou criar stock
        int stockId = await _stockRepo.GetOrCreateAsync(symbol);

        // 2️ Obter última data ingerida
        var lastDate = await _priceRepo.GetLastDateAsync(stockId);

        // 3️ Buscar dados externos
        var prices = await _twelveClient.GetHistoricalPricesAsync(symbol, interval);

        // 4️ Persistir preços novos
        await _priceRepo.InsertPricesAsync(stockId, prices, lastDate);
    }


}
