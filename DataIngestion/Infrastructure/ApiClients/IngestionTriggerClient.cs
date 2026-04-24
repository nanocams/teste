using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.ApiClients;


public interface IIngestionTriggerClient
{
    Task TriggerPriceIngestionAsync(string symbol);
}

public class IngestionTriggerClient : IIngestionTriggerClient
{
    private readonly HttpClient _http = new();
    private readonly string _url = Environment.GetEnvironmentVariable("DataIngestionUrl");
    private readonly string _code = Environment.GetEnvironmentVariable("FunctionTwelveDataCode");

    public async Task TriggerPriceIngestionAsync(string symbol)
    {
        var call = $"{_url}?symbol={symbol}&interval=1day&code={_code}";
        await _http.GetAsync(call);
    }
}
