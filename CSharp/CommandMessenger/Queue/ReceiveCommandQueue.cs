using System.Collections.Generic;

namespace CommandMessenger
{
    class ReceiveCommandQueue
    {
        private readonly ListQueue<CommandStrategy> _queue = new ListQueue<CommandStrategy>();   // Buffer for commands
        private readonly List<GeneralStrategy> _generalStrategies = new List<GeneralStrategy>(); // Buffer for command independent strategies
        private readonly CmdMessenger _cmdMessenger;

        public ReceiveCommandQueue(CmdMessenger cmdMessenger)
        {
            _cmdMessenger = cmdMessenger;
        }

        public ReceivedCommand DequeueCommand()
        {
            lock (_queue)
            {
                if (_queue.Count != 0)
                {
                    foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnDequeue(); }
                    var commandStrategy = _queue.Dequeue();
                    return (ReceivedCommand)commandStrategy.Command;
                }
                return null;
            }        
        }

        public void QueueCommand(ReceivedCommand receivedCommand)
        {
            var commandStrategy = new CommandStrategy(receivedCommand);
            lock (_queue)
            {
                // Process all generic enqueue strategies
                _queue.Enqueue(commandStrategy);
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnEnqueue(); }
            }
        }

        public void AddGeneralStrategy(GeneralStrategy generalStrategy)
        {
            // Add to general strategy list
            _generalStrategies.Add(generalStrategy);
        }
    }
}
