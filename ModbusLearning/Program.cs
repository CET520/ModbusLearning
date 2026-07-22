using System.IO.Ports;
using ModbusLearning.Services;
using ModbusLearning.Utils;

namespace ModbusLearning;
// 主程序入口
internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== 项目运行菜单 ===");
        Console.WriteLine("1、运行TCP协议的Modbus");
        Console.WriteLine("2、运行RTU协议的Modbus");

        var choice = Console.ReadLine();
        
        if ("1" == choice)
        {
            await RunTcpModeAsync();
        }
        else if ("2" == choice)
        {
            await RunRtuModeAsync();
        }
    }

    private static async Task RunTcpModeAsync()
    {
        Console.WriteLine("=== Modbus 服务端（主动等待从站连接）模式,按Control+C停止 ===");
        
        // 1. 读取服务端监听配置
        string listenIp = AppConfig.GetValue("ModbusTcpServer:ListenIp");
        int listenPort = int.Parse(AppConfig.GetValue("ModbusTcpServer:ListenPort"));

        // 2. 从配置读取轮询所需的参数
        byte unitId = byte.Parse(AppConfig.GetValue("ModbusSettings:UnitId"));
        ushort startAddr = ushort.Parse(AppConfig.GetValue("ModbusSettings:StartAddress"));
        ushort quantity = ushort.Parse(AppConfig.GetValue("ModbusSettings:Quantity"));
        int interval = int.Parse(AppConfig.GetValue("ModbusSettings:PollingIntervalMs"));

        // 3. 实例化服务端对象
        var serverClient = new ModbusTcpServerClient(listenIp, listenPort);
// 注册control+C
        Console.CancelKeyPress += async (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            await serverClient.StopAsync();
        };
        
        // 4. 启动轮询定时器（此时 _isConnected 为 false，定时器不会真正发包，直到连接建立）
        serverClient.StartPolling(unitId, startAddr, quantity, interval);

        // 5. 启动服务端监听（这是一个死循环，会阻塞在这里等待远程连接）
        await serverClient.StartServerAsync();
        Console.ReadLine();
    }
    
    // RTU协议的Modbus
    private static async Task RunRtuModeAsync()
    {
        Console.WriteLine("=== Modbus RTU (串口) 数据采集模式 ===");
        Console.WriteLine("【提示】按 Ctrl+C 可优雅退出程序！");

        string port = AppConfig.GetValue("RtuSettings:PortName");
        int baud = int.Parse(AppConfig.GetValue("RtuSettings:BaudRate"));
        string parityStr = AppConfig.GetValue("RtuSettings:Parity");
        Parity parity = parityStr switch { "Even" => Parity.Even, "Odd" => Parity.Odd, _ => Parity.None };
        int dataBits = int.Parse(AppConfig.GetValue("RtuSettings:DataBits"));
        string stopStr = AppConfig.GetValue("RtuSettings:StopBits");
        StopBits stopBits = stopStr switch { "Two" => StopBits.Two, _ => StopBits.One };

        var rtuClient = new ModbusRtuClient(port, baud, parity, dataBits, stopBits);

        Console.CancelKeyPress += async (_, e) =>
        {
            e.Cancel = true;
            await rtuClient.StopAsync();
        };

        await rtuClient.OpenAsync();
            
        byte unitId = byte.Parse(AppConfig.GetValue("ModbusSettings:UnitId"));
        ushort startAddr = ushort.Parse(AppConfig.GetValue("ModbusSettings:StartAddress"));
        ushort quantity = ushort.Parse(AppConfig.GetValue("ModbusSettings:Quantity"));
        int interval = int.Parse(AppConfig.GetValue("ModbusSettings:PollingIntervalMs"));

        rtuClient.StartPolling(unitId, startAddr, quantity, interval);

        await Task.Delay(Timeout.Infinite, rtuClient.GetCancellationToken()); // 阻塞主线程，等待 Ctrl+C
    }
}