using System.Net;

namespace Anima.IntegrationTests;

public sealed class CollectionTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    [Fact(DisplayName = "US-05.2: tiến độ set là số Card Definition khác nhau, bản trùng không cộng")]
    public async Task ProgressCountsDistinctDefinitions()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(3);
        var items = await p.Collection();
        var distinct = items.Select(i => i.GetProperty("card").GetProperty("id").GetInt32()).Distinct().Count();
        var prog = await p.Get("/v1/collection/progress");
        Assert.Equal(distinct, prog["owned"].GetInt32());
        Assert.Equal(100, prog["total"].GetInt32());
        Assert.True(items.Count >= distinct);
    }

    [Fact(DisplayName = "US-05.3: lọc theo hệ, rarity, loại thẻ và tên")]
    public async Task Filters()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(4);
        var all = await p.Collection();
        var first = all[0].GetProperty("card");
        var el = first.GetProperty("element").GetString();
        var byEl = (await p.Get($"/v1/collection?element={el}"))["items"].EnumerateArray().ToList();
        Assert.NotEmpty(byEl);
        Assert.All(byEl, i => Assert.Equal(el, i.GetProperty("card").GetProperty("element").GetString()));
        var name = first.GetProperty("name").GetString()!;
        Assert.All((await p.Get($"/v1/collection?q={name[..3].ToLowerInvariant()}"))["items"].EnumerateArray(), i => Assert.Contains(name[..3], i.GetProperty("card").GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "US-05.4: chỉ thẻ đã sở hữu mới đọc được Story Fragment")]
    public async Task StoryOnlyForDiscoveredCards()
    {
        var p = await fx.NewPlayer();
        var owned = (await p.BuyAndOpen(1))[0].GetProperty("card").GetProperty("id").GetInt32();
        var ownedCard = await p.Get($"/v1/cards/{owned}");
        Assert.False(string.IsNullOrEmpty(ownedCard["story"].GetString()));

        var mine = (await p.Collection()).Select(i => i.GetProperty("card").GetProperty("id").GetInt32()).ToHashSet();
        var notOwned = Enumerable.Range(1, 100).First(i => !mine.Contains(i));
        var locked = await p.Get($"/v1/cards/{notOwned}");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, locked["story"].ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await p.Get("/v1/cards/9999")).Status);
    }

    [Theory(DisplayName = "BR-I18N-01: Story Fragment trả theo ngôn ngữ yêu cầu")]
    [InlineData("zh-Hans", @"\p{IsCJKUnifiedIdeographs}")]
    [InlineData("zh-Hant", @"\p{IsCJKUnifiedIdeographs}")]
    [InlineData("vi", @"[ăâđêôơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]")]
    public async Task StoryIsLocalized(string locale, string pattern)
    {
        var p = await fx.NewPlayer();
        var id = (await p.BuyAndOpen(1))[0].GetProperty("card").GetProperty("id").GetInt32();
        var story = (await p.Get($"/v1/cards/{id}?locale={locale}"))["story"].GetString()!;
        Assert.Matches(pattern, story);
    }

    [Fact(DisplayName = "Mỗi người chơi chỉ thấy thẻ của mình")]
    public async Task IsolatedCollections()
    {
        var a = await fx.NewPlayer(); var b = await fx.NewPlayer();
        await a.BuyAndOpen(1);
        Assert.Equal(0, (await b.Get("/v1/collection"))["total"].GetInt32());
    }
}
