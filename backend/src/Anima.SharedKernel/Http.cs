using System.Text.Json;
using Anima.Contracts;
using Microsoft.AspNetCore.Http;

namespace Anima.SharedKernel;

/// <summary>Chuyển DomainException thành JSON { code, message, details } với status phù hợp.</summary>
public sealed class DomainErrorMiddleware(RequestDelegate next, ILogger<DomainErrorMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (DomainException e) when (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = e.Status;
            await ctx.Response.WriteAsJsonAsync(new { code = e.Code, message = e.Message, details = e.Details }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (Exception e) when (!ctx.Response.HasStarted)
        {
            log.LogError(e, "Unhandled error on {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "Unexpected error" });
        }
    }
}

public static class SharedKernelRegistration
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection s, IConfiguration config, bool isDevelopment)
    {
        var cs = config.GetConnectionString("Anima") ?? throw new InvalidOperationException("ConnectionStrings:Anima is required");
        s.AddSingleton(Npgsql.NpgsqlDataSource.Create(cs));
        s.AddSingleton<IClock, SystemClock>();
        s.AddSingleton<ISecureRandom, SystemSecureRandom>();
        s.AddSingleton<IFieldCipher>(FieldCipher.FromConfig(config, isDevelopment));
        s.AddScoped<IUnitOfWork, UnitOfWork>();
        s.AddScoped<IIdempotency, Idempotency>();
        s.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        return s;
    }
}
