#region CmdMessenger - MIT - (c) 2014 Thijs Elenbaas.
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

  Copyright 2014 - Thijs Elenbaas
*/

#endregion

using System;
using System.Threading;

namespace CommandMessenger
{
    // This class will trigger the main thread when a specific command is received on the ReceiveCommandQueue thread
    // this is used when synchronously waiting for an acknowledge command in BlockedTillReply
    public class ReceivedCommandSignal
    {
        public enum WaitState
        {
            TimeOut,
            Normal
        }

        private readonly object _key = new object();
        private bool _waitingForCommand;
        private bool _waitingForCommandProcessed;
        private int? _cmdIdToMatch;
        private SendQueue _sendQueueState;
        private ReceivedCommand _receivedCommand;


        /// <summary>
        /// start blocked (waiting for signal)
        /// </summary>
        public ReceivedCommandSignal() 
        {
            lock (_key)
            {
                _waitingForCommand = true;
                _waitingForCommandProcessed = false;
                Monitor.Pulse(_key);
            }
        }

        /// <summary>
        /// start blocked or signalled. 
        /// </summary>
        /// <param name="set">If true, first Wait will directly continue</param>
        public ReceivedCommandSignal(bool set)
        {
            lock (_key)
            {
                _waitingForCommand = !set;
                _waitingForCommandProcessed = false;
                Monitor.Pulse(_key);
            }
        }

        /// <summary>
        /// Wait function. 
        /// </summary>
        /// <param name="timeOut">time-out in ms</param>
        /// <param name="cmdId"></param>
        /// <param name="sendQueueState"></param>
        /// <returns></returns>
        public ReceivedCommand WaitForCmd(int timeOut, int cmdId, SendQueue sendQueueState)
        {
            lock (_key)
            {
                // Todo: this makes sure ProcessCommand is not waiting anymore
                // this sometimes seems to happen but should not
                _waitingForCommandProcessed = false;
                Monitor.Pulse(_key);

                _cmdIdToMatch = cmdId;
                _sendQueueState = sendQueueState;

                Logger.LogLine("Waiting for Command");

                // Wait under conditions
                var noTimeOut = true;
                while (noTimeOut && _waitingForCommand)
                {
                    noTimeOut = Monitor.Wait(_key, timeOut);
                }

                // Block Wait for next entry
                _waitingForCommand = true;

                // Signal to continue listening for next command
                _waitingForCommandProcessed = false;

                if (_receivedCommand == null)
                {

                }
                else
                {
                    Logger.LogLine("Command " + _receivedCommand.CmdId + "was received in main thread");
                }
                Monitor.Pulse(_key);

                // Reset CmdId to check on
                _cmdIdToMatch = null;

                // Return whether the Wait function was quit because of an Set event or timeout
                return noTimeOut ? _receivedCommand : null;
            }
        }


        /// <summary>
        /// Process command. See if it needs to be send to the main thread (false) or be used in queue (true)
        /// </summary>
        public bool ProcessCommand(ReceivedCommand receivedCommand)
        {
            lock (_key)
            {
                _receivedCommand  = receivedCommand;
                var receivedCmdId = _receivedCommand.CmdId;

                // If main thread is not waiting for any command (not waiting for acknowlegde)
                if (_cmdIdToMatch == null)
                {
                    throw new Exception("should not happen");
                    //return true;
                }
                else
                {
                    if (_cmdIdToMatch == receivedCmdId)
                    {
                        // Commands match! Sent a signal
                        _waitingForCommand = false;
                        Monitor.Pulse(_key);
                        Logger.LogLine("Send command "+receivedCommand.CmdId + " to main thread");

                        // Wait for response
                        while (_waitingForCommandProcessed)
                        {
                            // Todo: timeout seems to be needed, otherwise sometimes a block occurred. Should not happen                            
                             Monitor.Wait(_key,100);
                             Logger.LogLine("Command " + receivedCommand.CmdId + " was send to main thread");
                        }

                        // Block Wait for next entry
                        _waitingForCommandProcessed = true;

                        // Main thread wants this command
                        return false;
                    }
                    else
                    {
                        //Commands do not match, so depending of SendQueue state dump or put on queue
                        return (_sendQueueState != SendQueue.ClearQueue);
                    }
                }
            }
        }
    }
}
