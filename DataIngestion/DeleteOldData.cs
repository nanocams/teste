using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DataIngestion;

public class DeleteOldData
{
    private readonly ILogger _logger;
    private readonly string _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    private readonly Int32 _dayslimittodelete = Convert.ToInt32(Environment.GetEnvironmentVariable("DaysToDelete"));

    public DeleteOldData(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DeleteOldData>();
    }

    [Function("DeleteOldData")]
    public void Run([TimerTrigger("%ScheduleDeleteOldData%")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);
        _logger.LogInformation("days to delete previous to - " + _dayslimittodelete);
        try
        {
            if (_dayslimittodelete > 0)
            {
                _logger.LogInformation("Start deleting.");
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("DELETE FROM prices WHERE date < DATEADD(day, -" + _dayslimittodelete + ", GETDATE())", conn);

                    cmd.ExecuteNonQuery();

                    _logger.LogInformation("Delete finish.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("There was an error." + ex);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}