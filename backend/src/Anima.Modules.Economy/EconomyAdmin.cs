using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Economy;

public sealed record EconomyParam(string Key, long Value, int Version, DateTimeOffset EffectiveFrom, string CreatedBy, string ApprovedBy, string? Note);
public sealed record EconomyChange(Guid Id, string Key, long Value, DateTimeOffset EffectiveFrom, string ProposedBy, DateTimeOffset ProposedAt, string? Note, string Status, string? DecidedBy, DateTimeOffset? DecidedAt);

/// <summary>Quản trị tham số kinh tế: đề xuất → người khác duyệt → version mới có thời điểm hiệu lực (BR-ECO-03, BR-ADM-02).</summary>
public interface IEconomyAdminApi
{
    Task<IReadOnlyList<EconomyParam>> HistoryAsync(CancellationToken ct);
    Task<IReadOnlyList<EconomyChange>> ChangesAsync(string? status, CancellationToken ct);
    Task<EconomyChange> ProposeAsync(string key, long value, DateTimeOffset? effectiveFrom, string proposedBy, string? note, CancellationToken ct);
    Task<EconomyChange> ApproveAsync(Guid id, string approvedBy, CancellationToken ct);
    Task<EconomyChange> RejectAsync(Guid id, string rejectedBy, CancellationToken ct);
}

internal sealed class EconomyAdminService(IUnitOfWork uow, IClock clock) : IEconomyAdminApi
{
    /// <summary>Tham số được phép đổi và khoảng giá trị hợp lệ.</summary>
    private static readonly Dictionary<string, (long Min, long Max)> Allowed = new()
    {
        [EconomyKeys.GemToCoinRate] = (1, 1_000),
        [EconomyKeys.CoinToGemRate] = (1, 10_000),
        [EconomyKeys.CoinToGemDailyCapGem] = (1, 1_000_000),
        [EconomyKeys.ForgeFeeCoin] = (1, 1_000_000),
        [EconomyKeys.ForgeFeeGem] = (1, 100_000),
        [EconomyKeys.ForgeDailyLimitUnverified] = (1, 1_000),
        [EconomyKeys.ForgeDailyLimitVerified] = (1, 10_000),
        [EconomyKeys.FirstLoginCoin] = (0, 100_000),
        [EconomyKeys.PityGuaranteeAfter] = (1, 10_000),
    };

    private const string ChangeCols = "id,key,value,effective_from,proposed_by,proposed_at,note,status,decided_by,decided_at";
    private static EconomyChange Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt64(2), r.GetFieldValue<DateTimeOffset>(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5),
        r.IsDBNull(6) ? null : r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));

    public async Task<IReadOnlyList<EconomyParam>> HistoryAsync(CancellationToken ct) =>
        await uow.QueryAsync("SELECT key,value,version,effective_from,created_by,approved_by,note FROM economy.parameter ORDER BY key, version DESC",
            r => new EconomyParam(r.GetString(0), r.GetInt64(1), r.GetInt32(2), r.GetFieldValue<DateTimeOffset>(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)), ct);

    public async Task<IReadOnlyList<EconomyChange>> ChangesAsync(string? status, CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {ChangeCols} FROM economy.change_request WHERE (@s::text IS NULL OR status=@s) ORDER BY proposed_at DESC LIMIT 100", Map, ct, ("s", status));

    public async Task<EconomyChange> ProposeAsync(string key, long value, DateTimeOffset? effectiveFrom, string proposedBy, string? note, CancellationToken ct)
    {
        if (!Allowed.TryGetValue(key, out var range)) throw DomainException.Validation($"Unknown or protected parameter '{key}'");
        if (value < range.Min || value > range.Max) throw DomainException.Validation($"Value must be between {range.Min} and {range.Max}");
        var from = effectiveFrom is { } f && f > clock.UtcNow ? f.ToUniversalTime() : clock.UtcNow;
        return (await uow.QueryAsync($@"INSERT INTO economy.change_request(id,key,value,effective_from,proposed_by,proposed_at,note,status) VALUES(@id,@k,@v,@f,@p,@now,@n,'pending') RETURNING {ChangeCols}",
            Map, ct, ("id", Guid.NewGuid()), ("k", key), ("v", value), ("f", from), ("p", proposedBy), ("now", clock.UtcNow), ("n", note)))[0];
    }

    private async Task<EconomyChange> LoadPendingAsync(Guid id, CancellationToken ct)
    {
        var c = (await uow.QueryAsync($"SELECT {ChangeCols} FROM economy.change_request WHERE id=@i FOR UPDATE", Map, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Change request");
        return c.Status == "pending" ? c : throw new DomainException(ErrorCodes.InvalidState, $"The request is already {c.Status}", 409);
    }

    public Task<EconomyChange> ApproveAsync(Guid id, string approvedBy, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var c = await LoadPendingAsync(id, ct);
        if (string.Equals(c.ProposedBy, approvedBy, StringComparison.OrdinalIgnoreCase)) throw new DomainException(ErrorCodes.SelfApprovalForbidden, "You cannot approve your own change", 403);
        // Version mới; các bút toán và hành vi phát sinh trước thời điểm hiệu lực giữ giá trị cũ (BR-ECO-03).
        await uow.ExecAsync(@"INSERT INTO economy.parameter(key,version,value,effective_from,created_by,approved_by,note)
            SELECT @k, COALESCE(max(version),0)+1, @v, @f, @p, @a, @n FROM economy.parameter WHERE key=@k", ct,
            ("k", c.Key), ("v", c.Value), ("f", c.EffectiveFrom), ("p", c.ProposedBy), ("a", approvedBy), ("n", c.Note));
        return (await uow.QueryAsync($"UPDATE economy.change_request SET status='approved', decided_by=@a, decided_at=@now WHERE id=@i RETURNING {ChangeCols}", Map, ct, ("a", approvedBy), ("now", clock.UtcNow), ("i", id)))[0];
    }, ct);

    public Task<EconomyChange> RejectAsync(Guid id, string rejectedBy, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var c = await LoadPendingAsync(id, ct);
        if (string.Equals(c.ProposedBy, rejectedBy, StringComparison.OrdinalIgnoreCase)) throw new DomainException(ErrorCodes.SelfApprovalForbidden, "You cannot decide your own change", 403);
        return (await uow.QueryAsync($"UPDATE economy.change_request SET status='rejected', decided_by=@a, decided_at=@now WHERE id=@i RETURNING {ChangeCols}", Map, ct, ("a", rejectedBy), ("now", clock.UtcNow), ("i", id)))[0];
    }, ct);
}
