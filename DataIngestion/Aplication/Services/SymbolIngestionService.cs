using DataIngestion.Infrastructure.ApiClients;
using DataIngestion.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Aplication.Services;


public class SymbolIngestionService : ISymbolIngestionService
{
    private readonly ITwelveDataClient _twelveClient;
    private readonly ISymbolRepository _symbolRepo;
    private readonly IIngestionTriggerClient _trigger;

    public SymbolIngestionService(
        ITwelveDataClient twelveClient,
        ISymbolRepository symbolRepo,
        IIngestionTriggerClient trigger)
    {
        _twelveClient = twelveClient;
        _symbolRepo = symbolRepo;
        _trigger = trigger;
    }

    public async Task IngestSymbolsAsync()
    {
        var symbols = await _twelveClient.GetSymbolsAsync();

        foreach (var symbol in symbols)
        {
            bool exists = await _symbolRepo.ExistsAsync(symbol);

            if (!exists)
                await _symbolRepo.InsertAsync(symbol);

            if (symbol.Exchange is "NYSE" or "NASDAQ")
                await _trigger.TriggerPriceIngestionAsync(symbol.Symbol);
        }
    }
}

