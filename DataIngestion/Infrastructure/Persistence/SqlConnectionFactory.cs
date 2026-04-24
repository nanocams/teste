using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestion.Infrastructure.Persistence
{

    public interface ISqlConnectionFactory
    {

        SqlConnection Create();
    }


    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory()
        {
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        }

        public SqlConnection Create() => new SqlConnection(_connectionString);
    }

}
