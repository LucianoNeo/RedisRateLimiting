namespace poc.RateLimiter.API.Entidades
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public string Nome { get; set; }
        public virtual List<RateLimit> RateLimits { get; set; }
    }
}