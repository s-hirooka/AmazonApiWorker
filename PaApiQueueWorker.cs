using AmazonPaApiLinkSample;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PaApiWorker
{
    public sealed class PaApiQueueWorker
    {
        private readonly PaApiQueueRepository _repo;
        private readonly AmazonPaApiAffiliateLinkClient _client;
        private readonly RateGate _rateGate;
        private readonly AppLogger _logger;
        private readonly int _maxRetryCount;

        public PaApiQueueWorker(
            PaApiQueueRepository repo,
            AmazonPaApiAffiliateLinkClient client,
            RateGate rateGate,
            AppLogger logger,
            int maxRetryCount = 3)
        {
            _repo = repo;
            _client = client;
            _rateGate = rateGate;
            _logger = logger;
            _maxRetryCount = maxRetryCount;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Worker loop started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var item = _repo.TryTakeNext();

                if (item == null)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                _logger.Info($"Dequeued ASIN={item.Asin}, Id={item.Id}, Retry={item.RetryCount}");

                try
                {
                    await _rateGate.WaitAsync(cancellationToken);

                    var result = await _client.GetAffiliateLinkByAsinAsync(item.Asin, cancellationToken);

                    if (result.Success)
                    {
                        _repo.MarkSuccess(item.Id, result.Title, result.AffiliateUrl, result.RawResponse);
                        _logger.Info($"Success ASIN={item.Asin}, Id={item.Id}, Title={result.Title}, Url={result.AffiliateUrl}");
                    }
                    else
                    {
                        int nextRetry = item.RetryCount + 1;
                        if (nextRetry <= _maxRetryCount)
                        {
                            _repo.MarkRetry(item.Id, nextRetry, result.ErrorMessage);
                            _logger.Warn($"Retry ASIN={item.Asin}, Id={item.Id}, Retry={nextRetry}, Error={result.ErrorMessage}");
                        }
                        else
                        {
                            _repo.MarkFailed(item.Id, result.ErrorMessage);
                            _logger.Error($"Failed ASIN={item.Asin}, Id={item.Id}, Error={result.ErrorMessage}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    int nextRetry = item.RetryCount + 1;
                    if (nextRetry <= _maxRetryCount)
                    {
                        _repo.MarkRetry(item.Id, nextRetry, ex.ToString());
                        _logger.Warn($"Exception retry ASIN={item.Asin}, Id={item.Id}, Retry={nextRetry}, Error={ex.Message}");
                    }
                    else
                    {
                        _repo.MarkFailed(item.Id, ex.ToString());
                        _logger.Error(ex, $"Exception failed ASIN={item.Asin}, Id={item.Id}");
                    }
                }
            }

            _logger.Info("Worker loop stopped.");
        }
    }
}