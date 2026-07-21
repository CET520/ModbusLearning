using System.IO.Ports;
using ModbusLearning.Models;
using ModbusLearning.Utils;

namespace ModbusLearning.Services;

public class ModbusRtuClient
{
    // 串口号
    private SerialPort _serialPort;
    private byte[] _readBuffer = new byte[1024];
    private List<byte> _cacheBuffer = new List<byte>();
    private System.Threading.Timer _pollingTimer;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private bool _isPortOpen = false;

    public ModbusRtuClient(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
    {
        _portName = portName;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
    }

    public async Task OpenAsync()
    {
        _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits);
        _serialPort.Open();
        _isPortOpen = true;

        Logger.Info($"已打开串口: {_portName},(波特率:{_baudRate})");
        Console.WriteLine($"已打开串口:{_portName}");

        _ = ReceiveLoopAsync();
        await Task.CompletedTask;
    }

    public void StartPolling(byte unitId, ushort startAddress, ushort quantity, int intervalMs = 1000)
    {
        _pollingTimer = new System.Threading.Timer(_ =>
        {
            if (_cts.IsCancellationRequested) return;
            SendReadRequest(unitId, startAddress, quantity);
        }, null, 0, intervalMs);
        Console.WriteLine($"已启动RTU轮询，间隔：{intervalMs}毫秒");
    }

    // 发送读寄存器命令
    public void SendReadRequest(byte unitId, ushort quantity, ushort startAddress)
    {
        try
        {
            if (!_isPortOpen || _serialPort == null) return;
            byte[] request = new byte[8];
            request[0] = unitId;
            request[1] = 0x03;

            byte[] addrBytes = ByteHelper.ToBigEndianBytes(startAddress);
            Array.Copy(addrBytes, 0, request, 2, 2);

            byte[] qtyBytes = ByteHelper.ToBigEndianBytes(quantity);
            Array.Copy(addrBytes, 0, request, 4, 2);

            byte[] crcBytes = Crc16.Calculate(request.Take(6).ToArray());
            request[6] = crcBytes[0];
            request[7] = crcBytes[1];

            _serialPort.Write(request, 0, request.Length);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 已发送RTU请求（设备ID：{unitId}）");
        }
        catch (Exception e)
        {
            Logger.Error($"RTU 发送异常：{e.Message}");
            Console.WriteLine($"RTU 发送异常：{e.Message}");
        }
    }

    // 接收循环（和TCP逻辑类似，使用 BaseStream）
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (_isPortOpen && _serialPort.IsOpen && !_cts.IsCancellationRequested)
            {
                int bytesRead = await _serialPort.BaseStream.ReadAsync(_readBuffer, 0, _readBuffer.Length, _cts.Token);
                if (bytesRead == 0) break;

                _cacheBuffer.AddRange(_readBuffer.Take(bytesRead));

                while (true)
                {
                    // RTU 最小长度：设备ID(1) + 功能码(1) + 长度(1) + CRC(2) = 至少 5 字节
                    if (_cacheBuffer.Count < 5) break;

                    byte dataLen = _cacheBuffer[2];
                    // 完整长度 = 设备ID(1) + 功能码(1) + 数据长度(1) + 数据(dataLen) + CRC(2)
                    int expectedFullLength = 1 + 1 + 1 + dataLen + 2;

                    if (_cacheBuffer.Count < expectedFullLength) break;

                    byte[] fullPacket = _cacheBuffer.Take(expectedFullLength).ToArray();
                    _cacheBuffer.RemoveRange(0, expectedFullLength);

                    ProcessPacket(fullPacket);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("RTU 接收循环正常取消。");
        }
        catch (Exception ex)
        {
            Logger.Error($"RTU 接收异常: {ex.Message}");
        }
    }

// 解析数据 + 校验 CRC
    private void ProcessPacket(byte[] packet)
    {
        // 1. 校验 CRC
        byte[] dataPart = packet.Take(packet.Length - 2).ToArray();
        byte[] receivedCrc = packet.Skip(packet.Length - 2).Take(2).ToArray();
        byte[] calcCrc = Crc16.Calculate(dataPart);

        if (!receivedCrc.SequenceEqual(calcCrc))
        {
            Logger.Error($"CRC校验失败！丢弃无效报文。");
            return;
        }

        Console.WriteLine($"收到有效 RTU 报文: {BitConverter.ToString(packet)}");

        byte functionCode = packet[1];
        if (functionCode == 0x03)
        {
            byte dataLength = packet[2];
            for (int i = 0; i < dataLength; i += 2)
            {
                ushort rawValue = ByteHelper.ToUInt16BigEndian(packet, 3 + i);
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
                    if (_cts.IsCancellationRequested) return; // 若已取消，直接放弃入库
                    try
                    {
                        await DatabaseService.InsertSensorDataAsync(data);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"RTU 数据入库失败:{e.Message}");
                    }
                });
            }
        }
    }

    // 优雅停止
    public async Task StopAsync()
    {
        Console.WriteLine("\n正在优雅关闭 RTU 串口...");
        _cts.Cancel();
        _pollingTimer?.Dispose();

        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        _isPortOpen = false;
        Logger.Info("RTU 串口已完全关闭。");
        await Task.CompletedTask;
    }
    
    public CancellationToken GetCancellationToken() => _cts.Token;
}