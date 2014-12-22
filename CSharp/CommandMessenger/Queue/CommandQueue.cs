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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CommandMessenger.Queue
{
    // Command queue base object. 
    public class CommandQueue : IDisposable
    {
        private enum WorkerState
        {
            Stopped,
            Running,
            Suspended
        }

        private volatile WorkerState _state = WorkerState.Stopped;
        private volatile WorkerState _requestedState = WorkerState.Stopped;

        private Task _queueTask;
        private readonly object _lock = new object();
        private readonly EventWaiter _eventWaiter = new EventWaiter();

        protected readonly ListQueue<CommandStrategy> Queue = new ListQueue<CommandStrategy>();   // Buffer for commands
        protected readonly List<GeneralStrategy> GeneralStrategies = new List<GeneralStrategy>(); // Buffer for command independent strategies

        /// <summary>Gets count of records in queue. NOT THREAD-SAFE.</summary>
        public int Count
        {
            get { return Queue.Count; }
        }

        /// <summary>Gets is queue is empty. NOT THREAD-SAFE.</summary>
        public bool IsEmpty
        {
            get { return Queue.Count == 0; }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_state == WorkerState.Stopped)
                {
                    _requestedState = _state = WorkerState.Running;
                    _eventWaiter.Reset();

                    // http://blogs.msdn.com/b/pfxteam/archive/2010/06/13/10024153.aspx
                    // prefer using Task.Factory.StartNew for .net 4.0. For .net 4.5 Task.Run is the better option.
                    _queueTask = Task.Factory.StartNew(x =>
                    {
                        while (true)
                        {
                            if (_state == WorkerState.Stopped) break;

                            bool empty = false;
                            if (_state == WorkerState.Running)
                            {
                                ProcessQueue();
                                lock (Queue) empty = IsEmpty;
                            }
                            if (empty || _state == WorkerState.Suspended) _eventWaiter.WaitOne(Timeout.Infinite);
                            _state = _requestedState;
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning);

                    SpinWait.SpinUntil(() => _queueTask.Status == TaskStatus.Running);
                }
                else
                {
                    throw new InvalidOperationException("The Command Queue is already started.");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_state == WorkerState.Running || _state == WorkerState.Suspended)
                {
                    _requestedState = WorkerState.Stopped;
                    _eventWaiter.Set();
                    _queueTask.Wait();
                    _queueTask.Dispose();
                }
                else
                {
                    throw new InvalidOperationException("The Command Queue is already stopped.");
                }
            }
        }

        public void Suspend()
        {
            lock (_lock)
            {
                if (_state == WorkerState.Running)
                {
                    _requestedState = WorkerState.Suspended;
                    _eventWaiter.Set();
                    SpinWait.SpinUntil(() => _requestedState == _state);
                }
                else
                {
                    throw new InvalidOperationException("The Command Queue is not running.");
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (_state == WorkerState.Suspended)
                {
                    _requestedState = WorkerState.Running;
                    _eventWaiter.Set();
                    SpinWait.SpinUntil(() => _requestedState == _state);
                }
                else
                {
                    throw new InvalidOperationException("The Command Queue is not in suspended state.");
                }
            }
        }

        /// <summary> Clears the queue. </summary>
        public void Clear()
        {
            lock (Queue) Queue.Clear();
        }

        /// <summary> 
        /// Queue the command wrapped in a command strategy. 
        /// Call SignalWaiter method to continue processing of queue.
        /// </summary>
        /// <param name="commandStrategy"> The command strategy. </param>
        public virtual void QueueCommand(CommandStrategy commandStrategy)
        {
        }

        /// <summary> Adds a general strategy. This strategy is applied to all queued and dequeued commands.  </summary>
        /// <param name="generalStrategy"> The general strategy. </param>
        public void AddGeneralStrategy(GeneralStrategy generalStrategy)
        {
            // Give strategy access to queue
            generalStrategy.CommandQueue = Queue;
            // Add to general strategy list
            GeneralStrategies.Add(generalStrategy);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
            }
        }

        /// <summary>
        /// Continue processing of queue.
        /// </summary>
        protected void SignalWaiter()
        {
            if (_state == WorkerState.Running) _eventWaiter.Set();
        }

        /// <summary> Process the queue. </summary>
        protected virtual void ProcessQueue()
        {
        }
    }
}
