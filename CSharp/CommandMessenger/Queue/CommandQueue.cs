using System.Collections.Generic;
using System.Threading;

namespace CommandMessenger
{
    public class CommandQueue : DisposableObject
    {
        public enum threadRunStates
        {
            Start,
            Stop,
        }

        protected readonly Thread _queueThread;
       // protected threadRunState _threadRunState;
        protected readonly ListQueue<CommandStrategy> _queue = new ListQueue<CommandStrategy>();   // Buffer for commands
        protected readonly List<GeneralStrategy> _generalStrategies = new List<GeneralStrategy>(); // Buffer for command independent strategies
        protected readonly CmdMessenger _cmdMessenger;

        public threadRunStates ThreadRunState;
        //{
        //    get;
        //    set;
        //}
   
        public CommandQueue(DisposeStack disposeStack, CmdMessenger cmdMessenger) 
        {
            _cmdMessenger = cmdMessenger;
            disposeStack.Push(this);   
            // Create queue thread and wait for it to start
            _queueThread = new Thread(ProcessQueue) {Priority = ThreadPriority.Normal};
            _queueThread.Start();
            while (!_queueThread.IsAlive) {}
        }

        protected virtual void ProcessQueue()
        {
        }

        public void Clear()
        {
            _queue.Clear();
        }

        public virtual void QueueCommand(CommandStrategy commandStrategy)
        {
        }


        public void AddGeneralStrategy(GeneralStrategy generalStrategy)
        {
            // Give strategy access to queue
            generalStrategy.CommandQueue = _queue;
            // Add to general strategy list
            _generalStrategies.Add(generalStrategy);
        }

        // Dispose
        /// <summary> Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. </summary>
        /// <param name="disposing"> true if resources should be disposed, false if not. </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Stop polling
                _queueThread.Abort();
                _queueThread.Join();
            }
            base.Dispose(disposing);
        }

    }
}
