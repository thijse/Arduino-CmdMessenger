using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandMessenger
{
    public class AsyncWorker
    {
        public enum State
        {
            Stopped,
            Running,
            Suspended
        }

        /// <summary>
        /// Main worker method to do some work.
        /// </summary>
        /// <returns>true is there is more work to do, otherwise false and worker will wait until signalled with SignalWorker().</returns>
        public delegate bool AsyncWorkerJob();

        private volatile State _state = State.Stopped;
        private volatile State _requestedState = State.Stopped;

        private readonly object _lock = new object();
        private readonly EventWaiter _eventWaiter = new EventWaiter();

        private readonly AsyncWorkerJob _workerJob;

        private Task _workerTask;

        public State WorkerState { get { return _state; } }

        public AsyncWorker(AsyncWorkerJob workerJob)
        {
            if (workerJob == null) throw new ArgumentNullException("workerJob");
            _workerJob = workerJob;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_state == State.Stopped)
                {
                    _requestedState = _state = State.Running;
                    _eventWaiter.Reset();

                    // http://blogs.msdn.com/b/pfxteam/archive/2010/06/13/10024153.aspx
                    // prefer using Task.Factory.StartNew for .net 4.0. For .net 4.5 Task.Run is the better option.
                    _workerTask = Task.Factory.StartNew(x =>
                    {
                        while (true)
                        {
                            if (_state == State.Stopped) break;

                            bool haveMoreWork = false;
                            if (_state == State.Running)
                            {
                                haveMoreWork = _workerJob();
                            }
                            if (!haveMoreWork || _state == State.Suspended) _eventWaiter.WaitOne(Timeout.Infinite);
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
                if (_state == State.Running || _state == State.Suspended)
                {
                    _requestedState = State.Stopped;
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
                if (_state == State.Running)
                {
                    _requestedState = State.Suspended;
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
                if (_state == State.Suspended)
                {
                    _requestedState = State.Running;
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
            if (_state == State.Running) _eventWaiter.Set();
        }
    }
}
