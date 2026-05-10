using AmazonPaApiLinkSample;
using PaApiWorker;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmazonApiQueueSystem
{
    public sealed class AmazonApiQueueWorker
    {
        private readonly AmazonApiQueueRepository _repository;
        private readonly AmazonPaApiAffiliateLinkClient _apiClient;
        private readonly RateGate _rateGate;
        private readonly AppLogger _logger;
        private readonly SourceDbUpdater _sourceDbUpdater;
        private readonly int _maxRetryCount;

        public AmazonApiQueueWorker(
            AmazonApiQueueRepository repository,
            AmazonPaApiAffiliateLinkClient apiClient,
            RateGate rateGate,
            AppLogger logger,
            SourceDbUpdater sourceDbUpdater,
            int maxRetryCount = 3)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _rateGate = rateGate ?? throw new ArgumentNullException(nameof(rateGate));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sourceDbUpdater = sourceDbUpdater ?? throw new ArgumentNullException(nameof(sourceDbUpdater));
            _maxRetryCount = maxRetryCount;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Worker loop started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var item = _repository.TryTakeNextPending();

                if (item == null)
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _logger.Info($"Dequeued ASIN={item.Asin}, Id={item.Id}, Retry={item.RetryCount}, SourceRowId={item.SourceRowId}");

                try
                {
                    await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var result = await _apiClient
                        .GetAffiliateLinkByAsinAsync(item.Asin, cancellationToken)
                        .ConfigureAwait(false);

                    if (result.Success)
                    {
                        _repository.MarkSuccess(
                            item.Id,
                            result.Title,
                            result.AffiliateUrl,
                            result.RawResponse);

                        if (!string.IsNullOrWhiteSpace(item.SourceRowId))
                        {
                            _sourceDbUpdater.UpdateAffiliateUrl(
                                item.SourceRowId,
                                result.AffiliateUrl,
                                result.Title);
                        }

                        _logger.Info($"Success ASIN={item.Asin}, Id={item.Id}, Url={result.AffiliateUrl}");
                    }
                    else
                    {
                        int nextRetry = item.RetryCount + 1;
                        if (nextRetry <= _maxRetryCount)
                        {
                            _repository.MarkRetry(item.Id, nextRetry, result.ErrorMessage);
                            _logger.Warn($"Retry ASIN={item.Asin}, Id={item.Id}, Retry={nextRetry}, Error={result.ErrorMessage}");
                        }
                        else
                        {
                            _repository.MarkFailed(item.Id, result.ErrorMessage);
                            _logger.Error($"Failed ASIN={item.Asin}, Id={item.Id}, Error={result.ErrorMessage}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    int nextRetry = item.RetryCount + 1;
                    if (nextRetry <= _maxRetryCount)
                    {
                        _repository.MarkRetry(item.Id, nextRetry, ex.ToString());
                        _logger.Warn($"Exception retry ASIN={item.Asin}, Id={item.Id}, Retry={nextRetry}, Error={ex.Message}");
                    }
                    else
                    {
                        _repository.MarkFailed(item.Id, ex.ToString());
                        _logger.Error(ex, $"Exception failed ASIN={item.Asin}, Id={item.Id}");
                    }
                }
            }

            _logger.Info("Worker loop stopped.");
        }
    }
}