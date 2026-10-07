using System.Security.Claims;
using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.Economy;
using Anima.Fairness;
using Anima.Gacha;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;

namespace Anima.Forge;

public sealed record ForgeRequest(Guid[]? CardInstanceIds, string? FeeCurrency);
public sealed record ForgeResult(Guid SealedCardId, string FeeCurrency, long Fee, Balances Balances);
public sealed record SealedCardDto(Guid Id, DateTimeOffset CreatedAt);
public sealed record RevealResult(Guid SealedCardId, OpenedCard Card, object Fairness);

public sealed class ForgeModule : IModule
{
    public string Name => "forge";
    public System.Reflection.Assembly MigrationAssembly => typeof(ForgeModule).Assembly;
    public void ConfigureServices(IServiceCollection s, IConfiguration c) => s.AddScoped<ForgeService>();

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1").RequireAuthorization();
        g.MapPost("/forge", async (HttpRequest req, ClaimsPrincipal u, ForgeRequest r, ForgeService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            var ids = (r.CardInstanceIds ?? []).Select(x => x.ToString()).ToArray();
            return Results.Ok(await idem.RunAsync(acc, key, "forge.forge", Idempotency.Hash(string.Join(',', ids), r.FeeCurrency), () => s.ForgeAsync(acc, r.CardInstanceIds, r.FeeCurrency, key, ct), ct));
        });
        g.MapGet("/me/sealed", async (ClaimsPrincipal u, ForgeService s, CancellationToken ct) => Results.Ok(await s.ListSealedAsync(u.AccountId(), ct)));
        g.MapPost("/forge/sealed/{id:guid}/reveal", async (ClaimsPrincipal u, Guid id, string? locale, ForgeService s, CancellationToken ct) => Results.Ok(await s.RevealAsync(u.AccountId(), id, locale, ct)));
        g.MapGet("/forge/info", async (ClaimsPrincipal u, ForgeService s, CancellationToken ct) => Results.Ok(await s.InfoAsync(u.AccountId(), ct)));
    }
}

public sealed class ForgeService(IUnitOfWork uow, IClock clock, ICatalogApi catalog, ICollectionApi collection, IWalletApi wallet, IFairnessApi fairness,
    IEconomyApi economy, IIdentityApi identity, ICardIssuer issuer)
{
    // ---------- Rèn: 2 thẻ + phí → 1 thẻ chưa lật (BR-FRG-01/02, SAD 8.3) ----------
    public Task<ForgeResult> ForgeAsync(Guid acc, Guid[]? ids, string? feeCurrency, string idemKey, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (ids is null || ids.Length != 2) throw new DomainException(ErrorCodes.ForgeRequiresTwoCards, "Forging needs exactly two cards");
        if (ids[0] == ids[1]) throw new DomainException(ErrorCodes.ForgeDuplicateInput, "The same card cannot be used twice");
        if (!Currencies.IsValid(feeCurrency)) throw DomainException.Validation("feeCurrency must be GEM or COIN");
        var info = await identity.GetAsync(acc, ct);
        if (info.Status is "Restricted" or "Banned" or "PendingDeletion" or "Deleted") throw new DomainException(ErrorCodes.AccountRestricted, "This account cannot forge", 403);

        // Serialize các lần rèn của cùng tài khoản: bảo vệ hạn mức ngày (BR-FRG-07) và cặp thẻ đầu vào (SC-FRG-09).
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "forge:" + acc));
        var limit = await economy.GetAsync(info.PhoneVerified ? EconomyKeys.ForgeDailyLimitVerified : EconomyKeys.ForgeDailyLimitUnverified, ct);
        var (from, to) = TimeZones.LocalDayRange(clock.UtcNow, info.Timezone);
        var used = await uow.ScalarAsync<long>("SELECT count(*) FROM forge.forge_record WHERE account_id=@a AND created_at >= @f AND created_at < @t", ct, ("a", acc), ("f", from), ("t", to));
        if (used >= limit) throw new DomainException(ErrorCodes.ForgeDailyLimit, $"Daily forge limit of {limit} reached", 429, new { limit, used });

        var cards = await collection.LockAsync(ids, ct);
        foreach (var id in ids)
        {
            var c = cards.FirstOrDefault(x => x.Id == id);
            if (c is null || c.OwnerId != acc) throw new DomainException(ErrorCodes.CardNotOwned, "Card not found in your collection", 404);
            switch (c.State)
            {
                case "Burned": throw DomainException.Conflict(ErrorCodes.CardNotAvailable, "Card is no longer available");
                case "Listed" or "InAuction" or "Locked" or "InMatch": throw DomainException.Conflict(ErrorCodes.CardLocked, "Card is locked");
                case "Withdrawing" or "InWallet": throw DomainException.Conflict(ErrorCodes.CardNotInAccount, "Card is not in your account");
            }
            if (c.Soulbound) throw new DomainException(ErrorCodes.CardNotForgeable, "Account-bound cards cannot be forged", 403);
        }

        var asCoin = feeCurrency == Currencies.Coin;
        var fee = await economy.GetAsync(asCoin ? EconomyKeys.ForgeFeeCoin : EconomyKeys.ForgeFeeGem, ct);
        var sealedId = Guid.NewGuid();
        await wallet.DebitAsync(acc, feeCurrency!, fee, "FORGE_FEE", "forge", sealedId.ToString(), $"forge:{idemKey}", ct);   // thiếu tiền → rollback, thẻ vẫn Owned (SC-FRG-03)
        await collection.BurnAsync(ids, ct);
        foreach (var c in cards) await catalog.MarkBurnedAsync(c.CardDefinitionId, ct);
        var odds = await catalog.GetActiveOddsAsync("forge", ct);
        await uow.ExecAsync("INSERT INTO forge.sealed_card(id,account_id,forge_odds_version_id,status,created_at) VALUES(@id,@a,@o,'Sealed',@now)", ct, ("id", sealedId), ("a", acc), ("o", odds.Id), ("now", clock.UtcNow));
        await uow.ExecAsync("INSERT INTO forge.forge_record(id,account_id,input_card_ids,fee_currency,fee_amount,sealed_card_id,created_at) VALUES(@id,@a,@ids,@c,@f,@s,@now)", ct,
            ("id", Guid.NewGuid()), ("a", acc), ("ids", ids), ("c", feeCurrency), ("f", fee), ("s", sealedId), ("now", clock.UtcNow));
        return new ForgeResult(sealedId, feeCurrency!, fee, await wallet.GetBalancesAsync(acc, ct));
    }, ct);

    public async Task<IReadOnlyList<SealedCardDto>> ListSealedAsync(Guid acc, CancellationToken ct) =>
        await uow.QueryAsync("SELECT id, created_at FROM forge.sealed_card WHERE account_id=@a AND status='Sealed' ORDER BY created_at, id", r => new SealedCardDto(r.GetGuid(0), r.GetFieldValue<DateTimeOffset>(1)), ct, ("a", acc));

    // ---------- Lật thẻ chưa lật (BR-FRG-03: không pity, không hệ số nào khác) ----------
    public Task<RevealResult> RevealAsync(Guid acc, Guid sealedId, string? locale, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var row = (await uow.QueryAsync("SELECT account_id, status, forge_odds_version_id FROM forge.sealed_card WHERE id=@i FOR UPDATE",
            r => (Owner: r.GetGuid(0), Status: r.GetString(1), Odds: r.GetInt32(2)), ct, ("i", sealedId))).FirstOrDefault();
        if (row.Status is null || row.Owner != acc) throw DomainException.NotFound("Sealed card");
        if (row.Status != "Sealed") throw DomainException.Conflict(ErrorCodes.SealedCardAlreadyRevealed, "This sealed card was already revealed");
        var info = await identity.GetAsync(acc, ct);
        var loc = Locales.Normalize(locale ?? info.Locale);

        var odds = await catalog.GetOddsAsync(row.Odds, ct);
        var table = odds.Entries.OrderBy(e => Rarities.Index(e.Rarity)).Select(e => (e.Rarity, e.Ppm)).ToList();
        var seed = await fairness.BeginRollAsync(acc, ct);
        var roll = seed.Roll(0);
        var rarity = FairnessMath.Pick(roll, table);
        var issued = await issuer.IssueAsync(acc, seed, 0, rarity, roll, false, "FORGE", sealedId.ToString(), null, ct);
        await uow.ExecAsync("UPDATE forge.sealed_card SET status='Revealed', result_card_instance_id=@c, seed_id=@s, nonce=@n, revealed_at=@now WHERE id=@i", ct,
            ("c", issued.Instance.Id), ("s", seed.SeedId), ("n", seed.Nonce), ("now", clock.UtcNow), ("i", sealedId));
        var card = new OpenedCard(issued.Instance.Id, issued.Instance.Serial, $"#{issued.Instance.EditionNo}/{issued.Definition.MaxSupply}", issued.Instance.EditionNo, issued.Definition.Rarity, false,
            await catalog.ToPublicAsync(issued.Definition, loc, true, ct));
        return new RevealResult(sealedId, card, new { seedHash = seed.SeedHash, clientSeed = seed.ClientSeed, nonce = seed.Nonce, rarityRoll = roll, cardRoll = issued.Trace.CardRoll, issued.Trace.Candidates });
    }, ct);

    public async Task<object> InfoAsync(Guid acc, CancellationToken ct)
    {
        var info = await identity.GetAsync(acc, ct);
        var (from, to) = TimeZones.LocalDayRange(clock.UtcNow, info.Timezone);
        var used = await uow.ScalarAsync<long>("SELECT count(*) FROM forge.forge_record WHERE account_id=@a AND created_at >= @f AND created_at < @t", ct, ("a", acc), ("f", from), ("t", to));
        var odds = await catalog.GetActiveOddsAsync("forge", ct);
        return new
        {
            feeCoin = await economy.GetAsync(EconomyKeys.ForgeFeeCoin, ct),
            feeGem = await economy.GetAsync(EconomyKeys.ForgeFeeGem, ct),
            dailyLimit = await economy.GetAsync(info.PhoneVerified ? EconomyKeys.ForgeDailyLimitVerified : EconomyKeys.ForgeDailyLimitUnverified, ct),
            usedToday = used,
            odds = new { odds.Version, entries = odds.Entries },
        };
    }
}
