using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandMessenger
{
    public class AsyncWorker : IDisposable
    {
        public enum WorkerState { Stopped, Running, Suspended }

        public delegate Task<bool> AsyncWorkerJob();

        private volatile WorkerState _state = WorkerState.Stopped;
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0, 1);
        private readonly AsyncWorkerJob _workerJob;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _task = Task.CompletedTask;

        public string Name { get; }
        public WorkerState State => _state;
        public bool IsRunning => _state == WorkerState.Running;
        public bool IsSuspended => _state == WorkerState.Suspended;

        public AsyncWorker(AsyncWorkerJob workerJob, string workerName = null)
        {
            _workerJob = workerJob ?? throw new ArgumentNullException(nameof(workerJob));
            Name = workerName;
        }

        public void Start()
        {
            if (_state != WorkerState.Stopped) throw new InvalidOperationException("Worker is already started.");
            _cts = new CancellationTokenSource();
            _state = WorkerState.Running;
            _task = Task.Run(RunAsync);
        }

        public void Stop()
        {
            if (_state == WorkerState.Stopped) return;
            _state = WorkerState.Stopped;
            _cts.Cancel();
            Signal(); // wake the loop so it can exit
            try { _task.Wait(2000); } catch { }
        }

        public void Suspend()
        {
            if (_state == WorkerState.Running) _state = WorkerState.Suspended;
        }

        public void Resume()
        {
            if (_state == WorkerState.Suspended)
            {
                _state = WorkerState.Running;
                Signal();
            }
        }

        public void Signal()
        {
            if (_signal.CurrentCount == 0)
                try { _signal.Release(); } catch (SemaphoreFullException) { }
        }

        private async Task RunAsync()
        {
            while (!_cts.IsCancellationRequested && _state != WorkerState.Stopped)
            {
                if (_state == WorkerState.Suspended)
                {
                    try { await _signal.WaitAsync(_cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                bool haveMoreWork = false;
                try { haveMoreWork = await _workerJob().ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch { /* job exceptions are swallowed to keep worker alive */ }

                if (!haveMoreWork)
                {
                    try { await _signal.WaitAsync(_cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
            _signal.Dispose();
        }
    }
}
