using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandMessenger
{
    public class EventWaiter : IDisposable
    {
        public enum WaitState { TimeOut, Normal }

        private readonly SemaphoreSlim _semaphore;

        public EventWaiter(bool set = false)
        {
            _semaphore = new SemaphoreSlim(set ? 1 : 0, 1);
        }

        public WaitState WaitOne(int timeoutMs)
        {
            bool acquired = _semaphore.Wait(timeoutMs < 0 ? Timeout.Infinite : timeoutMs);
            return acquired ? WaitState.Normal : WaitState.TimeOut;
        }

        public async Task<WaitState> WaitOneAsync(int timeoutMs, CancellationToken ct = default)
        {
            bool acquired = await _semaphore.WaitAsync(timeoutMs < 0 ? Timeout.Infinite : timeoutMs, ct).ConfigureAwait(false);
            return acquired ? WaitState.Normal : WaitState.TimeOut;
        }

        public void Set()
        {
            if (_semaphore.CurrentCount == 0)
                try { _semaphore.Release(); } catch (SemaphoreFullException) { }
        }

        public void Reset() { /* SemaphoreSlim(0,1) auto-resets on acquire */ }

        public void Dispose() => _semaphore.Dispose();
    }
}
