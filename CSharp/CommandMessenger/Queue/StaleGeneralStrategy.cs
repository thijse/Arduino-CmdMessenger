using System;

namespace CommandMessenger
{
    public class StaleGeneralStrategy : GeneralStrategy
    {
        private readonly long _commandTimeOut;

        public StaleGeneralStrategy(long commandTimeOut)
        {
            _commandTimeOut = commandTimeOut;
        }

        public override void OnDequeue()
        {
            // Remove commands that have gone stale
            //Console.WriteLine("Before StaleStrategy {0}", CommandQueue.Count);
            var currentTime = TimeUtils.Millis;
            // Work from oldest to newest
            for (var item = 0; item < CommandQueue.Count; item++)
            {
                var age = currentTime - CommandQueue[item].Command.TimeStamp;
                if (age > _commandTimeOut && CommandQueue.Count > 1 )
                {
                    CommandQueue.RemoveAt(item);
                }
                else
                {
                    // From here on commands are newer, so we can stop
                    break;
                }
            }
            //Console.WriteLine("After StaleStrategy {0}", CommandQueue.Count);
        }
    }
}
