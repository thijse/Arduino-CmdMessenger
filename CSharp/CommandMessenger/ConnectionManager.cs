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
        
        protected Control ControlToInvokeOn;
        protected ConnectionManagerState ConnectionManagerState;

        public event EventHandler ConnectionTimeout;
        public event EventHandler ConnectionFound;
        public event EventHandler<ConnectionManagerProgressEventArgs> Progress;

        private readonly BackgroundWorker _workerThread;
        private readonly int _challengeCommandId;
        private readonly int _responseCommandId;

        private long _lastCheckTime;
        private long _nextTimeOutCheck;
        private uint _watchdogTries;

        /// <summary>
        /// Is connection manager currently connected to device.
        /// </summary>
        public bool Connected { get; protected set; }

        public int WatchdogTimeout { get; set; }
        public int WatchdogRetryTimeout { get; set; }
        public uint WatchdogTries { get; set; }

        /// <summary>
        /// Enables or disables connection watchdog functionality.
        /// </summary>
        public bool WatchdogEnabled { get; set; }

        /// <summary>
        /// Use this property to tell connection manager to connect only to specific port provided by transport.
        /// </summary>
        public bool UseFixedPort { get; set; }

        /// <summary>
        /// Enables or disables storing of last connection configuration in persistent file.
        /// </summary>
        public bool PersistentSettings { get; set; }

        protected ConnectionManager(CmdMessenger cmdMessenger, int challengeCommandId, int responseCommandId)
        {
            if (cmdMessenger == null)
                throw new ArgumentNullException("cmdMessenger", "Command Messenger is null.");
            
            WatchdogTimeout = 2000;
            WatchdogRetryTimeout = 1000;        
            WatchdogTries = 3;
            WatchdogEnabled = true;

            PersistentSettings = true;
            UseFixedPort = false;
            
            ControlToInvokeOn = null;
            CmdMessenger = cmdMessenger;

            ConnectionManagerState = ConnectionManagerState.Stop;

            _workerThread = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = false };

            _challengeCommandId = challengeCommandId;
            _responseCommandId = responseCommandId;

            CmdMessenger.Attach(responseCommandId, OnResponseCommandId);
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

        /// <summary> Sets a control to invoke on. </summary>
        /// <param name="controlToInvokeOn"> The control to invoke on. </param>
        public void SetControlToInvokeOn(Control controlToInvokeOn)
        {
            ControlToInvokeOn = controlToInvokeOn;
        }

        protected virtual void ConnectionFoundEvent()
        {
            ConnectionManagerState = ConnectionManagerState.Wait;

            if (WatchdogEnabled) StartWatchDog();

            InvokeEvent(ConnectionFound);
        }

        protected virtual void ConnectionTimeoutEvent()
        {
            Connected = false;

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
                if (eventHandler == null) return;
                if (ControlToInvokeOn != null && ControlToInvokeOn.IsDisposed) return;
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
                if (eventHandler == null) return;
                if (ControlToInvokeOn != null && ControlToInvokeOn.IsDisposed) return;
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

        protected virtual void OnResponseCommandId(ReceivedCommand arguments)
        {
            // Do nothing. 
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

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        ///  Check if Arduino is available
        /// </summary>
        /// <param name="timeOut">timout for waiting on response</param>
        /// <returns>Result. True if succesfull</returns>
        public bool ArduinoAvailable(int timeOut)
        {
            var challengeCommand = new SendCommand(_challengeCommandId, _responseCommandId, timeOut);
            var responseCommand = CmdMessenger.SendCommand(challengeCommand,SendQueue.InFrontQueue,ReceiveQueue.Default,UseQueue.BypassQueue);
            return responseCommand.Ok;
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

        protected abstract void DoWorkScan();

        protected abstract void DoWorkConnect();

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
                Log(3, "No watchdog response after final try");
                _watchdogTries = 0;
                ConnectionManagerState = ConnectionManagerState.Wait;
                ConnectionTimeoutEvent();
                return;
            }

            // We'll try another time
            // We queue the command in order to not be intrusive, but put it in front to get a quick answer
            CmdMessenger.SendCommand(new SendCommand(_challengeCommandId), SendQueue.InFrontQueue, ReceiveQueue.Default);
            _watchdogTries++;

            _lastCheckTime = currentTimeStamp;
            _nextTimeOutCheck = _lastCheckTime + WatchdogRetryTimeout;
            Log(3, "No watchdog response, performing try #" + _watchdogTries);
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


