using System.Net;
using System.Net.Sockets;
using ModbusLearning.Models;
using ModbusLearning.Utils;

namespace ModbusLearning.Services;

public class ModbusTcpServerClient
{
    // 服务端监听器
    private TcpListener _listener;
    // 远程从站连接上来的实例
    private TcpClient _remoteClient;
    // 网络流
    private NetworkStream _stream;
    
    // 接收缓存与黏包处理
    private byte[] _readBuffer = new byte[1024];
    private List<byte> _cacheBuffer = new List<byte>(); 
    
    // 轮询定时器
    private System.Threading.Timer _pollingTimer; 
    private ushort _transactionId = 0x0001;
    
    // 服务端配置
    private readonly string _listenIp;
    private readonly int _listenPort;
    private bool _isConnected = false; // 是否已连接状态标记

    private CancellationTokenSource _cts;

    public ModbusTcpServerClient(string listenIp, int listenPort)
    {
        _listenIp = listenIp;
        _listenPort = listenPort;
        _cts = new CancellationTokenSource();
    }

    // 服务端启动：循环等待从站连接
    public async Task StartServerAsync()
    {
        _listener = new TcpListener(IPAddress.Parse(_listenIp), _listenPort);
        _listener.Start();
        Logger.Info($"Modbus服务端已启动，监听 {_listenIp}:{_listenPort}，等待远程从站连接...");
        Console.WriteLine($"Modbus服务端已启动，监听 {_listenIp}:{_listenPort}，等待远程从站连接...");

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var acceptTask = _listener.AcceptTcpClientAsync();
                // 阻塞等待客户端连接
                var completedTask = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, _cts.Token));
                //如果要求取消
                if (_cts.Token.IsCancellationRequested) break;

                _remoteClient = await _listener.AcceptTcpClientAsync();
                _stream = _remoteClient.GetStream();
                _isConnected = true;

                Logger.Info("远程从站已成功连接！");
                Console.WriteLine("远程从站已成功连接！");

                // 启动接收循环（和之前 TCP 客户端逻辑完全一样）
                _ = ReceiveLoopAsync();
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"等待连接异常: {ex.Message}");
                await Task.Delay(3000, _cts.Token).ContinueWith(t =>
                {
                    //忽略取消时的异常
                }); // 异常后等待3秒再重新开始监听
            }
        }
    }

    // 启动轮询（必须在 _isConnected = true 之后才会真正发送数据）
    public void StartPolling(byte unitId, ushort startAddr, ushort quantity, int intervalMs = 2000)
    {
        _pollingTimer = new System.Threading.Timer(_ =>
        {
            if (_cts.IsCancellationRequested) return;
            SendReadRequest(unitId, startAddr, quantity);
        }, null, 0, intervalMs);

        Logger.Info($"已启动轮询，间隔时间: {intervalMs} 毫秒");
        Console.WriteLine($"已启动轮询，间隔时间: {intervalMs} 毫秒");
    }

    // 发送读取保持寄存器
    public void SendReadRequest(byte unitId, ushort startAddress, ushort quantity)
    {
        try
        {
            // 关键检查：只有远程设备连上来了，才能发送请求
            if (!_isConnected || _remoteClient == null || !_remoteClient.Connected)
            {
                // 如果没连上，直接静默返回，等到下一次轮询再尝试（不报错刷屏）
                return; 
            }

            byte[] transId = ByteHelper.ToBigEndianBytes(_transactionId++);
            byte[] request = new byte[12];
            Array.Copy(transId, 0, request, 0, 2);

            request[2] = 0x00; request[3] = 0x00;
            request[4] = 0x00; request[5] = 0x06;
            request[6] = unitId;
            request[7] = 0x03;

            byte[] addrBytes = ByteHelper.ToBigEndianBytes(startAddress);
            Array.Copy(addrBytes, 0, request, 8, 2);

            byte[] quantityBytes = ByteHelper.ToBigEndianBytes(quantity);
            Array.Copy(quantityBytes, 0, request, 10, 2);

            // 写入流
            _stream.Write(request, 0, request.Length);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} 事务ID：{_transactionId - 1}] 已发送请求");
        }
        catch (Exception ex)
        {
            // 一旦发送失败，大概率是远程设备掉线了，断开连接等待重连
            Logger.Error($"发送异常: {ex.Message}，远程从站可能已掉线，等待重连...");
            _isConnected = false;
            _stream?.Close();
            _remoteClient?.Close();
        }
    }

    // 接收循环（原封不动，非常稳定）
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (_isConnected && _remoteClient.Connected && !_cts.IsCancellationRequested)
            {
                int bytesRead = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length,_cts.Token);
                if (bytesRead == 0) break; // 远程关闭连接
                _cacheBuffer.AddRange(_readBuffer.Take(bytesRead));

                while (true)
                {
                    if (_cacheBuffer.Count < 8) break;
                    ushort length = ByteHelper.ToUInt16BigEndian(_cacheBuffer.ToArray(), 4);
                    int expectedFullLength = 6 + length;

                    if (_cacheBuffer.Count < expectedFullLength) break;

                    byte[] fullPacket = _cacheBuffer.Take(expectedFullLength).ToArray();
                    _cacheBuffer.RemoveRange(0, expectedFullLength);
                    ProcessPacket(fullPacket);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("接收循环已取消(非报错)");
        }
        catch (Exception ex)
        {
            Logger.Error($"接收循环异常: {ex.Message}");
        }
        finally
        {
            // 退出循环，说明连接已断开
            _isConnected = false;
            _stream?.Close();
            _remoteClient?.Close();
            Logger.Info("远程从站已断开连接，等待下一次连接...");
            Console.WriteLine("远程从站已断开连接，等待下一次连接...");
        }
    }

    // 报文解析（和之前完全一致）
    private void ProcessPacket(byte[] packet)
    {
        Console.WriteLine($"收到完整报文 (长度: {packet.Length})，内容: {BitConverter.ToString(packet)}");

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

                _ = Task.Run(async () =>
                {
                    if (_cts.IsCancellationRequested) return;
                    try { await DatabaseService.InsertSensorDataAsync(data); }
                    catch (Exception e) { Logger.Error($"数据库写入失败:{e.Message}"); }
                });
            }
        }
    }
    
    public async Task StopAsync()
    {
        Console.WriteLine("正在停止Modbus服务端...");
// 发送停止信号
        _cts.Cancel();
// 停止轮询定时器
        _pollingTimer?.Dispose();
        // 关闭底层连接和监听
        _stream?.Close();
        _remoteClient?.Close();
        _listener?.Stop();
        
        Logger.Info("Modbus 服务端已完全停止服务");
        Console.WriteLine("Modbus 服务端已完全停止");
        await Task.CompletedTask;
    }
}