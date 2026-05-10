using System;
using System.Threading;
using System.Threading.Tasks;

namespace PaApiWorker
{
    public sealed class RateGate
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly int _minIntervalMs;
        private DateTime _lastUtc = DateTime.MinValue;

        public RateGate(int minIntervalMs)
        {
            _minIntervalMs = minIntervalMs;
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var next = _lastUtc.AddMilliseconds(_minIntervalMs);

                if (next > now)
                {
                    await Task.Delay(next - now, cancellationToken);
                }

                _lastUtc = DateTime.UtcNow;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}