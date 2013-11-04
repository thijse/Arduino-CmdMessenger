using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace CommandMessenger
{
    class ReceiveCommandQueue : CommandQueue
    {
        CommunicationManager _communicationManager;
        private char _fieldSeparator;                                       // The field separator
        private char _commandSeparator;                                     // The command separator
        private char _escapeCharacter;                                      // The escape character
        private QueueSpeed queueSpeed = new QueueSpeed(0.5);

        public ReceiveCommandQueue(DisposeStack disposeStack, CmdMessenger cmdMessenger, CommunicationManager communicationManager, char fieldSeparator, 
            char commandSeparator, char escapeCharacter)
            : base(disposeStack, cmdMessenger)
        {
            disposeStack.Push(this);
            
            _communicationManager = communicationManager;            
            _fieldSeparator       = fieldSeparator;
            _commandSeparator     = commandSeparator;
            _escapeCharacter      = escapeCharacter;
            
            communicationManager.NewLinesReceived += OnNewLinesReceived;
        }

        public ReceivedCommand DequeueCommand()
        {
            queueSpeed.CalcSleepTime();
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

        /// <summary> Parse message. </summary>
        /// <param name="line"> The received command line. </param>
        /// <returns> The received command. </returns>
        private ReceivedCommand ParseMessage(string line)
        {
            // Trim and clean line
            var cleanedLine = line.Trim('\r', '\n');
            cleanedLine = Escaping.Remove(cleanedLine, _commandSeparator, _escapeCharacter);

            return
                new ReceivedCommand(Escaping.Split(cleanedLine, _fieldSeparator, _escapeCharacter,
                                    StringSplitOptions.RemoveEmptyEntries));
        }

        private void OnNewLinesReceived(object sender, EventArgs e)
        {
            ProcessLines();
        }

        /// <summary> Converts lines on . </summary>
        public void ProcessLines()
        {
            var line = _communicationManager.ReadLine();
            while(line != null)
            {               
                // Read line from raw buffer and make command
                var currentReceivedCommand = ParseMessage(line);
                currentReceivedCommand.rawString = line;
                // Set time stamp
                currentReceivedCommand.TimeStamp = _communicationManager.LastLineTimeStamp;
                // And put on queue
                QueueCommand(currentReceivedCommand);
                line = _communicationManager.ReadLine();
            }
        }

        protected override void ProcessQueue()
        {
            // Endless loop
            while (ThreadRunState == threadRunStates.Start)
            {
                queueSpeed.Sleep();
                
                var dequeueCommand = DequeueCommand();
                if (dequeueCommand != null)
                {
                    _cmdMessenger.HandleMessage(dequeueCommand);
                }
            }
        }

        public void QueueCommand(ReceivedCommand receivedCommand)
        {
            queueSpeed.addCount();
            QueueCommand(new CommandStrategy(receivedCommand));
        }

        public override void QueueCommand(CommandStrategy commandStrategy)
        {
            lock (_queue)
            {
                // Process all generic enqueue strategies
                _queue.Enqueue(commandStrategy);
                foreach (var generalStrategy in _generalStrategies) { generalStrategy.OnEnqueue(); }
            }
        }

        // Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _communicationManager.NewLinesReceived -= OnNewLinesReceived;
            }
            base.Dispose(disposing);
        }
    }
}
