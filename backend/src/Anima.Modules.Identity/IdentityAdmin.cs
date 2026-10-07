using System.Globalization;
using Anima.Contracts;
using Anima.Identity.Contracts;
using Anima.SharedKernel;

namespace Anima.Identity;

internal sealed class IdentityAdminService(IUnitOfWork uow, IFieldCipher cipher) : IIdentityAdminApi, IStatsContributor
{
    private const string Cols = "id,email_enc,phone_enc,status,restriction_reason,legal_country,locale,created_at";

    private AdminAccountRow Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), MaskEmail(cipher.Decrypt(r.GetFieldValue<byte[]>(1))),
        r.IsDBNull(2) ? null : MaskPhone(cipher.Decrypt(r.GetFieldValue<byte[]>(2))), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5).Trim(), r.GetString(6), r.GetFieldValue<DateTimeOffset>(7));

    public async Task<IReadOnlyList<AdminAccountRow>> SearchAsync(string? query, int limit, CancellationToken ct)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
            return await uow.QueryAsync($"SELECT {Cols} FROM identity.account ORDER BY created_at DESC LIMIT @l", Map, ct, ("l", limit));
        if (Guid.TryParse(query, out var id))
            return await uow.QueryAsync($"SELECT {Cols} FROM identity.account WHERE id=@i", Map, ct, ("i", id));
        if (query.StartsWith('+'))
            return await uow.QueryAsync($"SELECT {Cols} FROM identity.account WHERE phone_hash=@h AND phone_verified_at IS NOT NULL", Map, ct, ("h", cipher.LookupHash(query)));
        return await uow.QueryAsync($"SELECT {Cols} FROM identity.account WHERE email_hash=@h", Map, ct, ("h", cipher.LookupHash(query.ToLowerInvariant())));
    }

    public async Task<AdminAccountRow?> GetAsync(Guid id, CancellationToken ct) =>
        (await uow.QueryAsync($"SELECT {Cols} FROM identity.account WHERE id=@i", Map, ct, ("i", id))).FirstOrDefault();

    public async Task<PiiView> RevealPiiAsync(Guid id, CancellationToken ct)
    {
        var rows = await uow.QueryAsync("SELECT email_enc, phone_enc, birth_date_enc FROM identity.account WHERE id=@i",
            r => new PiiView(cipher.Decrypt(r.GetFieldValue<byte[]>(0)), r.IsDBNull(1) ? null : cipher.Decrypt(r.GetFieldValue<byte[]>(1)), cipher.Decrypt(r.GetFieldValue<byte[]>(2))), ct, ("i", id));
        return rows.Count > 0 ? rows[0] : throw DomainException.NotFound("Account");
    }

    public Task<(string Before, string After)> ChangeStatusAsync(Guid id, string action, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var cur = (await uow.QueryAsync("SELECT status, status_before_restriction, restriction_reason, phone_verified_at IS NOT NULL FROM identity.account WHERE id=@i FOR UPDATE",
            r => (Status: r.GetString(0), Before: r.IsDBNull(1) ? null : r.GetString(1), Reason: r.IsDBNull(2) ? null : r.GetString(2), Phone: r.GetBoolean(3)), ct, ("i", id))).FirstOrDefault();
        if (cur.Status is null) throw DomainException.NotFound("Account");
        string next; string? savedBefore = cur.Before; string? reason = cur.Reason;
        switch (action)
        {
            case "restrict" when cur.Status is "Unverified" or "Verified":
                next = "Restricted"; savedBefore = cur.Status; reason = "FRAUD"; break;
            case "unrestrict" when cur.Status == "Restricted" && cur.Reason == "FRAUD":
                next = cur.Before ?? (cur.Phone ? "Verified" : "Unverified"); savedBefore = null; reason = null; break;
            case "ban" when cur.Status is "Unverified" or "Verified" or "Restricted" or "PendingDeletion":
                next = "Banned"; savedBefore = cur.Status == "Restricted" ? cur.Before ?? "Unverified" : cur.Status; reason = null; break;
            case "unban" when cur.Status == "Banned":
                next = cur.Phone ? "Verified" : "Unverified"; savedBefore = null; reason = null; break;     // BRD 8.1: Banned → Verified, chỉ Super Admin
            default:
                throw new DomainException(ErrorCodes.InvalidState, $"Cannot {action} an account that is {cur.Status}", 409);
        }
        await uow.ExecAsync("UPDATE identity.account SET status=@s, status_before_restriction=@b, restriction_reason=@r WHERE id=@i", ct, ("s", next), ("b", savedBefore), ("r", reason), ("i", id));
        return (cur.Status, next);
    }, ct);

    public async Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct)
    {
        var row = await uow.QueryAsync(@"SELECT count(*), count(*) FILTER (WHERE phone_verified_at IS NOT NULL), count(*) FILTER (WHERE status='Restricted'), count(*) FILTER (WHERE status='Banned'),
            count(*) FILTER (WHERE created_at > now() - interval '24 hours') FROM identity.account", r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4) }, ct);
        var v = row[0];
        return new Dictionary<string, long> { ["accounts_total"] = v[0], ["accounts_phone_verified"] = v[1], ["accounts_restricted"] = v[2], ["accounts_banned"] = v[3], ["accounts_new_24h"] = v[4] };
    }

    /// <summary>"n***@gmail.com" (SC-ADM-07 / mục 13.1 BRD).</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : email[0] + "***" + email[at..];
    }

    /// <summary>"+84901234123" → "090****123": số Việt Nam đổi về dạng nội địa, còn lại giữ 3 ký tự đầu và 3 cuối.</summary>
    public static string MaskPhone(string e164)
    {
        var s = e164.StartsWith("+84", StringComparison.Ordinal) ? "0" + e164[3..] : e164;
        return s.Length <= 6 ? "****" : string.Create(CultureInfo.InvariantCulture, $"{s[..3]}****{s[^3..]}");
    }
}
