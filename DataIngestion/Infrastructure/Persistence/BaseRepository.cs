using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.Persistence;


public abstract class BaseRepository
{
    protected readonly ISqlConnectionFactory Factory;

    protected BaseRepository(ISqlConnectionFactory factory)
    {
        Factory = factory;
    }

    protected async Task<SqlConnection> OpenAsync()
    {
        var conn = Factory.Create();
        await conn.OpenAsync();
        return conn;
    }
}

