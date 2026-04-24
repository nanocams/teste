using DataIngestion.Infrastructure.ApiClients;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.Persistence;


public interface ISymbolRepository
{
    Task<bool> ExistsAsync(SymbolDto symbol);
    Task InsertAsync(SymbolDto symbol);
}


public class SymbolRepository : BaseRepository, ISymbolRepository
{
    public SymbolRepository(ISqlConnectionFactory factory)
        : base(factory) { }

    public async Task<bool> ExistsAsync(SymbolDto s)
    {
        using var conn = await OpenAsync();

        var cmd = new SqlCommand(
            @"SELECT COUNT(*) FROM stocksinfo
              WHERE symbol=@symbol AND mic_code=@mic AND figi_code=@figi", conn);

        cmd.Parameters.AddWithValue("@symbol", s.Symbol);
        cmd.Parameters.AddWithValue("@mic", s.Mic_Code);
        cmd.Parameters.AddWithValue("@figi", s.Figi_Code);

        return (int)await cmd.ExecuteScalarAsync() > 0;
    }

    public async Task InsertAsync(SymbolDto s)
    {
        using var conn = await OpenAsync();

        var cmd = new SqlCommand(
            @"INSERT INTO stocksinfo
              (symbol,name,currency,exchange,mic_code,country,type,
               figi_code,cfi_code,isin,cusip)
              VALUES
              (@symbol,@name,@currency,@exchange,@mic,@country,@type,
               @figi,@cfi,@isin,@cusip)", conn);

        cmd.Parameters.AddWithValue("@symbol", s.Symbol);
        cmd.Parameters.AddWithValue("@name", s.Name ?? "");
        cmd.Parameters.AddWithValue("@currency", s.Currency);
        cmd.Parameters.AddWithValue("@exchange", s.Exchange);
        cmd.Parameters.AddWithValue("@mic", s.Mic_Code);
        cmd.Parameters.AddWithValue("@country", s.Country);
        cmd.Parameters.AddWithValue("@type", s.Type);
        cmd.Parameters.AddWithValue("@figi", s.Figi_Code);
        cmd.Parameters.AddWithValue("@cfi", s.Cfi_Code);
        cmd.Parameters.AddWithValue("@isin", s.Isin);
        cmd.Parameters.AddWithValue("@cusip", s.Cusip);

        await cmd.ExecuteNonQueryAsync();
    }
}
