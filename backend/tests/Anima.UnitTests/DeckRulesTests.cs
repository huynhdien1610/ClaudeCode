using Anima.Contracts;
using Anima.Deck;
using Xunit;

namespace Anima.UnitTests;

public class DeckRulesTests
{
    private static DeckCard C(int def, string rarity = "common", string type = CardTypes.Anima) => new(Guid.NewGuid(), def, type, rarity);

    /// <summary>30 lá hợp lệ: 15 thẻ khác nhau × 2 bản, đều Anima Common.</summary>
    private static List<DeckCard> Valid() => Enumerable.Range(1, 15).SelectMany(d => new[] { C(d), C(d) }).ToList();

    private static string[] Rules(IEnumerable<DeckCard> cards) => DeckRules.Validate(cards.ToList()).Select(v => v.Rule).Distinct().ToArray();

    [Fact(DisplayName = "BR-DECK-01: bộ 30 lá, 24+ Anima, tối đa 6 lá hỗ trợ — hợp lệ")]
    public void ValidDeck() => Assert.Empty(Rules(Valid()));

    [Theory(DisplayName = "BR-DECK-01: sai số lượng lá")]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(0)]
    public void WrongSize(int n)
    {
        var cards = Enumerable.Range(1, n).Select(i => C(i)).ToList();
        Assert.Contains("DECK_SIZE", Rules(cards));
    }

    [Fact(DisplayName = "BR-DECK-01: 6 lá hỗ trợ được, 7 lá thì không; dưới 24 Anima thì không")]
    public void SupportLimits()
    {
        List<DeckCard> With(int support) => Enumerable.Range(1, 30 - support).Select(i => C(i)).Concat(Enumerable.Range(100, support).Select(i => C(i, "rare", CardTypes.Echo))).ToList();
        Assert.Empty(Rules(With(6)));
        var seven = Rules(With(7));
        Assert.Contains("DECK_SUPPORT_MAX", seven); Assert.Contains("DECK_ANIMA_MIN", seven);
        Assert.Contains("DECK_ANIMA_MIN", Rules(With(7)));
    }

    [Fact(DisplayName = "BR-DECK-02: tối đa 2 bản mỗi thẻ; Legendary và Secret chỉ 1 bản")]
    public void CopyLimits()
    {
        var three = Enumerable.Range(1, 27).Select(i => C(i)).Concat([C(99), C(99), C(99)]).ToList();
        Assert.Contains("DECK_COPY_LIMIT", Rules(three));
        var twoLegend = Enumerable.Range(1, 28).Select(i => C(i)).Concat([C(900, "legendary"), C(900, "legendary")]).ToList();
        Assert.Contains("DECK_COPY_LIMIT", Rules(twoLegend));
        var twoSecret = Enumerable.Range(1, 28).Select(i => C(i)).Concat([C(901, "secret"), C(901, "secret")]).ToList();
        Assert.Contains("DECK_COPY_LIMIT", Rules(twoSecret));
    }

    [Fact(DisplayName = "BR-DECK-03: tối đa 4 Epic, 2 Legendary, 1 Secret Rare")]
    public void RarityLimits()
    {
        List<DeckCard> Deck(string rarity, int n) => Enumerable.Range(1, 30 - n).Select(i => C(i)).Concat(Enumerable.Range(500, n).Select(i => C(i, rarity))).ToList();
        Assert.Empty(Rules(Deck("epic", 4))); Assert.Contains("DECK_RARITY_LIMIT", Rules(Deck("epic", 5)));
        Assert.Empty(Rules(Deck("legendary", 2))); Assert.Contains("DECK_RARITY_LIMIT", Rules(Deck("legendary", 3)));
        Assert.Empty(Rules(Deck("secret", 1))); Assert.Contains("DECK_RARITY_LIMIT", Rules(Deck("secret", 2)));
    }
}
