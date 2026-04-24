using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.Persistence;


public interface IStockRepository
{
    Task<int> GetOrCreateAsync(string symbol);
}

public class StockRepository : BaseRepository, IStockRepository
{
    public StockRepository(ISqlConnectionFactory factory)
        : base(factory) { }

    public async Task<int> GetOrCreateAsync(string symbol)
    {
        using var conn = await OpenAsync();

        var cmd = new SqlCommand(
            "SELECT id FROM stocks WHERE symbol = @symbol", conn);
        cmd.Parameters.AddWithValue("@symbol", symbol);

        var result = await cmd.ExecuteScalarAsync();
        if (result != null)
            return Convert.ToInt32(result);

        var insert = new SqlCommand(
            "INSERT INTO stocks (symbol) VALUES (@symbol); SELECT SCOPE_IDENTITY();",
            conn);
        insert.Parameters.AddWithValue("@symbol", symbol);

        return Convert.ToInt32(await insert.ExecuteScalarAsync());
    }
}
