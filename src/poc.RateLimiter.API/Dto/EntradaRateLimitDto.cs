namespace poc.RateLimiter.API.Dto
{
    public class EntradaRateLimitDto
    {
        public Guid UsuarioId { get; set; }
        public string Endpoint { get; set; }
        public TimeSpan Window { get; set; }
        public int PermitLimit { get; set; }
    }
}
