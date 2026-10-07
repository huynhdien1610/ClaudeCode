using System.Text.Json;
using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Catalog;

public sealed record AdminPack(string Code, string Name, string Kind, bool OnSale, string? OffSaleReason);
public sealed record OddsVersionAdmin(int Id, string PackCode, int Version, string Status, DateTimeOffset EffectiveFrom, string CreatedBy, string? ApprovedBy, IReadOnlyList<RarityOdds> Entries, bool InEffect);
public sealed record AdminCard(int Id, string Code, string Name, string Element, string Rarity, string CardType, int MaxSupply, int Issued, int Burned, bool Discontinued);

/// <summary>Quản trị catalog: tỷ lệ rơi có version theo maker-checker, ngừng phát hành thẻ. Phân quyền và audit do module Admin đảm nhiệm.</summary>
public interface ICatalogAdminApi
{
    Task<IReadOnlyList<AdminPack>> ListPacksAsync(CancellationToken ct);
    Task<IReadOnlyList<OddsVersionAdmin>> ListOddsAsync(string packCode, CancellationToken ct);
    Task<OddsVersionAdmin> CreateDraftAsync(string packCode, IReadOnlyList<RarityOdds> entries, DateTimeOffset? effectiveFrom, string createdBy, CancellationToken ct);
    Task<OddsVersionAdmin> UpdateDraftAsync(string packCode, int version, IReadOnlyList<RarityOdds> entries, CancellationToken ct);
    Task<OddsVersionAdmin> ApproveAsync(string packCode, int version, string approvedBy, CancellationToken ct);
    Task<IReadOnlyList<AdminCard>> ListCardsAsync(CancellationToken ct);
    Task DiscontinueCardAsync(int cardId, CancellationToken ct);
    Task DeleteCardAsync(int cardId, CancellationToken ct);
}

internal sealed class CatalogAdminService(IUnitOfWork uow, IClock clock) : ICatalogAdminApi
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AdminPack>> ListPacksAsync(CancellationToken ct) =>
        await uow.QueryAsync("SELECT code,name,kind,on_sale,off_sale_reason FROM catalog.pack_definition WHERE kind <> 'welcome' ORDER BY code",
            r => new AdminPack(r.GetString(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetString(4)), ct);

    private const string OddsSql = @"SELECT v.id,v.pack_code,v.version,v.status,v.effective_from,v.created_by,v.approved_by,v.entries::text,
        v.id = (SELECT x.id FROM catalog.odds_version x WHERE x.pack_code=v.pack_code AND x.status IN ('approved','active') AND x.effective_from <= now() ORDER BY x.effective_from DESC, x.version DESC LIMIT 1)
        FROM catalog.odds_version v";
    private static OddsVersionAdmin MapOdds(Npgsql.NpgsqlDataReader r) => new(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6), JsonSerializer.Deserialize<List<RarityOdds>>(r.GetString(7), J)!, !r.IsDBNull(8) && r.GetBoolean(8));

    public async Task<IReadOnlyList<OddsVersionAdmin>> ListOddsAsync(string packCode, CancellationToken ct) =>
        await uow.QueryAsync(OddsSql + " WHERE v.pack_code=@p ORDER BY v.version DESC", MapOdds, ct, ("p", packCode));

    /// <summary>SC-ADM-02: đủ 6 rarity, mỗi rarity một lần, tổng đúng 1,000,000 ppm (100.00%).</summary>
    private static string Validate(IReadOnlyList<RarityOdds> entries)
    {
        if (entries is null || entries.Count != Rarities.Order.Length || entries.Select(e => e.Rarity).Distinct().Count() != entries.Count || entries.Any(e => Rarities.Index(e.Rarity) < 0))
            throw DomainException.Validation("Odds must list every rarity exactly once");
        if (entries.Any(e => e.Ppm < 0 || e.Ppm > 1_000_000)) throw DomainException.Validation("Each rate must be between 0% and 100%");
        if (entries.Sum(e => (long)e.Ppm) != 1_000_000) throw new DomainException(ErrorCodes.DropRateSumInvalid, "The rates must add up to exactly 100.00%", 400, new { sumPpm = entries.Sum(e => (long)e.Ppm) });
        return JsonSerializer.Serialize(entries.OrderBy(e => Rarities.Index(e.Rarity)).ToList(), J);
    }

    private async Task<string> RequireOddsPackAsync(string packCode, CancellationToken ct)
    {
        var kind = await uow.ScalarAsync<string>("SELECT kind FROM catalog.pack_definition WHERE code=@c", ct, ("c", packCode));
        return kind is "standard" or "forge" ? kind : throw DomainException.NotFound("Pack");
    }

    public Task<OddsVersionAdmin> CreateDraftAsync(string packCode, IReadOnlyList<RarityOdds> entries, DateTimeOffset? effectiveFrom, string createdBy, CancellationToken ct) => uow.RunAsync(async () =>
    {
        await RequireOddsPackAsync(packCode, ct);
        var json = Validate(entries);
        var from = effectiveFrom is { } f && f > clock.UtcNow ? f.ToUniversalTime() : clock.UtcNow;
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "odds:" + packCode));
        var id = await uow.ScalarAsync<int>(@"INSERT INTO catalog.odds_version(pack_code,version,status,effective_from,created_by,entries)
            SELECT @p, COALESCE(max(version),0)+1, 'draft', @f, @c, @e::jsonb FROM catalog.odds_version WHERE pack_code=@p RETURNING id", ct, ("p", packCode), ("f", from), ("c", createdBy), ("e", json));
        return (await uow.QueryAsync(OddsSql + " WHERE v.id=@i", MapOdds, ct, ("i", id)))[0];
    }, ct);

    private async Task<OddsVersionAdmin> LoadAsync(string packCode, int version, CancellationToken ct) =>
        (await uow.QueryAsync(OddsSql + " WHERE v.pack_code=@p AND v.version=@n FOR UPDATE OF v", MapOdds, ct, ("p", packCode), ("n", version))).FirstOrDefault() ?? throw DomainException.NotFound("Odds version");

    public Task<OddsVersionAdmin> UpdateDraftAsync(string packCode, int version, IReadOnlyList<RarityOdds> entries, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var cur = await LoadAsync(packCode, version, ct);
        // SC-ADM-09: version đã duyệt hoặc đang hiệu lực không được sửa; phải tạo version mới.
        if (cur.Status != "draft") throw new DomainException(ErrorCodes.VersionLocked, "This odds version is locked. Create a new version instead", 409);
        var json = Validate(entries);
        await uow.ExecAsync("UPDATE catalog.odds_version SET entries=@e::jsonb WHERE id=@i", ct, ("e", json), ("i", cur.Id));
        return (await uow.QueryAsync(OddsSql + " WHERE v.id=@i", MapOdds, ct, ("i", cur.Id)))[0];
    }, ct);

    public Task<OddsVersionAdmin> ApproveAsync(string packCode, int version, string approvedBy, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var cur = await LoadAsync(packCode, version, ct);
        if (cur.Status != "draft") throw new DomainException(ErrorCodes.VersionLocked, "Only a draft can be approved", 409);
        if (string.Equals(cur.CreatedBy, approvedBy, StringComparison.OrdinalIgnoreCase)) throw new DomainException(ErrorCodes.SelfApprovalForbidden, "You cannot approve your own change", 403);
        var from = cur.EffectiveFrom > clock.UtcNow ? cur.EffectiveFrom : clock.UtcNow;
        await uow.ExecAsync("UPDATE catalog.odds_version SET status='approved', approved_by=@a, effective_from=@f WHERE id=@i", ct, ("a", approvedBy), ("f", from), ("i", cur.Id));
        // BR-SUP-04: pack ngừng bán vì hết bản được mở bán lại khi có version tỷ lệ mới công bố.
        await uow.ExecAsync("UPDATE catalog.pack_definition SET on_sale=true, off_sale_reason=NULL WHERE code=@c AND kind='standard' AND off_sale_reason LIKE 'RARITY_EXHAUSTED%'", ct, ("c", packCode));
        return (await uow.QueryAsync(OddsSql + " WHERE v.id=@i", MapOdds, ct, ("i", cur.Id)))[0];
    }, ct);

    public async Task<IReadOnlyList<AdminCard>> ListCardsAsync(CancellationToken ct) =>
        await uow.QueryAsync(@"SELECT d.id,d.code,d.name,d.element,d.rarity,d.card_type,e.max_supply,e.issued,e.burned,d.discontinued
            FROM catalog.card_definition d JOIN catalog.edition e ON e.card_definition_id=d.id ORDER BY d.id",
            r => new AdminCard(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6), r.GetInt32(7), r.GetInt32(8), r.GetBoolean(9)), ct);

    public async Task DiscontinueCardAsync(int cardId, CancellationToken ct)
    {
        if (await uow.ExecAsync("UPDATE catalog.card_definition SET discontinued=true WHERE id=@i", ct, ("i", cardId)) == 0) throw DomainException.NotFound("Card");
    }

    public async Task DeleteCardAsync(int cardId, CancellationToken ct)
    {
        var issued = await uow.ScalarAsync<int?>("SELECT issued FROM catalog.edition WHERE card_definition_id=@i", ct, ("i", cardId)) ?? throw DomainException.NotFound("Card");
        // SC-ADM-13: không xóa cứng thẻ đã có bản; chỉ được ngừng phát hành.
        if (issued > 0) throw new DomainException(ErrorCodes.CardHasInstances, "A card with issued copies cannot be deleted. Discontinue it instead", 409, new { issued });
        throw new DomainException(ErrorCodes.CardPublishedImmutable, "A published card definition cannot be deleted. Discontinue it instead", 409);
    }
}
