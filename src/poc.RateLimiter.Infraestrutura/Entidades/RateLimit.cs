namespace poc.RateLimiter.API.Entidades
{
    public class RateLimit
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string Endpoint { get; set; }
        public TimeSpan Window { get; set; }
        public int PermitLimit { get; set; }
        public virtual Usuario Usuario { get; set; }
    }
}
