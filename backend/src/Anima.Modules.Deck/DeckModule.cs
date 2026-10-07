using System.Security.Claims;
using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Deck;

/// <summary>Thông tin một lá trong bộ, đủ để kiểm luật (BR-DECK-01 → 03).</summary>
public sealed record DeckCard(Guid InstanceId, int CardDefinitionId, string CardType, string Rarity);

public sealed record DeckViolation(string Rule, string Message);

/// <summary>Luật bộ bài thuần (không phụ thuộc DB) để dùng lại khi vào trận (BR-DECK-07) và test.</summary>
public static class DeckRules
{
    public const int Size = 30, MinAnima = 24, MaxSupport = 6, MaxCopies = 2, MaxEpic = 4, MaxLegendary = 2, MaxSecret = 1, MaxDecks = 10;

    public static IReadOnlyList<DeckViolation> Validate(IReadOnlyCollection<DeckCard> cards)
    {
        var v = new List<DeckViolation>();
        if (cards.Count != Size) v.Add(new("DECK_SIZE", $"A deck needs exactly {Size} cards (has {cards.Count})"));
        var anima = cards.Count(c => c.CardType == CardTypes.Anima);
        var support = cards.Count - anima;
        if (anima < MinAnima) v.Add(new("DECK_ANIMA_MIN", $"At least {MinAnima} Anima are required (has {anima})"));
        if (support > MaxSupport) v.Add(new("DECK_SUPPORT_MAX", $"At most {MaxSupport} Echo/Sealed Memory cards are allowed (has {support})"));
        foreach (var g in cards.GroupBy(c => c.CardDefinitionId))
        {
            var cap = g.First().Rarity is "legendary" or "secret" ? 1 : MaxCopies;     // BR-DECK-02
            if (g.Count() > cap) v.Add(new("DECK_COPY_LIMIT", $"Card {g.Key} appears {g.Count()} times (max {cap})"));
        }
        void Cap(string rarity, int max) { var n = cards.Count(c => c.Rarity == rarity); if (n > max) v.Add(new("DECK_RARITY_LIMIT", $"At most {max} {rarity} cards are allowed (has {n})")); }
        Cap("epic", MaxEpic); Cap("legendary", MaxLegendary); Cap("secret", MaxSecret);   // BR-DECK-03
        return v;
    }
}

public sealed record DeckDto(Guid Id, string Name, bool IsDefault, bool Valid, IReadOnlyList<DeckViolation> Violations, IReadOnlyList<Guid> CardInstanceIds, int Count, DateTimeOffset UpdatedAt);
public sealed record DeckBody(string? Name, List<Guid>? CardInstanceIds);

public interface IDeckApi
{
    /// <summary>BR-DECK-07: kiểm lại toàn bộ luật và quyền sở hữu; ném DECK_INVALID nếu bộ không dùng được để vào trận. Trả về các lá của bộ.</summary>
    Task<IReadOnlyList<DeckCard>> RequireBattleReadyAsync(Guid accountId, Guid deckId, CancellationToken ct);
}

public sealed class DeckModule : IModule
{
    public string Name => "deck";
    public System.Reflection.Assembly MigrationAssembly => typeof(DeckModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<DeckService>();
        s.AddScoped<IDeckApi>(sp => sp.GetRequiredService<DeckService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/decks").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, DeckService s, CancellationToken ct) => Results.Ok(await s.ListAsync(u.AccountId(), ct)));
        g.MapPost("", async (ClaimsPrincipal u, DeckBody b, DeckService s, CancellationToken ct) => Results.Created("/v1/decks", await s.CreateAsync(u.AccountId(), b, ct)));
        g.MapGet("/{id:guid}", async (ClaimsPrincipal u, Guid id, DeckService s, CancellationToken ct) => Results.Ok(await s.GetAsync(u.AccountId(), id, ct)));
        g.MapPut("/{id:guid}", async (ClaimsPrincipal u, Guid id, DeckBody b, DeckService s, CancellationToken ct) => Results.Ok(await s.UpdateAsync(u.AccountId(), id, b, ct)));
        g.MapDelete("/{id:guid}", async (ClaimsPrincipal u, Guid id, DeckService s, CancellationToken ct) => { await s.DeleteAsync(u.AccountId(), id, ct); return Results.NoContent(); });
        g.MapPost("/{id:guid}/default", async (ClaimsPrincipal u, Guid id, DeckService s, CancellationToken ct) => Results.Ok(await s.SetDefaultAsync(u.AccountId(), id, ct)));
    }
}

public sealed class DeckService(IUnitOfWork uow, IClock clock, ICollectionApi collection, ICatalogApi catalog) : IDeckApi
{
    private async Task<(Guid Id, string Name, bool IsDefault, DateTimeOffset UpdatedAt)> RequireDeck(Guid acc, Guid id, CancellationToken ct)
    {
        var rows = await uow.QueryAsync("SELECT id,name,is_default,updated_at FROM deck.deck WHERE id=@i AND account_id=@a",
            r => (Id: r.GetGuid(0), Name: r.GetString(1), IsDefault: r.GetBoolean(2), UpdatedAt: r.GetFieldValue<DateTimeOffset>(3)), ct, ("i", id), ("a", acc));
        return rows.Count > 0 ? rows[0] : throw DomainException.NotFound("Deck");
    }

    /// <summary>
    /// Nạp các lá của bộ. BR-DECK-06: Instance không còn Owned của chính chủ (bán, rèn, khóa...) bị gỡ khỏi bộ — thực hiện khi đọc,
    /// nên mọi đường làm thẻ rời trạng thái Owned đều được phủ mà không cần từng module phát sự kiện.
    /// </summary>
    private async Task<List<DeckCard>> LoadAndPruneAsync(Guid acc, Guid deckId, CancellationToken ct)
    {
        var ids = await uow.QueryAsync("SELECT instance_id FROM deck.deck_card WHERE deck_id=@d", r => r.GetGuid(0), ct, ("d", deckId));
        if (ids.Count == 0) return [];
        var inst = (await collection.GetManyAsync(ids, ct)).ToDictionary(i => i.Id);
        var gone = ids.Where(i => !inst.TryGetValue(i, out var c) || c.OwnerId != acc || c.State != "Owned").ToList();
        if (gone.Count > 0) await uow.ExecAsync("DELETE FROM deck.deck_card WHERE deck_id=@d AND instance_id = ANY(@g)", ct, ("d", deckId), ("g", gone.ToArray()));
        var defs = (await catalog.ListCardsAsync(ct)).ToDictionary(d => d.Id);
        return ids.Except(gone).Select(i => new DeckCard(i, inst[i].CardDefinitionId, defs[inst[i].CardDefinitionId].CardType, defs[inst[i].CardDefinitionId].Rarity)).ToList();
    }

    private async Task<DeckDto> ToDto(Guid acc, (Guid Id, string Name, bool IsDefault, DateTimeOffset UpdatedAt) d, CancellationToken ct)
    {
        var cards = await LoadAndPruneAsync(acc, d.Id, ct);
        var violations = DeckRules.Validate(cards);
        return new DeckDto(d.Id, d.Name, d.IsDefault, violations.Count == 0, violations, cards.Select(c => c.InstanceId).ToList(), cards.Count, d.UpdatedAt);
    }

    public async Task<IReadOnlyList<DeckDto>> ListAsync(Guid acc, CancellationToken ct)
    {
        var rows = await uow.QueryAsync("SELECT id,name,is_default,updated_at FROM deck.deck WHERE account_id=@a ORDER BY created_at, id", r => (r.GetGuid(0), r.GetString(1), r.GetBoolean(2), r.GetFieldValue<DateTimeOffset>(3)), ct, ("a", acc));
        var list = new List<DeckDto>();
        foreach (var d in rows) list.Add(await ToDto(acc, d, ct));
        return list;
    }

    public async Task<DeckDto> GetAsync(Guid acc, Guid id, CancellationToken ct) => await ToDto(acc, await RequireDeck(acc, id, ct), ct);

    private static string CleanName(string? name)
    {
        var n = (name ?? "").Trim();
        return n.Length is >= 1 and <= 40 ? n : throw DomainException.Validation("Deck name must be 1 to 40 characters");
    }

    /// <summary>Lưu bộ chưa đủ 30 lá được (đang soạn dở), nhưng mỗi lá phải là Instance Owned của chính mình, không trùng, tối đa 30 (BR-DECK-04).</summary>
    private async Task SaveCardsAsync(Guid acc, Guid deckId, List<Guid>? ids, CancellationToken ct)
    {
        ids ??= [];
        if (ids.Count > DeckRules.Size) throw DomainException.Validation($"A deck holds at most {DeckRules.Size} cards");
        if (ids.Distinct().Count() != ids.Count) throw DomainException.Validation("The same card cannot be added twice to a deck");
        var found = (await collection.GetManyAsync(ids, ct)).ToDictionary(i => i.Id);
        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var c) || c.OwnerId != acc) throw new DomainException(ErrorCodes.CardNotInAccount, "A card is not in your account", 403);
            if (c.State != "Owned") throw new DomainException(ErrorCodes.CardLocked, "Only cards you own and have not listed or locked can be used", 409);
        }
        await uow.ExecAsync("DELETE FROM deck.deck_card WHERE deck_id=@d", ct, ("d", deckId));
        foreach (var id in ids) await uow.ExecAsync("INSERT INTO deck.deck_card(deck_id,instance_id) VALUES(@d,@i)", ct, ("d", deckId), ("i", id));
    }

    public Task<DeckDto> CreateAsync(Guid acc, DeckBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var name = CleanName(b.Name);
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "deck:" + acc));
        if (await uow.ScalarAsync<long>("SELECT count(*) FROM deck.deck WHERE account_id=@a", ct, ("a", acc)) >= DeckRules.MaxDecks)
            throw DomainException.Conflict(ErrorCodes.DeckLimitReached, $"You can save at most {DeckRules.MaxDecks} decks");
        var id = Guid.NewGuid();
        await uow.ExecAsync("INSERT INTO deck.deck(id,account_id,name,created_at,updated_at) VALUES(@i,@a,@n,@now,@now)", ct, ("i", id), ("a", acc), ("n", name), ("now", clock.UtcNow));
        await SaveCardsAsync(acc, id, b.CardInstanceIds, ct);
        return await GetAsync(acc, id, ct);
    }, ct);

    public Task<DeckDto> UpdateAsync(Guid acc, Guid id, DeckBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        await RequireDeck(acc, id, ct);
        var name = CleanName(b.Name);
        await uow.ExecAsync("UPDATE deck.deck SET name=@n, updated_at=@now WHERE id=@i", ct, ("n", name), ("now", clock.UtcNow), ("i", id));
        if (b.CardInstanceIds is not null) await SaveCardsAsync(acc, id, b.CardInstanceIds, ct);
        return await GetAsync(acc, id, ct);
    }, ct);

    public Task DeleteAsync(Guid acc, Guid id, CancellationToken ct) => uow.RunAsync(async () =>
    {
        await RequireDeck(acc, id, ct);
        await uow.ExecAsync("DELETE FROM deck.deck WHERE id=@i", ct, ("i", id));
    }, ct);

    public Task<DeckDto> SetDefaultAsync(Guid acc, Guid id, CancellationToken ct) => uow.RunAsync(async () =>
    {
        await RequireDeck(acc, id, ct);
        await uow.ExecAsync("UPDATE deck.deck SET is_default=false WHERE account_id=@a AND is_default", ct, ("a", acc));
        await uow.ExecAsync("UPDATE deck.deck SET is_default=true WHERE id=@i", ct, ("i", id));
        return await GetAsync(acc, id, ct);
    }, ct);

    public async Task<IReadOnlyList<DeckCard>> RequireBattleReadyAsync(Guid acc, Guid deckId, CancellationToken ct)
    {
        await RequireDeck(acc, deckId, ct);
        var cards = await LoadAndPruneAsync(acc, deckId, ct);
        var violations = DeckRules.Validate(cards);
        if (violations.Count > 0) throw new DomainException(ErrorCodes.DeckInvalid, "This deck cannot enter a match", 409, new { violations });
        return cards;
    }
}
