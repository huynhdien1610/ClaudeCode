using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anima.Contracts;
using Microsoft.AspNetCore.Http;

namespace Anima.SharedKernel;

public interface IIdempotency
{
    /// <summary>Chạy <paramref name="work"/> đúng một lần cho (tài khoản, key). Trùng key trả lại kết quả đã lưu.</summary>
    Task<T> RunAsync<T>(Guid accountId, string key, string endpoint, string requestHash, Func<Task<T>> work, CancellationToken ct);
}

public sealed class Idempotency(IUnitOfWork uow) : IIdempotency
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<T> RunAsync<T>(Guid accountId, string key, string endpoint, string requestHash, Func<Task<T>> work, CancellationToken ct) =>
        uow.RunAsync(async () =>
        {
            var inserted = await uow.ExecAsync(
                "INSERT INTO shared.idempotency(account_id,key,endpoint,request_hash) VALUES(@a,@k,@e,@h) ON CONFLICT DO NOTHING", ct,
                ("a", accountId), ("k", key), ("e", endpoint), ("h", requestHash));
            if (inserted == 0)
            {
                var rows = await uow.QueryAsync("SELECT endpoint, request_hash, response::text FROM shared.idempotency WHERE account_id=@a AND key=@k",
                    r => (Endpoint: r.GetString(0), Hash: r.GetString(1), Response: r.IsDBNull(2) ? null : r.GetString(2)), ct, ("a", accountId), ("k", key));
                if (rows.Count == 0 || rows[0].Endpoint != endpoint || rows[0].Hash != requestHash || rows[0].Response is null)
                    throw DomainException.Conflict(ErrorCodes.IdempotencyKeyReused, "Idempotency key was already used for a different request");
                return JsonSerializer.Deserialize<T>(rows[0].Response!, Json)!;
            }
            var result = await work();
            await uow.ExecAsync("UPDATE shared.idempotency SET response=@r::jsonb WHERE account_id=@a AND key=@k", ct,
                ("r", JsonSerializer.Serialize(result, Json)), ("a", accountId), ("k", key));
            return result;
        }, ct);

    public static string Hash(params object?[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts.Select(p => p?.ToString() ?? "")))));
}

public static class HttpIdempotencyExtensions
{
    public static string RequireIdempotencyKey(this HttpRequest req)
    {
        var k = req.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(k) || k.Length > 100)
            throw new DomainException(ErrorCodes.IdempotencyKeyRequired, "Idempotency-Key header is required (max 100 chars)", 400);
        return k;
    }
}
