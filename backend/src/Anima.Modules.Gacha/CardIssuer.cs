using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.Fairness;
using Anima.SharedKernel;

namespace Anima.Gacha;

public sealed record IssueTrace(int Slot, string RarityRequested, string RarityIssued, int RarityRoll, int CardRoll, int Candidates, int Index, int Retries, string? Note);
public sealed record IssuedCard(CardInstance Instance, CardDefinition Definition, IssueTrace Trace);

/// <summary>
/// Cấp một thẻ theo rarity đã quay (SAD 8.2 bước 4–5): lấy Card Definition còn bản, chọn bằng giá trị quay ":c",
/// tăng <c>issued</c> nguyên tử rồi tạo Card Instance có số thứ tự và serial. Dùng chung cho mở pack và lật thẻ rèn.
/// </summary>
public interface ICardIssuer
{
    Task<IssuedCard> IssueAsync(Guid accountId, RollSession seed, int slot, string rarity, int rarityRoll, bool soulbound, string originType, string? originId,
        string? packCodeForOffSale, CancellationToken ct, string? cardType = null, string? element = null);
}

internal sealed class CardIssuer(ICatalogApi catalog, ICollectionApi collection) : ICardIssuer
{
    private const int MaxRetries = 8;

    public async Task<IssuedCard> IssueAsync(Guid accountId, RollSession seed, int slot, string rarity, int rarityRoll, bool soulbound, string originType, string? originId,
        string? packCodeForOffSale, CancellationToken ct, string? cardType = null, string? element = null)
    {
        var effective = rarity; string? note = null;
        for (var retry = 0; retry <= MaxRetries; retry++)
        {
            var candidates = await catalog.AvailableCardIdsAsync(effective, ct, cardType, element);
            if (candidates.Count == 0)
            {
                // BR-SUP-04: hết bản của rarity → ngừng bán pack, KHÔNG tự hạ tỷ lệ ngầm. Lần mở đang chạy dùng rarity thấp hơn gần nhất
                // còn bản và ghi rõ lý do vào bản ghi (SAD 8.2 bước 5; chờ PO xác nhận — Q-39).
                if (packCodeForOffSale is not null) await catalog.SetPackOffSaleAsync(packCodeForOffSale, $"RARITY_EXHAUSTED:{effective}", ct);
                var idx = Rarities.Index(effective) - 1;
                if (idx < 0 || cardType is not null) throw new DomainException(ErrorCodes.PackSupplyExhausted, "No card supply is left for this pack", 409);
                effective = Rarities.Order[idx];
                note = $"DOWNGRADED_FROM:{rarity}";
                retry--;
                continue;
            }
            var roll = seed.RollCard(slot, retry);
            var pick = roll % candidates.Count;
            var edition = await catalog.TryIssueAsync(candidates[pick], ct);
            if (edition is null) continue;                        // vừa hết bản do đồng thời → chọn lại với danh sách mới
            var def = (await catalog.GetCardAsync(candidates[pick], ct))!;
            var inst = await collection.GrantAsync(accountId, def.Id, edition.Value, soulbound, originType, originId, ct);
            return new IssuedCard(inst, def, new IssueTrace(slot, rarity, effective, rarityRoll, roll, candidates.Count, pick, retry, note));
        }
        throw new DomainException(ErrorCodes.PackSupplyExhausted, "Could not allocate a card, please retry", 409);
    }
}
