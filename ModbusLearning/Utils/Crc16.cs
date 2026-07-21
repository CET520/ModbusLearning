namespace ModbusLearning.Utils;

public static class Crc16
{
    //Modbus RTU的CRC16 计算函数
    public static byte[] Calculate(byte[] data)
    {
        ushort crc = 0xFFFF;
        for(int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for(int j = 0; j < 8; j++)
            {
                if ((crc& 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return BitConverter.GetBytes(crc);
    }
}
