using System;

namespace ChobiIot.Models
{
    public class SensorReadingBuffer
    {
        public double Temperature { get; set; }
        public double Brightness { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
