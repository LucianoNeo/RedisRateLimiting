namespace poc.RateLimiter.API.Dto
{
    public class DeletarRateLimitDto
    {
        public Guid UsuarioId { get; set; }
        public string Endpoint { get; set; }
    }
}
