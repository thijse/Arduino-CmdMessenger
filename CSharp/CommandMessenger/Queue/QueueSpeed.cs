using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CommandMessenger
{
    public class QueueSpeed
    {
        private long QueueCount;
        private long prevTime;
        private long sleepTime;
        private double alpha = 0.8;
        private double _targetQueue = 0.5;
        private long _maxSleep = 50;

        public string Name { get; set;  }

        public  QueueSpeed(double targetQueue)
        {
            _targetQueue = targetQueue;       
            prevTime = TimeUtils.Millis;
            sleepTime = 10;
        }

        public void CalcSleepTime() {
            var currentTime = TimeUtils.Millis;
            var deltaT = (currentTime-prevTime);
            var processT = deltaT- sleepTime;
            double rate = (double)QueueCount / (double)deltaT;
            double targetT = _targetQueue /rate;
            double compensatedT = Math.Min(Math.Max(targetT - processT, 0), 1e6);
            sleepTime = Math.Max(Math.Min((long)(alpha * sleepTime + (1 - alpha) * compensatedT), _maxSleep), 0);

           // if (Name!="" && Name!=null) {
           //     Console.WriteLine("Rate {1} {0}", Name, rate);
           //     Console.WriteLine("Sleeptime {1} {0}", Name, sleepTime);
        //}
            // Reset
            prevTime = currentTime;
            QueueCount = 0;
        }

        public void CalcSleepTimeWithoutLoad()
        {
            var currentTime = TimeUtils.Millis;
            var deltaT = (currentTime - prevTime);
            //var processT = deltaT - sleepTime;
            double rate = (double)QueueCount / (double)deltaT;
            double targetT = _targetQueue / rate;
            //double compensatedT = Math.Min(Math.Max(targetT - processT, 0), 1e6);
            sleepTime = Math.Max(Math.Min((long)(alpha * sleepTime + (1 - alpha) * targetT), _maxSleep), 0);

            //if (Name != "" && Name != null)
            //{
                //Console.WriteLine("Rate {1} {0}", Name, rate);
                //Console.WriteLine("targetT {1} {0}", Name, targetT);
               // Console.WriteLine("sleepTime {1} {0}", Name, sleepTime);
            //}
            // Reset
            prevTime = currentTime;
            QueueCount = 0;
        }


        public void addCount() {
            QueueCount++;
        }

        public void addCount(int count)
        {
            QueueCount+= count;
        }

        public void Sleep() {
            Thread.Sleep(TimeSpan.FromMilliseconds(sleepTime));
        }
    }
}
