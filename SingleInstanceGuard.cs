using System;
using System.Threading;

namespace PaApiWorker
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex _mutex;
        public bool IsFirstInstance { get; }

        public SingleInstanceGuard(string mutexName)
        {
            if (string.IsNullOrWhiteSpace(mutexName))
                throw new ArgumentException("mutexName is required.", nameof(mutexName));

            _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out bool createdNew);
            IsFirstInstance = createdNew;
        }

        public void Dispose()
        {
            try
            {
                if (IsFirstInstance)
                {
                    _mutex.ReleaseMutex();
                }
            }
            catch
            {
            }

            _mutex.Dispose();
        }
    }
}