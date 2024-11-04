using System;
using System.Threading.RateLimiting;

namespace poc.ratelimiter.Services.RedisRateLimiting
{
    /// <summary>
    /// Contains methods for assisting in the creation of partitions for your rate limiter.
    /// </summary>
    public static class RedisRateLimitPartition
    {
        /// <summary>
        /// Defines a partition with a <see cref="RedisFixedWindowRateLimiter{TKey}"/> with the given <see cref="RedisFixedWindowRateLimiterOptions"/>.
        /// </summary>
        /// <typeparam name="TKey">The type to distinguish partitions with.</typeparam>
        /// <param name="partitionKey">The specific key for this partition. This will be used to check for an existing cached limiter before calling the <paramref name="factory"/>.</param>
        /// <param name="factory">The function called when a rate limiter for the given <paramref name="partitionKey"/> is needed. This can return the same instance of <see cref="RedisFixedWindowRateLimiterOptions"/> across different calls.</param>
        /// <returns></returns>
        public static RateLimitPartition<TKey> GetFixedWindowRateLimiter<TKey>(
            TKey partitionKey,
            Func<TKey, RedisFixedWindowRateLimiterOptions> factory)
        {
            return RateLimitPartition.Get(partitionKey, key => new RedisFixedWindowRateLimiter<TKey>(key, factory(key)));
        }
    }
}
