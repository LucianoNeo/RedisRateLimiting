using poc.ratelimiter.Services.RedisRateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

builder.Services.AddDbContext<UsuariosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UsuariosDb")));

app.UseHttpsRedirection();

var redisConfig = builder.Configuration.GetSection("Redis")["ConnectionString"];

app.Use(async (context, next) =>
{
    var userId = "user1";
    var endpoint = context.Request.Path.ToString().ToLower();
    var key = $"{userId}:{endpoint}";

    var rateLimitConfig = builder.Configuration.GetSection("RateLimit:Limits");

    var userConfig = rateLimitConfig.GetSection(userId).GetSection("endpoints");

    var endpointConfig = userConfig.GetSection(endpoint);

    if (!endpointConfig.Exists())
    {
        await next();
        return;
    }

    var window = endpointConfig["window"];
    var limit = endpointConfig["limit"];

    TimeSpan timeWindow = TimeSpan.Parse(window);
    int permitLimit = int.Parse(limit);

    var options = new RedisFixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = timeWindow,
        ConnectionMultiplexerFactory = () => ConnectionMultiplexer.Connect(redisConfig)
    };

    var limiter = new RedisFixedWindowRateLimiter<string>(key, options);
    var permit = await limiter.AcquireAsync();

    if (permit.IsAcquired)
    {
        await next();
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
    }
});

app.MapPost("v1/rateLimit/clear", async (RateLimitClearRequest request) =>
{
    var redis = ConnectionMultiplexer.Connect(redisConfig);
    var db = redis.GetDatabase();

    string prefix = $"rl:fw:{{{request.UserId}:{request.Endpoint}}}";

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
            db.KeyDelete(key);
            Console.WriteLine($"Chave {key} deletada com sucesso.");
        }
    } while (cursor != 0);

    redis.Close();

    if (keysDeleted.Count == 0)
    {
        return Results.NotFound(new
        {
            Message = $"Nenhuma chave encontrada para o usuário {request.UserId} e endpoint {request.Endpoint}."
        });
    }

    return Results.Ok(new
    {
        Message = $"Limites removidos para o usuário {request.UserId} e endpoint {request.Endpoint}.",
        DeletedKeys = keysDeleted
    });
});

app.MapGet("v1/weatherforecast", () =>
{
    return Results.Ok("Olá Henrique");
});

app.MapGet("v1/teste2", () =>
{
    return Results.Ok("Olá Henrique");
});

app.Run();

public class RateLimitClearRequest
{
    public string UserId { get; set; }
    public string Endpoint { get; set; }
}