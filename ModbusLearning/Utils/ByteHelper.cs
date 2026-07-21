namespace ModbusLearning.Utils;
    public static class ByteHelper
    {
        // 将 2 字节的 ushort（无符号短整型）转为大端序字节数组
        public static byte[] ToBigEndianBytes(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes); // 反转转为大端
            return bytes;
        }

        // 从大端序字节数组中读取 ushort
        public static ushort ToUInt16BigEndian(byte[] buffer, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                // 手动合并两个字节，高位在前
                return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
            }
            return BitConverter.ToUInt16(buffer, offset);
        }
    }
