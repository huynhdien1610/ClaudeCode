using System.Security.Claims;
using System.Text.Json;
using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.Economy;
using Anima.Fairness;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;

namespace Anima.Gacha;

public sealed record PurchaseRequest(string? Currency, int Quantity = 1);
public sealed record PurchaseResult(IReadOnlyList<Guid> PackInstanceIds, string Currency, long Price, Balances Balances);
public sealed record OpenedCard(Guid InstanceId, long Serial, string Edition, int EditionNo, string Rarity, bool Soulbound, object Card);
public sealed record OpenResult(Guid PackInstanceId, string PackCode, IReadOnlyList<OpenedCard> Cards, string MaxRarity, bool Climax, int PityBefore, int PityAfter, bool PityTriggered, object Fairness);
public sealed record PackInstanceDto(Guid Id, string PackCode, string Kind, bool Soulbound, DateTimeOffset CreatedAt);

public sealed class GachaModule : IModule
{
    public string Name => "gacha";
    public System.Reflection.Assembly MigrationAssembly => typeof(GachaModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<ICardIssuer, CardIssuer>();
        s.AddScoped<GachaService>();
        s.AddScoped<IDomainEventHandler<AccountRegistered>, WelcomePackGrant>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1").RequireAuthorization();
        g.MapPost("/packs/{code}/purchase", async (HttpRequest req, ClaimsPrincipal u, string code, PurchaseRequest r, GachaService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            return Results.Ok(await idem.RunAsync(acc, key, "gacha.purchase", Idempotency.Hash(code, r.Currency, r.Quantity), () => s.PurchaseAsync(acc, code, r.Currency, r.Quantity, key, ct), ct));
        });
        g.MapGet("/me/packs", async (ClaimsPrincipal u, GachaService s, CancellationToken ct) => Results.Ok(await s.ListUnopenedAsync(u.AccountId(), ct)));
        g.MapGet("/me/pity/{code}", async (ClaimsPrincipal u, string code, GachaService s, CancellationToken ct) => Results.Ok(await s.PityAsync(u.AccountId(), code, ct)));
        g.MapPost("/pack-instances/{id:guid}/open", async (ClaimsPrincipal u, Guid id, string? locale, GachaService s, CancellationToken ct) => Results.Ok(await s.OpenAsync(u.AccountId(), id, locale, ct)));
        g.MapGet("/pack-instances/{id:guid}/opening", async (ClaimsPrincipal u, Guid id, string? locale, GachaService s, CancellationToken ct) => Results.Ok(await s.GetOpeningAsync(u.AccountId(), id, locale, ct)));
    }
}

internal sealed class WelcomePackGrant(GachaService svc) : IDomainEventHandler<AccountRegistered>
{
    public Task HandleAsync(AccountRegistered e, CancellationToken ct) => svc.GrantWelcomePackAsync(e.AccountId, ct);
}

public sealed class GachaService(IUnitOfWork uow, IClock clock, ICatalogApi catalog, IWalletApi wallet, IFairnessApi fairness, IEconomyApi economy,
    IIdentityApi identity, ICardIssuer issuer)
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    // ---------- Mua pack (BR-PACK-04, US-03.1) ----------
    public Task<PurchaseResult> PurchaseAsync(Guid acc, string code, string? currency, int quantity, string idemKey, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (!Currencies.IsValid(currency)) throw DomainException.Validation("currency must be GEM or COIN");
        if (quantity is < 1 or > 10) throw DomainException.Validation("quantity must be between 1 and 10");
        var info = await identity.GetAsync(acc, ct);
        if (info.Status is "Restricted" or "Banned" or "PendingDeletion" or "Deleted") throw new DomainException(ErrorCodes.AccountRestricted, "This account cannot buy packs", 403);
        var pack = await catalog.GetPackAsync(code, ct);
        if (pack is null || pack.Kind != "standard" || !pack.OnSale) throw new DomainException(ErrorCodes.PackNotOnSale, "This pack is not on sale", 409);
        var unit = currency == Currencies.Gem ? pack.PriceGem : pack.PriceCoin;
        if (unit is null) throw new DomainException(ErrorCodes.PackNotOnSale, $"This pack cannot be bought with {currency}", 409);
        var odds = await catalog.GetActiveOddsAsync(code, ct);
        var total = checked(unit.Value * quantity);
        // Trừ tiền và tạo Pack Instance trong cùng một transaction (US-03.1).
        var entry = await wallet.DebitAsync(acc, currency!, total, "PACK_PURCHASE", "pack", code, $"purchase:{idemKey}", ct);
        var ids = new List<Guid>();
        for (var i = 0; i < quantity; i++)
        {
            var id = Guid.NewGuid(); ids.Add(id);
            await uow.ExecAsync("INSERT INTO gacha.pack_instance(id,account_id,pack_code,kind,odds_version_id,status,purchase_ledger_entry_id,created_at) VALUES(@id,@a,@c,'standard',@o,'Unopened',@l,@now)", ct,
                ("id", id), ("a", acc), ("c", code), ("o", odds.Id), ("l", entry.Id), ("now", clock.UtcNow));
        }
        return new PurchaseResult(ids, currency!, total, await wallet.GetBalancesAsync(acc, ct));
    }, ct);

    // ---------- Gói chào mừng (BR-NEW-01) ----------
    public async Task GrantWelcomePackAsync(Guid acc, CancellationToken ct) =>
        await uow.ExecAsync("INSERT INTO gacha.pack_instance(id,account_id,pack_code,kind,status,soulbound,created_at) VALUES(@id,@a,'welcome','welcome','Unopened',true,@now)", ct,
            ("id", Guid.NewGuid()), ("a", acc), ("now", clock.UtcNow));

    public async Task<IReadOnlyList<PackInstanceDto>> ListUnopenedAsync(Guid acc, CancellationToken ct) =>
        await uow.QueryAsync("SELECT id,pack_code,kind,soulbound,created_at FROM gacha.pack_instance WHERE account_id=@a AND status='Unopened' ORDER BY created_at, id",
            r => new PackInstanceDto(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetFieldValue<DateTimeOffset>(4)), ct, ("a", acc));

    public async Task<object> PityAsync(Guid acc, string code, CancellationToken ct) => new
    {
        packCode = code,
        count = await uow.ScalarAsync<int?>("SELECT count FROM gacha.pity_counter WHERE account_id=@a AND pack_code=@c", ct, ("a", acc), ("c", code)) ?? 0,
        guaranteeAfter = await economy.GetAsync(EconomyKeys.PityGuaranteeAfter, ct),
    };

    // ---------- Mở pack (SAD 8.2) ----------
    public Task<OpenResult> OpenAsync(Guid acc, Guid packInstanceId, string? locale, CancellationToken ct) => uow.RunAsync(async () =>
    {
        // Khóa Pack Instance: hai yêu cầu mở đồng thời xếp hàng, yêu cầu sau thấy Opened (SC-PACK-13).
        var pi = (await uow.QueryAsync("SELECT account_id,pack_code,kind,odds_version_id,status,soulbound FROM gacha.pack_instance WHERE id=@i FOR UPDATE",
            r => (Owner: r.GetGuid(0), Code: r.GetString(1), Kind: r.GetString(2), Odds: r.IsDBNull(3) ? (int?)null : r.GetInt32(3), Status: r.GetString(4), Soulbound: r.GetBoolean(5)), ct, ("i", packInstanceId))).FirstOrDefault();
        if (pi.Code is null || pi.Owner != acc) throw new DomainException(ErrorCodes.PackNotOwned, "Pack not found", 404);
        if (pi.Status != "Unopened") throw DomainException.Conflict(ErrorCodes.PackAlreadyOpened, "This pack was already opened");
        var info = await identity.GetAsync(acc, ct);
        if (info.Status == "Banned") throw new DomainException(ErrorCodes.AccountRestricted, "This account is banned", 403);
        var loc = Locales.Normalize(locale ?? info.Locale);

        var seed = await fairness.BeginRollAsync(acc, ct);
        var issued = new List<IssuedCard>();
        var rarityBefore = new List<string>(); int pityBefore = 0, pityAfter = 0; var pityTriggered = false;
        var extra = new Dictionary<string, object?>();

        if (pi.Kind == "welcome")
        {
            // 5 Anima Common thuộc 5 hệ khác nhau trong 7 hệ vòng nhân quả: xáo 7 hệ bằng giá trị quay, lấy 5 đầu.
            var els = Elements.Cycle.ToList();
            for (var i = els.Count - 1; i > 0; i--) { var j = seed.Roll(i) % (i + 1); (els[i], els[j]) = (els[j], els[i]); }
            var chosen = els.Take(5).ToList();
            extra["elements"] = chosen;
            for (var slot = 0; slot < 5; slot++)
                issued.Add(await issuer.IssueAsync(acc, seed, slot, "common", seed.Roll(slot), true, "WELCOME", packInstanceId.ToString(), null, ct, CardTypes.Anima, chosen[slot]));
        }
        else
        {
            var pack = (await catalog.GetPackAsync(pi.Code, ct))!;
            var odds = await catalog.GetOddsAsync(pi.Odds!.Value, ct);
            var table = odds.Entries.OrderBy(e => Rarities.Index(e.Rarity)).Select(e => (e.Rarity, e.Ppm)).ToList();
            if (table.Sum(t => t.Ppm) != FairnessMath.Range) throw new InvalidOperationException("Odds version does not sum to 1,000,000");
            var rolls = Enumerable.Range(0, pack.CardsPerPack).Select(seed.Roll).ToArray();
            var rarities = rolls.Select(r => FairnessMath.Pick(r, table)).ToList();
            rarityBefore.AddRange(rarities);

            // Pity (BR-PACK-05): khóa bộ đếm, đạt ngưỡng mà chưa có Legendary+ thì đổi slot thấp nhất thành Legendary.
            await uow.ExecAsync("INSERT INTO gacha.pity_counter(account_id,pack_code) VALUES(@a,@c) ON CONFLICT DO NOTHING", ct, ("a", acc), ("c", pi.Code));
            pityBefore = await uow.ScalarAsync<int>("SELECT count FROM gacha.pity_counter WHERE account_id=@a AND pack_code=@c FOR UPDATE", ct, ("a", acc), ("c", pi.Code));
            var threshold = (int)await economy.GetAsync(EconomyKeys.PityGuaranteeAfter, ct);
            if (pityBefore >= threshold && !rarities.Any(Rarities.IsLegendaryPlus))
            {
                var low = 0;
                for (var i = 1; i < rarities.Count; i++) if (Rarities.Index(rarities[i]) < Rarities.Index(rarities[low])) low = i;
                rarities[low] = "legendary"; pityTriggered = true;
            }
            pityAfter = rarities.Any(Rarities.IsLegendaryPlus) ? 0 : pityBefore + 1;
            await uow.ExecAsync("UPDATE gacha.pity_counter SET count=@n WHERE account_id=@a AND pack_code=@c", ct, ("n", pityAfter), ("a", acc), ("c", pi.Code));

            for (var slot = 0; slot < rarities.Count; slot++)
                issued.Add(await issuer.IssueAsync(acc, seed, slot, rarities[slot], rolls[slot], pi.Soulbound, "PACK", packInstanceId.ToString(), pi.Code, ct));
        }

        await uow.ExecAsync("UPDATE gacha.pack_instance SET status='Opened', opened_at=@now WHERE id=@i", ct, ("now", clock.UtcNow), ("i", packInstanceId));
        var traceJson = JsonSerializer.Serialize(new { traces = issued.Select(x => new { x.Trace, instanceId = x.Instance.Id, serial = x.Instance.Serial, editionNo = x.Instance.EditionNo, cardDefinitionId = x.Definition.Id }), rarityBeforePity = rarityBefore, extra }, J);
        await uow.ExecAsync(@"INSERT INTO gacha.pack_opening(pack_instance_id,seed_id,seed_hash,client_seed,nonce,results,pity_before,pity_after,pity_triggered,opened_at)
            VALUES(@i,@s,@h,@c,@n,@r::jsonb,@pb,@pa,@pt,@now)", ct,
            ("i", packInstanceId), ("s", seed.SeedId), ("h", seed.SeedHash), ("c", seed.ClientSeed), ("n", seed.Nonce), ("r", traceJson), ("pb", pityBefore), ("pa", pityAfter), ("pt", pityTriggered), ("now", clock.UtcNow));

        // Thứ tự lật: rarity tăng dần, hiếm nhất lật cuối (BR-PACK-06); cùng rarity giữ thứ tự slot.
        var ordered = issued.OrderBy(x => Rarities.Index(x.Definition.Rarity)).ThenBy(x => x.Trace.Slot).ToList();
        var cards = new List<OpenedCard>();
        foreach (var x in ordered)
            cards.Add(new OpenedCard(x.Instance.Id, x.Instance.Serial, $"#{x.Instance.EditionNo}/{x.Definition.MaxSupply}", x.Instance.EditionNo, x.Definition.Rarity, x.Instance.Soulbound,
                await catalog.ToPublicAsync(x.Definition, loc, true, ct)));
        var max = ordered[^1].Definition.Rarity;
        return new OpenResult(packInstanceId, pi.Code, cards, max, Rarities.IsEpicPlus(max), pityBefore, pityAfter, pityTriggered,
            new { seedHash = seed.SeedHash, clientSeed = seed.ClientSeed, nonce = seed.Nonce });
    }, ct);

    /// <summary>Bản ghi mở pack để công cụ kiểm chứng tính lại; cũng dùng để phát lại màn Summary (BR-PACK-02).</summary>
    public async Task<object> GetOpeningAsync(Guid acc, Guid packInstanceId, string? locale, CancellationToken ct)
    {
        var row = (await uow.QueryAsync(@"SELECT p.seed_hash,p.client_seed,p.nonce,p.results::text,p.pity_before,p.pity_after,p.pity_triggered,p.opened_at,i.pack_code
            FROM gacha.pack_opening p JOIN gacha.pack_instance i ON i.id=p.pack_instance_id WHERE p.pack_instance_id=@i AND i.account_id=@a",
            r => (Hash: r.GetString(0), Client: r.GetString(1), Nonce: r.GetInt32(2), Results: r.GetString(3), PB: r.GetInt32(4), PA: r.GetInt32(5), PT: r.GetBoolean(6), At: r.GetFieldValue<DateTimeOffset>(7), Code: r.GetString(8)), ct, ("i", packInstanceId), ("a", acc))).FirstOrDefault();
        if (row.Hash is null) throw DomainException.NotFound("Pack opening");
        var loc = Locales.Normalize(locale ?? (await identity.GetAsync(acc, ct)).Locale);
        using var doc = JsonDocument.Parse(row.Results);
        var cards = new List<object>();
        foreach (var t in doc.RootElement.GetProperty("traces").EnumerateArray())
        {
            var def = (await catalog.GetCardAsync(t.GetProperty("cardDefinitionId").GetInt32(), ct))!;
            cards.Add(new { instanceId = t.GetProperty("instanceId").GetGuid(), serial = t.GetProperty("serial").GetInt64(), edition = $"#{t.GetProperty("editionNo").GetInt32()}/{def.MaxSupply}", trace = t.GetProperty("trace").Clone(), card = await catalog.ToPublicAsync(def, loc, true, ct) });
        }
        return new { packInstanceId, packCode = row.Code, openedAt = row.At, fairness = new { seedHash = row.Hash, clientSeed = row.Client, nonce = row.Nonce }, pity = new { before = row.PB, after = row.PA, triggered = row.PT }, cards, raw = doc.RootElement.Clone() };
    }
}
