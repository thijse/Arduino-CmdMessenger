using System.Collections.Generic;
using System.Threading;
using System;
using System.Globalization;
namespace CommandMessenger
{
    class SendCommandQueue : CommandQueue
    {
        private QueueSpeed queueSpeed = new QueueSpeed(0.5);

        public SendCommandQueue(DisposeStack disposeStack, CmdMessenger cmdMessenger)
            : base(disposeStack, cmdMessenger)
        {
        }

        protected override void ProcessQueue()
        {
            // Endless loop
            while (ThreadRunState == threadRunStates.Start)
            {
                queueSpeed.Sleep();
                SendCommandFromQueue();
            }
        }

        private void SendCommandFromQueue()
        {
            queueSpeed.CalcSleepTime();
            CommandStrategy commandStrategy;
            lock (_queue)
            {
                commandStrategy = _queue.Count != 0 ? _queue.Peek() : null;
                // Process command specific dequeue strategy
                if (commandStrategy != null)
                { commandStrategy.DeQueue(); }

                // Process all generic dequeue strategies
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnDequeue(); }

            }
            // Send command
            if (commandStrategy != null && commandStrategy.Command != null) 
                _cmdMessenger.SendCommand((SendCommand)commandStrategy.Command);    
     
        }

        public void QueueCommand(SendCommand sendCommand)
        {
            
            QueueCommand(new CommandStrategy(sendCommand));
        }

        public override void QueueCommand(CommandStrategy commandStrategy)
        {
            queueSpeed.addCount();
            lock (_queue)
            {
                // Process commandStrategy enqueue associated with command
                commandStrategy.CommandQueue = _queue;
                commandStrategy.ThreadRunState = ThreadRunState;

                commandStrategy.Enqueue();

                // Process all generic enqueue strategies
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnEnqueue(); }

            }
        }


        // Dispose
        /// <summary> Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. </summary>
        /// <param name="disposing"> true if resources should be disposed, false if not. </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

    }
}
