using System.IO.Ports;
using ModbusLearning.Services;
using ModbusLearning.Utils;
using Org.Apache.Rocketmq;

namespace ModbusLearning;
// 主程序入口
internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== 项目运行菜单 ===");
        Console.WriteLine("1、运行TCP协议的Modbus");
        Console.WriteLine("2、运行RTU协议的Modbus");
        Console.WriteLine("3、RocketMQ运行测试");

        var choice = Console.ReadLine();
        
        if ("1" == choice)
        {
            await RunTcpModeAsync();
        }
        else if ("2" == choice)
        {
            await RunRtuModeAsync();
        }
        else if ("3" == choice)
        {
            await RocketMqTest();
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
    
    
//  RocketMQ 消息发送测试
    private static async Task RocketMqTest()
    {
        var endpoint = AppConfig.GetValue("RocketMqSettings:RocketMqEndpoint");
        var accessKey = AppConfig.GetValue("RocketMqSettings:RocketMqAccessKey");
        var secretKey = AppConfig.GetValue("RocketMqSettings:RocketMqSecretKey");
        const string topic = "ModbusTopic";

        Console.WriteLine("=== RocketMQ 消息发送测试 ===");
        Console.WriteLine($"Endpoints地址: {endpoint}");

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var credentialsProvider = new StaticSessionCredentialsProvider(accessKey, secretKey);
// 客户端配置构建
        var clientConfig = new ClientConfig.Builder()
            .SetEndpoints(endpoint)
            .SetCredentialsProvider(credentialsProvider)
            .Build();
        
        // 构建生产者
        var producer = await new Producer.Builder()
            .SetTopics(topic)
            .SetClientConfig(clientConfig)
            .Build();

        Console.WriteLine("RocketMQ 生产者启动成功");

        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("Hello RocketMQ from ModbusLearning!");
            // 构建消息对象
            var message = new Message.Builder()
                .SetTopic(topic)
                .SetBody(bytes)
                .SetTag("TestTag")
                .SetKeys("ModbusMessageKey")
                .Build();
//          生产者发送信息
            var sendReceipt = await producer.Send(message);
            Console.WriteLine($"消息发送成功！MessageId: {sendReceipt.MessageId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"消息发送失败: {ex.Message}");
        }
        finally
        {
            await producer.DisposeAsync();
            Console.WriteLine("RocketMQ 生产者已关闭");
        }
    }
}