using Microsoft.Data.Sqlite;
using ModbusLearning.Models;
using ModbusLearning.Utils;

namespace ModbusLearning.Services;

public static class DatabaseService
{
    private static readonly string ConnectionString = AppConfig.GetConnectionString("SqliteDb");

    private static readonly string DbFilePath = new SqliteConnectionStringBuilder(ConnectionString).DataSource;
    
    static DatabaseService()
    {
        var directoryPath = Path.GetDirectoryName(DbFilePath);
        
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        
        if (!File.Exists(DbFilePath))
        {
            using var connection = new SqliteConnection(ConnectionString);
            
            connection.Open();

            var command = connection.CreateCommand();
            
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SensorLogs(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RegisterAddress INTEGER NOT NULL,
                    RawValue INTEGER NOT NULL,
                    PhysicalValue REAL NOT NULL,
                    Timestamp TEXT NOT NULL
                )";
            command.ExecuteNonQuery();
            connection.Close();
        }
    }

    //插入数据（异步实现）
    public static async Task InsertSensorDataAsync(SensorData data)
    {
        using var connection = new SqliteConnection(ConnectionString);
        
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SensorLogs (RegisterAddress,RawValue,PhysicalValue,Timestamp)
            VALUES($addr,$raw,$phy,$time)";

        //参数化查询，防止SQL注入
        command.Parameters.AddWithValue("$addr", data.RegisterAddress);
        command.Parameters.AddWithValue("$raw", data.RawValue);
        command.Parameters.AddWithValue("$phy", data.PhysicalValue);
        command.Parameters.AddWithValue("$time", data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        await command.ExecuteNonQueryAsync();
    }
}