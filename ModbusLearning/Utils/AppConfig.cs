using Microsoft.Extensions.Configuration;

namespace ModbusLearning.Utils;

// 应用配置读取
public static class AppConfig
{
    private static IConfigurationRoot _config;

    static AppConfig()
    {
        var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _config = builder.Build();
    }

    // 读取普通配置项，支持冒号路径，例如 "ModbusSettings:ServerIp"
    public static string GetValue(string key)
    {
        return _config[key];
    }

    // 读取数据库连接字符串
    public static string GetConnectionString(string name)
    {
        return _config.GetConnectionString(name);
    }
}
