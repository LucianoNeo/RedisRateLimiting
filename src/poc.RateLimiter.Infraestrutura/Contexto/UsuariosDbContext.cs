using Microsoft.EntityFrameworkCore;
using poc.RateLimiter.API.Entidades;

namespace poc.RateLimiter.Infraestrutura.Contexto
{
    public class UsuariosDbContext : DbContext
    {
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<RateLimit> RateLimits { get; set; }

        public UsuariosDbContext(DbContextOptions<UsuariosDbContext> options) : base(options)
        {
        }
    }
}