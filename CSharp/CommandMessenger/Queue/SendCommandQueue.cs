using System.Collections.Generic;
using System.Threading;

namespace CommandMessenger
{
    class SendCommandQueue
    {
        private readonly Thread _queueThread;
        private readonly ListQueue<CommandStrategy> _queue = new ListQueue<CommandStrategy>();   // Buffer for commands
        private readonly List<GeneralStrategy> _generalStrategies = new List<GeneralStrategy>(); // Buffer for command independent strategies
        private readonly CmdMessenger _cmdMessenger;

        public SendCommandQueue(CmdMessenger cmdMessenger)
        {
            _cmdMessenger = cmdMessenger;
            
            // Create queue thread and wait for it to start
            _queueThread = new Thread(ProcessQueue) {Priority = ThreadPriority.Normal};
            _queueThread.Start();
            while (!_queueThread.IsAlive) {}
        }

        private void ProcessQueue()
        {
            // Endless loop
            while (true)
            {
                SendCommandFromQueue();
            }
        }

        private void SendCommandFromQueue()
        {
            CommandStrategy commandStrategy;
            lock (_queue)
            {
                commandStrategy = _queue.Count != 0 ? _queue.Peek() : null;
                // Process command specific dequeue strategy
                if (commandStrategy != null) { commandStrategy.DeQueue(); }

                // Process all generic dequeue strategies
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnDequeue(); }

            }
            // Send command
            if (commandStrategy != null && commandStrategy.Command != null) _cmdMessenger.SendCommand((SendCommand)commandStrategy.Command);         
        }

        public void QueueCommand(SendCommand sendCommand)
        {
            QueueCommand(new CommandStrategy(sendCommand));
        }

        public void QueueCommand(CommandStrategy commandStrategy)
        {
            lock (_queue)
            {
                // Process commandStrategy enqueue associated with command
                commandStrategy.CommandQueue = _queue;
                commandStrategy.Enqueue();

                // Process all generic enqueue strategies
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnEnqueue(); }
            }
        }

        public void AddGeneralStrategy(GeneralStrategy generalStrategy)
        {
            // Give strategy access to queue
            generalStrategy.CommandQueue = _queue;
            // Add to general strategy list
            _generalStrategies.Add(generalStrategy);
        }
    }
}
