using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Tách lớp riêng: test này ngừng phát hành gần hết thẻ Common nên cần database riêng.</summary>
public sealed class BasicPackTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    [Fact(DisplayName = "SC-NEW-06: pack cơ bản không cho ra bản thứ 3 của một thẻ gắn chặt (chỉ còn 3 thẻ Common khả dụng)")]
    public async Task BasicPackAvoidsThirdCopy()
    {
        var p = await fx.NewPlayer();
        await fx.Sql(@"UPDATE catalog.card_definition SET discontinued=true WHERE rarity='common' AND card_type='anima'
            AND id NOT IN (SELECT id FROM catalog.card_definition WHERE rarity='common' AND card_type='anima' ORDER BY id LIMIT 3)");
        var packId = Guid.NewGuid();
        await fx.Sql("INSERT INTO gacha.pack_instance(id,account_id,pack_code,kind,status,soulbound) VALUES(@i,@a,'basic','basic','Unopened',true)", ("i", packId), ("a", p.Id));
        var o = await p.Open(packId); Assert.True(o.Ok, o.Body.ToString());
        var byCard = o["cards"].EnumerateArray().GroupBy(c => c.GetProperty("card").GetProperty("id").GetInt32()).ToList();
        Assert.Equal(5, o["cards"].GetArrayLength());
        Assert.All(byCard, g => Assert.True(g.Count() <= 2));
        Assert.All(o["cards"].EnumerateArray(), c => Assert.True(c.GetProperty("soulbound").GetBoolean()));
    }
}
