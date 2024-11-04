using StackExchange.Redis;
using System;

namespace poc.ratelimiter.Services.RedisRateLimiting
{
    public abstract class RedisRateLimiterOptions
    {
        // <summary>
        /// Factory for a Redis ConnectionMultiplexer.
        /// </summary>
        public Func<IConnectionMultiplexer>? ConnectionMultiplexerFactory { get; set; }
    }
}
