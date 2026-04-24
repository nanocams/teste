using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DataIngestion.FunctionTwelveData;

namespace DataIngestion.Infrastructure.ApiClients;



//public interface ITwelveDataClient
//{
//    Task<IEnumerable<PriceDto>> GetHistoricalPricesAsync(string symbol, string interval);
//    Task<IEnumerable<SymbolDto>> GetSymbolsAsync();
//}

public record PriceDto(
    DateTime Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public class TwelveDataClient : ITwelveDataClient
{
    private readonly HttpClient _http = new();
    private readonly string _url = Environment.GetEnvironmentVariable("ApiUrl");
    private readonly string _key = Environment.GetEnvironmentVariable("APIKey");

    public async Task<IEnumerable<PriceDto>> GetHistoricalPricesAsync(string symbol, string interval)
    {
        var response = await _http.GetStringAsync(
            $"{_url}/time_series?symbol={symbol}&interval={interval}&apikey={_key}");

        dynamic parsed = JsonConvert.DeserializeObject(response);

        var list = new List<PriceDto>();

        foreach (var v in parsed.values)
        {
            list.Add(new PriceDto(
                DateTime.Parse(v.datetime.ToString()),
                decimal.Parse(v.open.ToString(), CultureInfo.InvariantCulture),
                decimal.Parse(v.high.ToString(), CultureInfo.InvariantCulture),
                decimal.Parse(v.low.ToString(), CultureInfo.InvariantCulture),
                decimal.Parse(v.close.ToString(), CultureInfo.InvariantCulture),
                long.Parse(v.volume.ToString())
            ));
        }

        return list;
    }


    public async Task<IEnumerable<SymbolDto>> GetSymbolsAsync()
    {
        var url = $"{_url}/stocks";
        var json = await _http.GetStringAsync(url);

        dynamic parsed = JsonConvert.DeserializeObject(json);

        var list = new List<SymbolDto>();

        foreach (var item in parsed.data)
        {
            list.Add(JsonConvert.DeserializeObject<SymbolDto>(item.ToString()));
        }

        return list;
    }

}




