using System.IO.Ports;
using System.Net.Sockets;
using ModbusLearning.Utils;

namespace ModbusLearning.Services;

public class ModbusRtuClient
{
    private SerialPort _serialPort;
    private byte[] _readBuffer = new byte[1024];
    private List<byte> _cacheBuffer = new List<byte>();

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;

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
        Logger.Info($"已打开串口: {_portName}(波特率:{_baudRate})");
    }
}