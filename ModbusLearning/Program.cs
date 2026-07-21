using ModbusLearning.Services;
using ModbusLearning.Utils;

namespace ModbusLearning;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Console.WriteLine("==== Modbus 自动轮询与数据解析标准协议学习 ====");
        //
        // string ip = AppConfig.GetValue("ModbusSettings:ServerIp");
        // int port = int.Parse(AppConfig.GetValue("ModbusSettings:Port"));
        // byte unitId = byte.Parse(AppConfig.GetValue("ModbusSettings:UnitId"));
        // ushort startAddr = ushort.Parse(AppConfig.GetValue("ModbusSettings:StartAddress"));
        // ushort quantity = ushort.Parse(AppConfig.GetValue("ModbusSettings:Quantity"));
        // int interval = int.Parse(AppConfig.GetValue("ModbusSettings:PollingIntervalMs"));
        //
        // var client = new ModbusTcpClient(ip, port);
        // await client.ConnectAsync();
        //
        // // 启动轮询,使用配置文件中的参数启动
        // client.StartPolling(unitId, startAddr, quantity, interval);
        //
        // //保持主线程不退出
        // Console.WriteLine("服务正在进行中，按回车键终止服务");
        // Console.ReadLine();

        // var client = new ModbusTcpMaster();
        //
        // var config = new TouchSocketConfig();
        //
        // config.SetRemoteIPHost(null);
        // config.ConfigurePlugins(a =>
        // {
        //     a.UseReconnection<ModbusTcpMaster>(options =>
        //     {
        //         options.PollingInterval = TimeSpan.FromSeconds(1);
        //     });
        // });
        //
        // await client.SetupAsync(config);
        // await client.ConnectAsync();
        
        Console.WriteLine("=== Modbus 服务端（主动等待从站连接）模式 ===");
            
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
            
        // 4. 启动轮询定时器（此时 _isConnected 为 false，定时器不会真正发包，直到连接建立）
        serverClient.StartPolling(unitId, startAddr, quantity, interval);

        // 5. 启动服务端监听（这是一个死循环，会阻塞在这里等待远程连接）
        await serverClient.StartServerAsync();

        Console.WriteLine("服务已停止。");
        Console.ReadLine();
    }
}