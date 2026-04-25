using Polly;
using Polly.Retry;
using Tutor.Api.Models.Exceptions;

namespace Tutor.Api.Utilities
{
    public class ResiliencePipelineUtility
    {
        public static ResiliencePipeline CreateAssertRequestPipeline()
        {
            return new ResiliencePipelineBuilder()
                    .AddRetry(
                        new RetryStrategyOptions
                        {
                            MaxRetryAttempts = 3,
                            Delay = TimeSpan.FromMilliseconds(200),
                            BackoffType = DelayBackoffType.Exponential,
                            ShouldHandle = new PredicateBuilder()
                                .Handle<Exception>(x => x.Message == Errors.COULD_NOT_ACQUIRE_LOCK_FOR_USER_UPDATE)
                                .Handle<Exception>(x => x.Message == Errors.ALL_REQUESTS_USED)
                                .Handle<Exception>(x => x.Message == Errors.TIME_WINDOW_FINISHED)
                        })
                    .Build();
        }
    }
}
