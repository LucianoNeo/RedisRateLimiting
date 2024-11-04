#region USINGS

using poc.RateLimiter.API.Dto;
using poc.RateLimiter.API.Entidades;
using poc.RateLimiter.Infraestrutura.Contexto;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using poc.ratelimiter.Services.RedisRateLimiting;

#endregion USINGS

#region BUILDER

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UsuariosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UsuariosDb")));

var redisConfig = builder.Configuration.GetSection("Redis")["ConnectionString"];
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig));

var app = builder.Build();
app.UseHttpsRedirection();

#endregion BUILDER

#region REPOSITORIOS

// Consulta ao banco SQL para buscar usuários e limites e armazenar no Redis
var dadosLimitBanco = await GetUsuariosComLimitesSql(app.Services);
await SalvarLimitesNoRedis(dadosLimitBanco, app.Services);

#endregion REPOSITORIOS

#region MIDDLEWARE

app.Use(async (context, next) =>
{
    var userId = "Luciano";
    var endpoint = context.Request.Path.ToString().ToLower();

    var userConfig = await ObterLimitesDoRedis(userId, endpoint, redisConfig);

    if (userConfig == null)
    {
        await next();
        return;
    }

    var options = new RedisFixedWindowRateLimiterOptions
    {
        PermitLimit = userConfig.Limit.PermitLimit,
        Window = userConfig.Limit.Window,
        ConnectionMultiplexerFactory = () => ConnectionMultiplexer.Connect(redisConfig)
    };

    var limiter = new RedisFixedWindowRateLimiter<string>($"{userId}:{endpoint}", options);
    var permit = await limiter.AcquireAsync();

    if (permit.IsAcquired)
    {
        context.Response.Headers["RateLimit-Remaining"] = (userConfig.Limit.PermitLimit - userConfig.Remaining).ToString();
        await next();
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        context.Response.Headers.RetryAfter = userConfig.Reset.ToString();
        context.Response.Headers["RateLimit-Limit"] = userConfig.Limit.PermitLimit.ToString();
        context.Response.Headers["RateLimit-Remaining"] = (userConfig.Limit.PermitLimit - userConfig.Remaining).ToString();
        context.Response.Headers["RateLimit-Reset"] = userConfig.Reset.ToString();

        await context.Response.WriteAsync("Limite de acesso atingido, tente novamente mais tarde.");
    }
});

#endregion MIDDLEWARE

#region ENDPOINTS

// Endpoint para limpar limites
app.MapPost("v1/rateLimit/clear", async (DeletarRateLimitDto request, IConnectionMultiplexer redis, IServiceProvider services) =>
{
    var resultado = await ClearRateLimitKeysAsync(app.Services, redis, request);
    if (resultado != null)
    {
        await LimparLimitesRedis(request.UsuarioId, request.Endpoint, services);
        await AtualizarLimites();
    }
    return resultado ?? Results.NotFound(new { Message = "Limite não encontrado." });
});

app.MapGet("v1/weatherforecast", () => Results.Ok("Olá Henrique"));
app.MapGet("v1/teste2", () => Results.Ok("Olá Henrique"));

app.MapPost("v1/rateLimit", async (EntradaRateLimitDto entrada, IServiceProvider services) =>
{
    var resultado = await AdicionarLimite(services, entrada);
    return Results.Ok(new { Message = "Limite adicionado com sucesso.", Limite = resultado });
});

app.MapPut("v1/rateLimit", async (EntradaRateLimitDto entrada, IServiceProvider services) =>
{
    var resultado = await AlterarLimite(services, entrada);
    if (resultado != null)
    {
        await LimparLimitesRedis(entrada.UsuarioId, entrada.Endpoint, services);
        await AtualizarLimites();
    }
    return resultado ?? Results.NotFound(new { Message = "Limite não encontrado." });
});

app.MapDelete("v1/rateLimit", async (IServiceProvider services, [FromBody] DeletarRateLimitDto request) =>
{
    var resultado = await DeletarLimite(services, request);
    if (resultado != null)
    {
        await LimparLimitesRedis(request.UsuarioId, request.Endpoint, services);
        await AtualizarLimites();
    }
    return resultado ?? Results.NotFound(new { Message = "Limite não encontrado." });
});

#endregion ENDPOINTS

#region SERVIÇOS

async Task<List<Usuario>> GetUsuariosComLimitesSql(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();
    return await context.Usuarios.Include(u => u.RateLimits).ToListAsync();
}

async Task SalvarLimitesNoRedis(List<Usuario> usuarios, IServiceProvider services)
{
    var redis = services.GetRequiredService<IConnectionMultiplexer>();
    var db = redis.GetDatabase();

    foreach (var usuario in usuarios)
    {
        foreach (var limit in usuario.RateLimits)
        {
            string key = $"RateLimit:{usuario.Nome}:{limit.Endpoint}";
            var value = new { limit.Window, limit.PermitLimit };
            await db.StringSetAsync(key, JsonSerializer.Serialize(value));
        }
    }
}

async Task LimparLimitesRedis(Guid usuarioId, string endpoint, IServiceProvider services)
{
    var redis = services.GetRequiredService<IConnectionMultiplexer>();
    var db = redis.GetDatabase();
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();

    var user = await context.Usuarios.FindAsync(usuarioId) ?? throw new Exception("Usuário não encontrado!");

    var userName = user.Nome;

    // Prefixo para as chaves do Rate Limit
    var prefixRateLimit = $"RateLimit:{userName}:{endpoint}";

    // Prefixo para as chaves que você deseja remover
    var prefixFw = $"rl:fw:{{{userName}:{endpoint}}}";

    int cursor = 0;
    var keysDeleted = new List<string>();

    // Limpar chaves do prefixo RateLimit
    do
    {
        var result = db.Execute("SCAN", cursor.ToString(), "MATCH", $"{prefixRateLimit}*");
        cursor = Convert.ToInt32(result[0]);
        var keys = (RedisKey[])result[1];

        foreach (var key in keys)
        {
            keysDeleted.Add(key);
            db.KeyDelete(key);
            Console.WriteLine($"Chave {key} deletada com sucesso.");
        }
    } while (cursor != 0);

    cursor = 0;

    // Limpar chaves do prefixo rl:fw
    do
    {
        var result = db.Execute("SCAN", cursor.ToString(), "MATCH", $"{prefixFw}*");
        cursor = Convert.ToInt32(result[0]);
        var keys = (RedisKey[])result[1];

        foreach (var key in keys)
        {
            keysDeleted.Add(key);
            db.KeyDelete(key);
            Console.WriteLine($"Chave {key} deletada com sucesso.");
        }
    } while (cursor != 0);

    if (keysDeleted.Count > 0)
    {
        Console.WriteLine($"Limites removidos para o usuário {usuarioId} e endpoint {endpoint}.");
    }
    else
    {
        Console.WriteLine($"Nenhuma chave encontrada para o usuário {usuarioId} e endpoint {endpoint}.");
    }
}

async Task<RespostaRateLimit?> ObterLimitesDoRedis(string userId, string endpoint, string redisConfig)
{
    var redis = ConnectionMultiplexer.Connect(redisConfig);
    var db = redis.GetDatabase();

    // Chave para o rate limit
    string key = $"RateLimit:{userId}:{endpoint}";
    var value = await db.StringGetAsync(key);

    // Chave para os valores de Remaining
    string remainingKey = $"rl:fw:{{{userId}:{endpoint}}}";
    var remainingValue = await db.StringGetAsync(remainingKey);

    // Chave para os valores de reset e retry
    string expKey = $"rl:fw:{{{userId}:{endpoint}}}:exp";
    var expValue = await db.StringGetAsync(expKey);
    redis.Close();

    // Checa se há informações de limite
    if (value.HasValue)
    {
        var rateLimit = JsonSerializer.Deserialize<RateLimit>(value.ToString());

        var reset = expValue.HasValue
            ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds((long)expValue).LocalDateTime
            : null;

        var remaining = remainingValue.HasValue
            ? (int?)remainingValue
            : null;

        return new RespostaRateLimit
        {
            Limit = rateLimit,
            Reset = reset,
            Remaining = remaining
        };
    }

    return null;
}

async Task<RateLimit?> AdicionarLimite(IServiceProvider services, EntradaRateLimitDto entrada)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();

    var usuarioExiste = await context.Usuarios.AnyAsync(u => u.Id == entrada.UsuarioId);
    if (!usuarioExiste)
    {
        Console.WriteLine("O UsuarioId fornecido não existe.");
        return null;
    }

    var novoLimite = new RateLimit
    {
        Id = Guid.NewGuid(),
        Endpoint = entrada.Endpoint,
        UsuarioId = entrada.UsuarioId,
        PermitLimit = entrada.PermitLimit,
        Window = entrada.Window,
    };

    await context.RateLimits.AddAsync(novoLimite);
    await context.SaveChangesAsync();
    await AtualizarLimites();
    return novoLimite;
}

async Task<IResult?> AlterarLimite(IServiceProvider services, EntradaRateLimitDto entrada)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();

    var limite = await context.RateLimits.FirstOrDefaultAsync(x => x.UsuarioId == entrada.UsuarioId && x.Endpoint == entrada.Endpoint);
    if (limite == null)
        return null;

    limite.Endpoint = entrada.Endpoint;
    limite.UsuarioId = entrada.UsuarioId;
    limite.PermitLimit = entrada.PermitLimit;
    limite.Window = entrada.Window;

    await context.SaveChangesAsync();
    return Results.NoContent();
}

async Task<IResult?> DeletarLimite(IServiceProvider services, DeletarRateLimitDto entrada)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();

    var limite = await context.RateLimits.FirstOrDefaultAsync(x => x.UsuarioId == entrada.UsuarioId && x.Endpoint == entrada.Endpoint);
    if (limite == null)
        return null;

    context.RateLimits.Remove(limite);
    await context.SaveChangesAsync();
    return Results.NoContent();
}

async Task AtualizarLimites()
{
    try
    {
        var dadosLimitBanco = await GetUsuariosComLimitesSql(app.Services);
        await SalvarLimitesNoRedis(dadosLimitBanco, app.Services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao atualizar limites: {ex.Message}");
    }
}

async Task<IResult?> ClearRateLimitKeysAsync(IServiceProvider services, IConnectionMultiplexer redis, DeletarRateLimitDto request)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UsuariosDbContext>();

    var user = await context.Usuarios.FirstOrDefaultAsync(x => x.Id == request.UsuarioId);

    if (user == null)
    {
        return null;
    }

    var userName = user.Nome;

    var db = redis.GetDatabase();
    var prefix = $"rl:fw:{{{userName}:{request.Endpoint}}}";
    int cursor = 0;
    var keysDeleted = new List<string>();

    do
    {
        var result = db.Execute("SCAN", cursor.ToString(), "MATCH", $"{prefix}*");
        cursor = Convert.ToInt32(result[0]);
        var keys = (RedisKey[])result[1];

        foreach (var key in keys)
        {
            keysDeleted.Add(key);
            await db.KeyDeleteAsync(key);
            Console.WriteLine($"Chave {key} deletada com sucesso.");
        }
    } while (cursor != 0);

    if (keysDeleted.Count == 0)
    {
        return Results.NotFound(new
        {
            Message = $"Nenhuma chave encontrada para o usuário {request.UsuarioId} e endpoint {request.Endpoint}."
        });
    }

    return Results.Ok(new
    {
        Message = $"Limites removidos para o usuário {request.UsuarioId} e endpoint {request.Endpoint}.",
        DeletedKeys = keysDeleted
    });
}

#endregion SERVIÇOS

app.Run();