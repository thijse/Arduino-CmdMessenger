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
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;

namespace CommandMessenger
{
    public class ConnectionManagerProgressEventArgs : EventArgs
    {
        public int Level { get; set; }
        public String Description { get; set; }
    }

    public enum ConnectionManagerState
    {
        Scan,
        Connect,
        Watchdog,
        Wait,
        Stop
    }

    public abstract class ConnectionManager : IDisposable 
    {
        protected readonly CmdMessenger CmdMessenger;
        protected ConnectionManagerState ConnectionManagerState;

        public event EventHandler ConnectionTimeout;
        public event EventHandler ConnectionFound;
        public event EventHandler<ConnectionManagerProgressEventArgs> Progress;

        private readonly BackgroundWorker _workerThread;
        private readonly int _identifyCommandId;
        private readonly string _uniqueDeviceId;

        private long _lastCheckTime;
        private long _nextTimeOutCheck;
        private uint _watchdogTries;
        private bool _watchdogEnabled;

        /// <summary>
        /// Is connection manager currently connected to device.
        /// </summary>
        public bool Connected { get; protected set; }

        public int WatchdogTimeout { get; set; }
        public int WatchdogRetryTimeout { get; set; }
        public uint WatchdogTries { get; set; }

        internal Control ControlToInvokeOn { get { return CmdMessenger.ControlToInvokeOn; } }

        /// <summary>
        /// Enables or disables connection watchdog functionality using identify command and unique device id.
        /// </summary>
        public bool WatchdogEnabled
        {
            get { return _watchdogEnabled; }
            set
            {
                if (value && string.IsNullOrEmpty(_uniqueDeviceId))
                    throw new InvalidOperationException("Watchdog can't be enabled without Unique Device ID.");
                _watchdogEnabled = value;
            }
        }

        /// <summary>
        /// Use this property to tell connection manager to connect only to specific port provided by transport.
        /// </summary>
        public bool UseFixedPort { get; set; }

        /// <summary>
        /// Enables or disables storing of last connection configuration in persistent file.
        /// </summary>
        public bool PersistentSettings { get; set; }

        protected ConnectionManager(CmdMessenger cmdMessenger, int identifyCommandId = 0, string uniqueDeviceId = null)
        {
            if (cmdMessenger == null)
                throw new ArgumentNullException("cmdMessenger", "Command Messenger is null.");

            _identifyCommandId = identifyCommandId;
            _uniqueDeviceId = uniqueDeviceId;

            WatchdogTimeout = 3000;
            WatchdogRetryTimeout = 1000;        
            WatchdogTries = 3;
            WatchdogEnabled = false;

            PersistentSettings = false;
            UseFixedPort = false;
            
            CmdMessenger = cmdMessenger;

            ConnectionManagerState = ConnectionManagerState.Stop;

            _workerThread = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = false };

            if (!string.IsNullOrEmpty(uniqueDeviceId))
                CmdMessenger.Attach(identifyCommandId, OnIdentifyResponse);
        }

        /// <summary>
        /// Start connection manager.
        /// </summary>
        public virtual bool StartConnectionManager()
        {
            if (ConnectionManagerState == ConnectionManagerState.Stop)
            {
                ConnectionManagerState = ConnectionManagerState.Wait;

                bool canStart = !_workerThread.IsBusy;
                if (canStart)
                {
                    _workerThread.DoWork += WorkerThreadDoWork;
                    // Start the asynchronous operation.
                    _workerThread.RunWorkerAsync();
                }

                if (UseFixedPort)
                {
                    StartConnect();
                }
                else
                {
                    StartScan();
                }

                return canStart;
            }

            return false;
        }

        /// <summary>
        /// Stop connection manager.
        /// </summary>
        public virtual void StopConnectionManager()
        {
            if (ConnectionManagerState != ConnectionManagerState.Stop)
            {
                ConnectionManagerState = ConnectionManagerState.Stop;

                if (_workerThread.WorkerSupportsCancellation)
                {
                    // Cancel the asynchronous operation.
                    _workerThread.CancelAsync();
                }

                _workerThread.DoWork -= WorkerThreadDoWork;
            }
        }

        protected virtual void ConnectionFoundEvent()
        {
            ConnectionManagerState = ConnectionManagerState.Wait;

            if (WatchdogEnabled) StartWatchDog();

            InvokeEvent(ConnectionFound);
        }

        protected virtual void ConnectionTimeoutEvent()
        {
            Disconnect();

            InvokeEvent(ConnectionTimeout);

            if (WatchdogEnabled)
            {
                StopWatchDog();

                if (UseFixedPort)
                {
                    StartConnect();
                }
                else
                {
                    StartScan();
                }
            }
        }

        protected virtual void InvokeEvent(EventHandler eventHandler)
        {
            try
            {
                if (eventHandler == null || (ControlToInvokeOn != null && ControlToInvokeOn.IsDisposed)) return;
                if (ControlToInvokeOn != null && ControlToInvokeOn.InvokeRequired)
                {
                    //Asynchronously call on UI thread
                    ControlToInvokeOn.BeginInvoke((MethodInvoker)(() => eventHandler(this, null)));
                    Thread.Yield();
                }
                else
                {
                    //Directly call
                    eventHandler(this, null);
                }
            }
            catch (Exception)
            {
            }
        }

        protected virtual void InvokeEvent<TEventHandlerArguments>(EventHandler<TEventHandlerArguments> eventHandler,
            TEventHandlerArguments eventHandlerArguments) where TEventHandlerArguments : EventArgs
        {
            try
            {
                if (eventHandler == null || (ControlToInvokeOn != null && ControlToInvokeOn.IsDisposed)) return;
                if (ControlToInvokeOn != null && ControlToInvokeOn.InvokeRequired)
                {
                    //Asynchronously call on UI thread
                    ControlToInvokeOn.BeginInvoke((MethodInvoker) (() => eventHandler(this, eventHandlerArguments)));
                    Thread.Yield();
                }
                else
                {
                    //Directly call
                    eventHandler(this, eventHandlerArguments);
                }
            }
            catch (Exception)
            {
            }
        }

        protected virtual void Log(int level, string logMessage)
        {
            var args = new ConnectionManagerProgressEventArgs {Level = level, Description = logMessage};
            InvokeEvent(Progress, args);
        }

        protected virtual void OnIdentifyResponse(ReceivedCommand responseCommand)
        {
            if (responseCommand.Ok && !string.IsNullOrEmpty(_uniqueDeviceId))
            {
                ValidateDeviceUniqueId(responseCommand);
            }
        }

        private void WorkerThreadDoWork(object sender, DoWorkEventArgs e)
        {
            if (Thread.CurrentThread.Name == null) Thread.CurrentThread.Name = "SerialConnectionManager";

            while (ConnectionManagerState != ConnectionManagerState.Stop)
            {
                // Check if thread is being canceled
                var worker = sender as BackgroundWorker;
                if (worker != null && worker.CancellationPending)
                {
                    break;
                }

                // Switch between waiting, device scanning and watchdog 
                switch (ConnectionManagerState)
                {
                    case ConnectionManagerState.Scan:
                        DoWorkScan();
                        break;
                    case ConnectionManagerState.Connect:
                        DoWorkConnect();
                        break;
                    case ConnectionManagerState.Watchdog:
                        DoWorkWatchdog();
                        break;
                }
                // Sleep a bit before checking again. If not present, the connection manager will 
                // consume a lot of CPU resources while waiting
                Thread.Sleep(100);  
                
            }
        }

        /// <summary>
        ///  Check if Arduino is available
        /// </summary>
        /// <param name="timeOut">Timout for waiting on response</param>
        /// <returns>Result. True if succesfull</returns>
        public bool ArduinoAvailable(int timeOut)
        {
            var challengeCommand = new SendCommand(_identifyCommandId, _identifyCommandId, timeOut);
            var responseCommand = CmdMessenger.SendCommand(challengeCommand, SendQueue.InFrontQueue, ReceiveQueue.Default, UseQueue.BypassQueue);

            bool isOk = responseCommand.Ok;
            if (isOk && !string.IsNullOrEmpty(_uniqueDeviceId))
            {
                isOk = ValidateDeviceUniqueId(responseCommand);
            }

            return isOk;
        }

        /// <summary>
        ///  Check if Arduino is available
        /// </summary>
        /// <param name="timeOut">Timout for waiting on response</param>
        /// <param name="tries">Number of tries</param>
        /// <returns>Result. True if succesfull</returns>
        public bool ArduinoAvailable(int timeOut, int tries)
        {
            for (var i = 1; i <= tries; i++)
            {
                Log(3, "Polling Arduino, try # " + i);
                if (ArduinoAvailable(timeOut)) return true;
            }
            return false;
        }

        protected virtual bool ValidateDeviceUniqueId(ReceivedCommand responseCommand)
        {
            bool valid = _uniqueDeviceId == responseCommand.ReadStringArg();
            if (!valid)
            {
                Log(3, "Invalid device response. Device ID mismatch.");
            }

            return valid;
        }

        //Try to connect using current connections settings
        protected abstract void DoWorkConnect();

        // Perform scan to find connected systems
        protected abstract void DoWorkScan();

        protected virtual void DoWorkWatchdog()
        {
            var lastLineTimeStamp = CmdMessenger.LastReceivedCommandTimeStamp;
            var currentTimeStamp = TimeUtils.Millis;

            // If timeout has not elapsed, wait till next watch time
            if (currentTimeStamp < _nextTimeOutCheck) return;

            // if a command has been received recently, set next check time
            if (lastLineTimeStamp > _lastCheckTime)
            {
                Log(3, "Successful watchdog response");
                _lastCheckTime = currentTimeStamp;
                _nextTimeOutCheck = _lastCheckTime + WatchdogTimeout;
                _watchdogTries = 0;
                return;
            }

            // Apparently, other side has not reacted in time
            // If too many tries, notify and stop
            if (_watchdogTries >= WatchdogTries)
            {
                Log(2, "Watchdog received no response after final try #" + WatchdogTries);
                _watchdogTries = 0;
                ConnectionManagerState = ConnectionManagerState.Wait;
                ConnectionTimeoutEvent();
                return;
            }

            // We'll try another time
            // We queue the command in order to not be intrusive, but put it in front to get a quick answer
            CmdMessenger.SendCommand(new SendCommand(_identifyCommandId), SendQueue.InFrontQueue, ReceiveQueue.Default);
            _watchdogTries++;

            _lastCheckTime = currentTimeStamp;
            _nextTimeOutCheck = _lastCheckTime + WatchdogRetryTimeout;
            Log(3, _watchdogTries == 1 ? 
                "Watchdog detected no communication for " + WatchdogTimeout/1000.0 + "s, asking for response" 
                : "Watchdog received no response, performing try #" + _watchdogTries);
        }

        /// <summary>
        /// Disconnect from Arduino
        /// </summary>
        /// <returns>true if sucessfully disconnected</returns>
        public bool Disconnect()
        {
            if (Connected)
            {
                Connected = false;
                return CmdMessenger.Disconnect();
            }

            return true;
        }

        /// <summary>
        /// Start watchdog. Will check if connection gets interrupted
        /// </summary>
        protected virtual void StartWatchDog()
        {
            if (ConnectionManagerState != ConnectionManagerState.Watchdog && Connected)
            {
                Log(1, "Starting Watchdog");
                _lastCheckTime = TimeUtils.Millis;
                _nextTimeOutCheck = _lastCheckTime + WatchdogTimeout;
                _watchdogTries = 0;

                ConnectionManagerState = ConnectionManagerState.Watchdog;
            }
        }

        /// <summary>
        /// Stop watchdog.
        /// </summary>
        protected virtual void StopWatchDog()
        {
            if (ConnectionManagerState == ConnectionManagerState.Watchdog)
            {
                Log(1, "Stopping Watchdog");
                ConnectionManagerState = ConnectionManagerState.Wait;
            }
        }

        /// <summary>
        /// Start scanning for devices
        /// </summary>
        protected virtual void StartScan()
        {
            if (ConnectionManagerState != ConnectionManagerState.Scan && !Connected)
            {
                Log(1, "Starting device scan");
                ConnectionManagerState = ConnectionManagerState.Scan;
            }
        }

        /// <summary>
        /// Stop scanning for devices
        /// </summary>
        protected virtual void StopScan()
        {
            if (ConnectionManagerState == ConnectionManagerState.Scan)
            {
                Log(1, "Stopping device scan");
                ConnectionManagerState = ConnectionManagerState.Wait;
            }
        }

        /// <summary>
        /// Start connect to device
        /// </summary>
        protected virtual void StartConnect()
        {
            if (ConnectionManagerState != ConnectionManagerState.Connect && !Connected)
            {
                Log(1, "Start connecting to device");
                ConnectionManagerState = ConnectionManagerState.Connect;
            }
        }

        /// <summary>
        /// Stop connect to device
        /// </summary>
        protected virtual void StopConnect()
        {
            if (ConnectionManagerState == ConnectionManagerState.Connect)
            {
                Log(1, "Stop connecting to device");
                ConnectionManagerState = ConnectionManagerState.Wait;
            }
        }

        protected virtual void StoreSettings() { }

        protected virtual void ReadSettings() { }

        // Dispose 
        public void Dispose()
        {
            Dispose(true);
        }

        // Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopConnectionManager();
            }
        }
    }
}


