using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace InternalAPI
{
    public class MoveDataPrevisionToHistoryFunction
    {
        private readonly ILogger _logger;
        private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        public MoveDataPrevisionToHistoryFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MoveDataPrevisionToHistoryFunction>();
        }

        // Run every day at midnight (UTC)
        [Function("MoveDataPrevisionToHistory")]
        public async Task RunAsync([TimerTrigger("%MOVE_DATA_CRON%")] TimerInfo timerInfo)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string sql = @"
                         INSERT INTO previsionsHistory (id, create_date,prevision_date,price,stock_id,MovedDate)
                                SELECT p.id,p.create_date,p.prevision_date,p.price,p.stock_id , GETDATE() 
                                        FROM previsions p
                                        WHERE CAST(p.create_date AS DATE) = DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
                                        AND NOT EXISTS (
                                            SELECT 1 FROM previsionsHistory ph
                                            WHERE ph.id = p.id
                                                        );

                                 DELETE FROM previsions WHERE CAST(create_date AS DATE) = DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
                                    ";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        int rows = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation($"{rows} rows copied for yesterday at {DateTime.UtcNow}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving data.");
            }
        }

    }
}


