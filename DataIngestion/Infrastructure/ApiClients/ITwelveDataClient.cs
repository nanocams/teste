using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.ApiClients
{
    public class ITwelveDataClient
    {
        Task<IEnumerable<PriceDto>> GetHistoricalPricesAsync(string symbol, string interval);
        Task<IEnumerable<SymbolDto>> GetSymbolsAsync();
    }
}
