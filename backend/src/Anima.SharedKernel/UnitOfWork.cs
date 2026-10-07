using System.Data;
using Npgsql;

namespace Anima.SharedKernel;

/// <summary>
/// Một connection + một transaction PostgreSQL cho cả request. Mọi repository của các module dùng chung
/// connection này nên giao dịch xuyên module (ví dụ mua pack: trừ ví + tạo Pack Instance) là nguyên tử (SAD 4.1).
/// Npgsql chạy mọi lệnh trên connection trong transaction đang mở nên repository chỉ cần dùng <see cref="IUnitOfWork.Command"/>.
/// </summary>
public interface IUnitOfWork
{
    NpgsqlConnection Connection { get; }
    bool InTransaction { get; }
    /// <summary>Chạy <paramref name="work"/> trong transaction; gọi lồng thì tham gia transaction hiện có.</summary>
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
    Task RunAsync(Func<Task> work, CancellationToken ct = default);
    NpgsqlCommand Command(string sql, params (string Name, object? Value)[] parameters);
}

public sealed class UnitOfWork(NpgsqlDataSource dataSource) : IUnitOfWork, IAsyncDisposable
{
    private int _depth;
    public NpgsqlConnection Connection { get; } = dataSource.CreateConnection();
    public bool InTransaction => _depth > 0;

    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        if (_depth > 0) { _depth++; try { return await work(); } finally { _depth--; } }

        if (Connection.State != ConnectionState.Open) await Connection.OpenAsync(ct);
        await using var tx = await Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        _depth = 1;
        try
        {
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { /* connection đã hỏng */ }
            throw;
        }
        finally
        {
            _depth = 0;
            await Connection.CloseAsync();
        }
    }

    public Task RunAsync(Func<Task> work, CancellationToken ct = default) =>
        RunAsync<object?>(async () => { await work(); return null; }, ct);

    public NpgsqlCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.Add(new NpgsqlParameter(name, value ?? DBNull.Value));
        return cmd;
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}

public static class UnitOfWorkExtensions
{
    /// <summary>Lệnh đọc/ghi đơn lẻ ngoài RunAsync: tự mở connection rồi trả về pool. Trong transaction connection đã mở sẵn.</summary>
    private static async Task<T> WithConnection<T>(IUnitOfWork uow, Func<Task<T>> action, CancellationToken ct)
    {
        var opened = false;
        if (uow.Connection.State != ConnectionState.Open) { await uow.Connection.OpenAsync(ct); opened = true; }
        try { return await action(); }
        finally { if (opened) await uow.Connection.CloseAsync(); }
    }

    public static Task<int> ExecAsync(this IUnitOfWork uow, string sql, CancellationToken ct, params (string, object?)[] p) =>
        WithConnection(uow, async () =>
        {
            await using var c = uow.Command(sql, p);
            return await c.ExecuteNonQueryAsync(ct);
        }, ct);

    public static Task<T?> ScalarAsync<T>(this IUnitOfWork uow, string sql, CancellationToken ct, params (string, object?)[] p) =>
        WithConnection(uow, async () =>
        {
            await using var c = uow.Command(sql, p);
            var o = await c.ExecuteScalarAsync(ct);
            return o is null or DBNull ? default : (T)o;
        }, ct);

    public static Task<List<T>> QueryAsync<T>(this IUnitOfWork uow, string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct, params (string, object?)[] p) =>
        WithConnection(uow, async () =>
        {
            await using var c = uow.Command(sql, p);
            await using var r = await c.ExecuteReaderAsync(ct);
            var list = new List<T>();
            while (await r.ReadAsync(ct)) list.Add(map(r));
            return list;
        }, ct);

    public static async Task<T?> QueryOneAsync<T>(this IUnitOfWork uow, string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct, params (string, object?)[] p) where T : class
    {
        var l = await uow.QueryAsync(sql, map, ct, p);
        return l.Count > 0 ? l[0] : null;
    }
}
