using System.Security.Claims;

namespace Anima.SharedKernel;

public interface IClock { DateTimeOffset UtcNow { get; } }

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

/// <summary>CSPRNG (NFR-12). Không dùng System.Random cho bất cứ thứ gì liên quan tới kết quả pack.</summary>
public interface ISecureRandom { byte[] Bytes(int count); }

public sealed class SystemSecureRandom : ISecureRandom
{
    public byte[] Bytes(int count) => System.Security.Cryptography.RandomNumberGenerator.GetBytes(count);
}

public static class PrincipalExtensions
{
    public static Guid AccountId(this ClaimsPrincipal p)
    {
        var sub = p.FindFirstValue("sub") ?? p.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : throw new DomainException(Anima.Contracts.ErrorCodes.Unauthorized, "Missing account", 401);
    }
}

/// <summary>Sự kiện nội bộ phát đồng bộ trong cùng transaction (ví dụ AccountRegistered).</summary>
public interface IDomainEvent { }

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}

public interface IDomainEventPublisher { Task PublishAsync<TEvent>(TEvent e, CancellationToken ct) where TEvent : IDomainEvent; }

public sealed class DomainEventPublisher(IServiceProvider sp) : IDomainEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent e, CancellationToken ct) where TEvent : IDomainEvent
    {
        foreach (var h in sp.GetServices<IDomainEventHandler<TEvent>>()) await h.HandleAsync(e, ct);
    }
}

public interface IModule
{
    string Name { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder app);
    /// <summary>Assembly chứa migration SQL nhúng (Migrations/*.sql).</summary>
    System.Reflection.Assembly MigrationAssembly { get; }
}

/// <summary>Module có dữ liệu khởi tạo (catalog, tham số). Chạy sau migration khi Seed:Enabled = true.</summary>
public interface IModuleSeeder
{
    Task SeedAsync(CancellationToken ct);
}

/// <summary>Module đóng góp số liệu cho dashboard quản trị mà không để Admin truy vấn schema của module khác.</summary>
public interface IStatsContributor
{
    Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct);
}

/// <summary>Việc chạy một lần khi khởi động, luôn chạy (khác <see cref="IModuleSeeder"/> chỉ chạy khi bật seed): ví dụ tạo Super Admin đầu tiên.</summary>
public interface IStartupTask
{
    Task RunAsync(CancellationToken ct);
}
