#region CmdMessenger - MIT - (c) 2013 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  Copyright 2013 - Thijs Elenbaas
*/
#endregion

namespace CommandMessenger
{


    /// <summary> Queue of received commands.  </summary>
    public class ReceiveCommandQueue : CommandQueue
    {
        private bool _directProcessing = false;

        public event NewLineEvent.NewLineHandler NewLineReceived;

        public ReceivedCommandSignal ReceivedCommandSignal { get; private set; }

        //private readonly QueueSpeed _queueSpeed = new QueueSpeed(0.5,5);

        /// <summary> Receive command queue constructor. </summary>
        /// <param name="disposeStack"> DisposeStack. </param>
        /// <param name="cmdMessenger"> The command messenger. </param>
        public ReceiveCommandQueue(DisposeStack disposeStack, CmdMessenger cmdMessenger )
            : base(disposeStack, cmdMessenger)
        {
            disposeStack.Push(this);
            ReceivedCommandSignal = new ReceivedCommandSignal();
            QueueThread.Name = "ReceiveCommandQueue";
           // _queueSpeed.Name = "ReceiveCommandQueue";
        }

        public void DirectProcessing()
        {
            // Disable processing queue
            ItemPutOnQueueSignal.KeepBlocked();
            _directProcessing = true;
        }

        public void QueuedProcessing()
        {
            // Enable processing queue
            ItemPutOnQueueSignal.Normal(true);
            _directProcessing = false;
        }


        /// <summary> Dequeue the received command. </summary>
        /// <returns> The received command. </returns>
        public ReceivedCommand DequeueCommand()
        {
            ReceivedCommand receivedCommand = null;
            lock (Queue)
            {
                if (!IsEmpty)
                {
                    foreach (var generalStrategy in GeneralStrategies) { generalStrategy.OnDequeue(); }
                    var commandStrategy = Queue.Dequeue();
                    receivedCommand = (ReceivedCommand)commandStrategy.Command;                    
                }
            }        
            return receivedCommand;
        }

        /// <summary> Process the queue. </summary>
        protected override void ProcessQueue()
        {
            var dequeueCommand = DequeueCommand();
            if (dequeueCommand != null)
            {
                CmdMessenger.HandleMessage(dequeueCommand);
            }
        }

        /// <summary> Queue the received command. </summary>
        /// <param name="receivedCommand"> The received command. </param>
        public void QueueCommand(ReceivedCommand receivedCommand)
        {
            QueueCommand(new CommandStrategy(receivedCommand));
        }

        /// <summary> Queue the command wrapped in a command strategy. </summary>
        /// <param name="commandStrategy"> The command strategy. </param>
        public override void QueueCommand(CommandStrategy commandStrategy)
        {
            // See if we should redirect the command to the live thread for synchronous processing
            // or put on the queue
            if (_directProcessing)
            {
                // Directly send this command to waiting thread
                var addToQueue = ReceivedCommandSignal.ProcessCommand((ReceivedCommand)commandStrategy.Command);
                // check if the item needs to be added to the queue for later processing. If not return directly
                if (!addToQueue) return;
            }

            // Put it on the queue
            lock (Queue)
            {
                // Process all generic enqueue strategies
                Queue.Enqueue(commandStrategy);
                foreach (var generalStrategy in GeneralStrategies) { generalStrategy.OnEnqueue(); }
            }

            // If queue-ing, give a signal to queue processor to indicate that a new item has been queued
            if (!_directProcessing)
            {
                ItemPutOnQueueSignal.Set();
                if (NewLineReceived != null) NewLineReceived(this, new NewLineEvent.NewLineArgs(commandStrategy.Command));
            }
        }
    }
}
