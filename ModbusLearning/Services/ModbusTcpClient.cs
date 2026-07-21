using System.Net.Sockets;
using ModbusLearning.Models;
using ModbusLearning.Utils;

namespace ModbusLearning.Services;

public class ModbusTcpClient
{
    //TCP连接实例
    public TcpClient _tcpClient;
    // 网络流实例
    private NetworkStream _stream;
    // 接收缓存区
    private byte[] _readBuffer = new byte[1024]; 
    // 黏包处理缓存区
    private List<byte> _cacheBuffer = new List<byte>();
    // 轮询定时器
    private System.Threading.Timer _pollingTimer; 
    // 自增事务ID
    private ushort _transactionId = 0x0001;
    //保存服务器配置
    private readonly string _ip;
    private readonly int _port;

    //构造函数，初始化服务器配置
    public ModbusTcpClient(string ip,int port)
    {
        _ip = ip;
        _port = port;
    }

    //异步连接
    public async Task ConnectAsync()
    {
        // 创建 TCP 客户端实例
        _tcpClient = new TcpClient();
        // 异步启动连接
        await _tcpClient.ConnectAsync(_ip, _port);
        // 获取网络流
        _stream = _tcpClient.GetStream();
        Logger.Info($"已连接 Modbus TCP 服务器: {_ip}:{_port}");
        Console.WriteLine($"已连接 Modbus TCP 服务器: {_ip}:{_port}");

        // 开始异步接收数据
        _ = ReceiveLoopAsync();
    }
    
    // 轮询方法
    public void StartPolling(byte unitId,ushort startAddr,ushort quantity,int intervalMs = 2000)
    {
        _pollingTimer = new System.Threading.Timer(_ =>
        {
            SendReadRequest(unitId, startAddr, quantity);
        },null,0,intervalMs);
        Logger.Info($"已启动轮询，间隔时间: {intervalMs} 毫秒");
        Console.WriteLine($"已启动轮询，间隔时间: {intervalMs} 毫秒");
    }

    // 核心：发送读取保持寄存器（功能码 0x03）的请求
    public void SendReadRequest(byte unitId, ushort startAddress, ushort quantity)
    {
        try
        {
            //检查连接的实例是否为空或连接
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                Console.WriteLine("【网络】检测到连接断开，尝试重连中");
                return;
            }

            byte[] transId = ByteHelper.ToBigEndianBytes(_transactionId++);

            byte[] request = new byte[12];
            Array.Copy(transId, 0, request, 0, 2);

            request[2] = 0x00;
            request[3] = 0x00;

            request[4] = 0x00;
            request[5] = 0x06;

            request[6] = unitId;

            request[7] = 0x03; // 功能码

            byte[] addrBytes = ByteHelper.ToBigEndianBytes(startAddress);
            Array.Copy(addrBytes, 0, request, 8, 2);

            byte[] quantityBytes = ByteHelper.ToBigEndianBytes(quantity);
            Array.Copy(quantityBytes, 0, request, 10, 2);

            //发送
            _stream.Write(request, 0, request.Length);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} 事务ID：{_transactionId - 1}] 已发送请求");
        }
        catch (Exception e)//捕获异常
        {
            {
                Logger.Error($"发送失败{e.Message}，连接已断开，下次轮询将重新尝试连接。");
                // Console.WriteLine($"发送失败{e.Message}，连接已断开，下次轮询将重新尝试连接。");

                _stream?.Close();
                _tcpClient?.Close();
            }
        }
    }
    
    // 发送写请求
    public void SendWriteRequest(byte unitId, ushort startAddress, ushort quantity)
    {
        try
        {
            //检查连接的实例是否为空或连接
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                Console.WriteLine("【网络】检测到连接断开，尝试重连中");
                return;
            }

            byte[] transId = ByteHelper.ToBigEndianBytes(_transactionId++);

            byte[] request = new byte[12];
            Array.Copy(transId, 0, request, 0, 2);

            request[2] = 0x00;
            request[3] = 0x00;

            request[4] = 0x00;
            request[5] = 0x06;

            request[6] = unitId;

            request[7] = 0x03; // 功能码

            byte[] addrBytes = ByteHelper.ToBigEndianBytes(startAddress);
            Array.Copy(addrBytes, 0, request, 8, 2);

            byte[] quantityBytes = ByteHelper.ToBigEndianBytes(quantity);
            Array.Copy(quantityBytes, 0, request, 10, 2);

            //发送
            _stream.Write(request, 0, request.Length);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} 事务ID：{_transactionId - 1}] 已发送请求");
        }
        catch (Exception e)//捕获异常
        {
            {
                Logger.Error($"发送失败{e.Message}，连接已断开，下次轮询将重新尝试连接。");
                // Console.WriteLine($"发送失败{e.Message}，连接已断开，下次轮询将重新尝试连接。");

                _stream?.Close();
                _tcpClient?.Close();
            }
        }
    }

    // 异步循环接收数据，解决黏包问题
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (_tcpClient.Connected)
            {
                int bytesRead = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
                if (bytesRead == 0) break;

                // 1. 将新收到的数据追加到缓存区
                _cacheBuffer.AddRange(_readBuffer.Take(bytesRead));

                // 2. 循环解析缓存区里的数据，直到不够组成一个完整包
                while (true)
                {
                    // Modbus TCP 最少 8 字节（MBAP头）
                    if (_cacheBuffer.Count < 8) break;

                    // 解析 MBAP 头中的 "长度" 字段 (字节索引 4 和 5，大端)
                    ushort length = ByteHelper.ToUInt16BigEndian(_cacheBuffer.ToArray(), 4);
                    // 完整帧长度 = 前面 8 个字节（含长度字段自身 + 单元ID） + 后面数据长度
                    int frameTotalLength = 8 + (length - 1);
                    // 注：长度字段的值 = 单元ID(1) + 功能码(1) + 数据长度(N) = 2 + N，所以实际总长度 = 8 + (长度值 - 1) = 7 + 长度值？
                    // 更简单可靠的计算：总长度 = 6 + length。 
                    int expectedFullLength = 6 + length;

                    if (_cacheBuffer.Count < expectedFullLength)
                    {
                        // 数据不够，说明发生了拆包，跳出循环等下一次接收
                        break;
                    }

                    // 数据够包了！切出来处理
                    byte[] fullPacket = _cacheBuffer.Take(expectedFullLength).ToArray();
                    _cacheBuffer.RemoveRange(0, expectedFullLength); // 从缓存中移除已处理的包

                    // 处理这个完整的包
                    ProcessPacket(fullPacket);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"接收异常: {ex.Message}");
            // Console.WriteLine($"接收异常: {ex.Message}");
        }
    }

    // 解析完整报文
    private void ProcessPacket(byte[] packet)
    {
        //校验事务ID是否匹配（目前忽略）
        Console.WriteLine($"收到完整报文 (长度: {packet.Length})，内容: {BitConverter.ToString(packet)}");

        // 解析功能码
        byte functionCode = packet[7];
        if (functionCode == 0x03)
        {
            byte dataLength = packet[8];
            for (int i = 0; i < dataLength; i += 2)
            {
                ushort rawValue = ByteHelper.ToUInt16BigEndian(packet, 9 + i);
                double physicalValue = (rawValue / 65535.0) * 100.0;

                var data = new SensorData
                {
                    RegisterAddress = (ushort)(i / 2),
                    RawValue = rawValue,
                    PhysicalValue = physicalValue,
                    Timestamp = DateTime.Now
                };

                Console.WriteLine($"-> {data}");

                //将数据异步存入数据库，不阻塞主流程
                _ = Task.Run(async () =>
                    {
                    try
                    {
                        await DatabaseService.InsertSensorDataAsync(data);
                    }
                    catch (Exception e) 
                    {
                        // Console.WriteLine($"[ERROR] 数据库写入失败:{e.Message}");
                        Logger.Error($"[ERROR] 数据库写入失败:{e.Message}");
                    }
                });
            }
        }
    }
}