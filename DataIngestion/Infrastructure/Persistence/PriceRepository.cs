using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DataIngestion.FunctionTwelveData;

namespace DataIngestion.Infrastructure.Persistence;



public interface IPriceRepository
{
    Task<DateTime?> GetLastDateAsync(int stockId);
    Task InsertPricesAsync(int stockId, IEnumerable<PriceDto> prices, DateTime? lastDate);
}

public class PriceRepository : BaseRepository, IPriceRepository
{
    public PriceRepository(ISqlConnectionFactory factory)
        : base(factory) { }

    public async Task<DateTime?> GetLastDateAsync(int stockId)
    {
        using var conn = await OpenAsync();
        var cmd = new SqlCommand(
            "SELECT MAX([date]) FROM prices WHERE stock_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", stockId);

        var result = await cmd.ExecuteScalarAsync();
        return result != DBNull.Value ? (DateTime?)result : null;
    }

    public async Task InsertPricesAsync(
        int stockId,
        IEnumerable<PriceDto> prices,
        DateTime? lastDate)
    {
        using var conn = await OpenAsync();

        foreach (var p in prices)
        {
            if (lastDate.HasValue && p.Date <= lastDate.Value)
                continue;

            var cmd = new SqlCommand(
                @"INSERT INTO prices 
                  ([date],[open],[high],[low],[close],volume,stock_id,provider_id)
                  VALUES (@date,@open,@high,@low,@close,@volume,@stock,1)", conn);

            cmd.Parameters.AddWithValue("@date", p.Date);
            cmd.Parameters.AddWithValue("@open", p.Open);
            cmd.Parameters.AddWithValue("@high", p.High);
            cmd.Parameters.AddWithValue("@low", p.Low);
            cmd.Parameters.AddWithValue("@close", p.Close);
            cmd.Parameters.AddWithValue("@volume", p.Volume);
            cmd.Parameters.AddWithValue("@stock", stockId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}

