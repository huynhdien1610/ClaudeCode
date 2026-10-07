using System.Reflection;
using Npgsql;

namespace Anima.SharedKernel;

/// <summary>
/// Chạy migration SQL nhúng theo thứ tự module rồi theo tên file. SQL là nguồn sự thật của schema
/// (ràng buộc, trigger chặn sửa ledger...) nên viết tay thay vì EF migrations; test tích hợp kiểm tra model khớp schema.
/// </summary>
public static class Migrator
{
    private const long LockKey = 727274;

    public static async Task RunAsync(NpgsqlDataSource ds, IEnumerable<(string Module, Assembly Assembly)> modules, ILogger? log, CancellationToken ct = default)
    {
        await using var conn = await ds.OpenConnectionAsync(ct);
        await Exec(conn, "SELECT pg_advisory_lock(@k)", ct, ("k", LockKey));
        try
        {
            await Exec(conn, "CREATE TABLE IF NOT EXISTS public.schema_migrations(module text NOT NULL, name text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(module, name))", ct);
            foreach (var (module, asm) in modules)
            {
                var names = asm.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal) && n.Contains(".Migrations.")).OrderBy(n => n, StringComparer.Ordinal);
                foreach (var res in names)
                {
                    var file = res[(res.LastIndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..];
                    await using (var chk = new NpgsqlCommand("SELECT 1 FROM public.schema_migrations WHERE module=@m AND name=@n", conn))
                    {
                        chk.Parameters.AddWithValue("m", module); chk.Parameters.AddWithValue("n", file);
                        if (await chk.ExecuteScalarAsync(ct) is not null) continue;
                    }
                    using var s = asm.GetManifestResourceStream(res)!;
                    using var sr = new StreamReader(s);
                    var sql = await sr.ReadToEndAsync(ct);
                    await using var tx = await conn.BeginTransactionAsync(ct);
                    await using (var cmd = new NpgsqlCommand(sql, conn, tx)) await cmd.ExecuteNonQueryAsync(ct);
                    await using (var ins = new NpgsqlCommand("INSERT INTO public.schema_migrations(module,name) VALUES(@m,@n)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("m", module); ins.Parameters.AddWithValue("n", file);
                        await ins.ExecuteNonQueryAsync(ct);
                    }
                    await tx.CommitAsync(ct);
                    log?.LogInformation("Applied migration {Module}/{File}", module, file);
                }
            }
        }
        finally { await Exec(conn, "SELECT pg_advisory_unlock(@k)", ct, ("k", LockKey)); }
    }

    private static async Task Exec(NpgsqlConnection c, string sql, CancellationToken ct, params (string, object)[] p)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
