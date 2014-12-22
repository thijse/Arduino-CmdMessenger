using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandMessenger
{
    public class Worker
    {
        public enum WorkerState
        {
            Stopped,
            Running,
            Suspended
        }

        /// <summary>
        /// Main worker method to do some work.
        /// </summary>
        /// <returns>true is there is more work to do, otherwise false and worker will wait until signalled with SignalWorker().</returns>
        public delegate bool WorkerJob();

        private volatile WorkerState _state = WorkerState.Stopped;
        private volatile WorkerState _requestedState = WorkerState.Stopped;

        private readonly object _lock = new object();
        private readonly EventWaiter _eventWaiter = new EventWaiter();

        private readonly WorkerJob _workerJob = () => false;

        private Task _workerTask;

        public Worker(WorkerJob workerJob)
        {
            _workerJob = workerJob;
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
                    _workerTask = Task.Factory.StartNew(x =>
                    {
                        while (true)
                        {
                            if (_state == WorkerState.Stopped) break;

                            bool haveMoreWork = false;
                            if (_state == WorkerState.Running)
                            {
                                haveMoreWork = _workerJob();
                            }
                            if (!haveMoreWork || _state == WorkerState.Suspended) _eventWaiter.WaitOne(Timeout.Infinite);
                            _state = _requestedState;
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning);

                    SpinWait.SpinUntil(() => _workerTask.Status == TaskStatus.Running);
                }
                else
                {
                    throw new InvalidOperationException("The worker is already started.");
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
                    _workerTask.Wait();
                    _workerTask.Dispose();
                }
                else
                {
                    throw new InvalidOperationException("The worker is already stopped.");
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
                    throw new InvalidOperationException("The worker is not running.");
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
                    throw new InvalidOperationException("The worker is not in suspended state.");
                }
            }
        }

        /// <summary>
        /// Signal worker to continue processing.
        /// </summary>
        public void Signal()
        {
            if (_state == WorkerState.Running) _eventWaiter.Set();
        }
    }
}
