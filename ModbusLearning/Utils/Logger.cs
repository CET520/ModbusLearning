namespace ModbusLearning.Utils;

public static class Logger
{
    // 放到程序运行目录下的Logs文件夹中
    //private static readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    // private static readonly string LogDir;
    private static readonly string LogDir = GetLogDirectory();

    private static readonly string LogFile = Path.Combine(LogDir, $"modbus_{DateTime.Now:yyyy-MM-dd}.log");

    static Logger()
    {
        //文件不存在，则自己创建
        if (!Directory.Exists(LogDir))
        {
            Directory.CreateDirectory(LogDir);
        }
    }

    //获取日志地址
    private static string GetLogDirectory()
    {
        //从配置文件中读取
        string dir = AppConfig.GetValue("Logging:LogDirectory");
        
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    private static string GetLogFile()
    {
        //从配置文件中读取
        string dir = AppConfig.GetValue("Logging:LogDirectory");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }
    
    // 写日志
    private static void WriteLog(string level, string message)
    {
        try
        {
            string content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{level}{message}{Environment.NewLine}";
            File.AppendAllText(LogFile, content);
        }
        catch
        {
            //防止写入失败使主程序崩溃
        }
    }

    public static void Info(string msg)
    {
        WriteLog("[INFO]", msg);
    }

    public static void Error(string msg)
    {
        WriteLog("[ERROR]", msg);
    }
}