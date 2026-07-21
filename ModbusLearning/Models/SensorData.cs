using System;
using System.Collections.Generic;
using System.Text;

namespace ModbusLearning.Models;
public class SensorData
{
    public int RegisterAddress { get; set; }//寄存器地址

    public ushort RawValue { get; set; } //寄存器值

    public double PhysicalValue { get; set; } //物理值

    public DateTime Timestamp { get; set; }


    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] SensorData类的数据: \n寄存器地址RegisterAddress={RegisterAddress}, \n原始值RawValue={RawValue}, \n物理量PhysicalValue={PhysicalValue:F2}";
    }
}
