using poc.RateLimiter.API.Entidades;

namespace poc.RateLimiter.API.Dto
{
    public class RespostaRateLimit
    {
        public RateLimit? Limit { get; set; }
        public DateTime? Reset { get; set; }
        public int? Remaining { get; set; }
    }
}
