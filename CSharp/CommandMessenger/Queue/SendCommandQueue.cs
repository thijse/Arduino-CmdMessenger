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

using System;
using System.Threading;

namespace CommandMessenger.Queue
{
    /// <summary> Queue of received commands.  </summary>
    class SendCommandQueue : CommandQueue
    {
        private readonly Sender _sender;
        public event NewLineEvent.NewLineHandler NewLineSent;
        private readonly int _sendBufferMaxLength = 62;
        string _sendBuffer = string.Empty;
        int _commandCount;

        public uint MaxQueueLength { get; set; }


        /// <summary> send command queue constructor. </summary>
        /// <param name="sender">Object that does the actual sending of the command</param>
        /// <param name="sendBufferMaxLength">Length of the send buffer</param>
        public SendCommandQueue(Sender sender, int sendBufferMaxLength)
        {
            MaxQueueLength = 5000;
            _sender = sender;
            _sendBufferMaxLength = sendBufferMaxLength;
        }

        /// <summary> Process the queue. </summary>
        protected override void ProcessQueue()
        {
            SendCommandsFromQueue();
        }

        /// <summary> Sends the commands from queue. All commands will be combined until either
        /// 		   the SendBufferMaxLength  has been reached or if a command requires an acknowledge
        /// 		   </summary>
        private void SendCommandsFromQueue()
        {
            _commandCount = 0;
            _sendBuffer = string.Empty;
            CommandStrategy eventCommandStrategy = null;

            // while maximum buffer string is not reached, and command in queue, AND    
            while (_sendBuffer.Length < _sendBufferMaxLength && Queue.Count > 0)
            {
                lock (Queue)
                {
                    var commandStrategy = !IsEmpty ? Queue.Peek() : null;
                    if (commandStrategy != null)
                    {
                        if (commandStrategy.Command != null)
                        {
                            var sendCommand = (SendCommand)commandStrategy.Command;

                            if (sendCommand.ReqAc)
                            {
                                if (_commandCount > 0)
                                {
                                    break;
                                }
                                SendSingleCommandFromQueue(commandStrategy);
                            }
                            else
                            {                                
                                eventCommandStrategy = commandStrategy;
                                AddToCommandsString(commandStrategy);
                            }
                        }                        
                    }
                }
                // event callback outside lock for performance
                if (eventCommandStrategy != null)
                {
                    if (NewLineSent != null) NewLineSent(this, new NewLineEvent.NewLineArgs(eventCommandStrategy.Command));
                    eventCommandStrategy = null;
                }
            }

            // Now check if a command string has been filled
            if (_sendBuffer.Length > 0)
            {
                _sender.ExecuteSendString(_sendBuffer, SendQueue.InFrontQueue);              
            }
        }

        /// <summary> Sends a float command from the queue. </summary>
        /// <param name="commandStrategy"> The command strategy to send. </param>
        private void SendSingleCommandFromQueue(CommandStrategy commandStrategy)
        {
            // Dequeue
            lock (Queue)
            {
                commandStrategy.DeQueue();
                // Process all generic dequeue strategies
                foreach (var generalStrategy in GeneralStrategies) { generalStrategy.OnDequeue(); }
            }
            // Send command
            if (commandStrategy.Command != null)
                _sender.ExecuteSendCommand((SendCommand)commandStrategy.Command, SendQueue.InFrontQueue);                     
        }

        /// <summary> Adds a commandStrategy to the commands string.  </summary>
        /// <param name="commandStrategy"> The command strategy to add. </param>
        private void AddToCommandsString(CommandStrategy commandStrategy)
        {
            // Dequeue
            lock (Queue)
            {
                commandStrategy.DeQueue();
                // Process all generic dequeue strategies
                foreach (var generalStrategy in GeneralStrategies) { generalStrategy.OnDequeue(); }
            }
            // Add command
            if (commandStrategy.Command != null) {
                    _commandCount++;
                    _sendBuffer += commandStrategy.Command.CommandString();
                    if (Command.PrintLfCr) { _sendBuffer +=  Environment.NewLine; }
            }
        }

        /// <summary> Sends a command. Note that the command is put at the front of the queue </summary>
        /// <param name="sendCommand"> The command to sent. </param>
        public void SendCommand(SendCommand sendCommand)
        {
            // Add command to front of queue
            QueueCommand(new TopCommandStrategy(sendCommand));
        }

        /// <summary> Queue the send command. </summary>
        /// <param name="sendCommand"> The command to sent. </param>
        public void QueueCommand(SendCommand sendCommand)
        {            
            QueueCommand(new CommandStrategy(sendCommand));
        }

        /// <summary> Queue the send command wrapped in a command strategy. </summary>
        /// <param name="commandStrategy"> The command strategy. </param>
        public override void QueueCommand(CommandStrategy commandStrategy)
        {
            while (Queue.Count > MaxQueueLength)
            {
                Thread.Yield();
            }

            lock (Queue)
            {
                // Process commandStrategy enqueue associated with command
                commandStrategy.CommandQueue = Queue;
                //commandStrategy.ThreadRunState = ThreadRunState;

                commandStrategy.Enqueue();

                // Process all generic enqueue strategies
                foreach (var generalStrategy in GeneralStrategies) { generalStrategy.OnEnqueue(); }
            }

            SignalWaiter();
        }
    }
}
