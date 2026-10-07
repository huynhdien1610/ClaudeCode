using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Catalog;

/// <summary>
/// Dữ liệu tạm cho set "Awakening" (task T042 / T125): 100 thẻ = 80 Anima + 12 Tiếng vọng + 8 Ký ức phong ấn (BR-CARD-07).
/// Chỉ số theo khung chi phí BR-CARD-03; kỹ năng, mạch truyện và nội dung truyện là PLACEHOLDER chờ Game Designer và Content.
/// Chạy một lần (bỏ qua nếu mùa S1 đã tồn tại). Chỉ bật khi cấu hình Seed:Enabled.
/// </summary>
internal sealed class CatalogSeeder(IUnitOfWork uow) : IModuleSeeder
{
    public const string Season = "S1";
    private sealed record Def(string Name, string Element, string Rarity, string Type, int Cost, string Style = "bal", string? Skill = null, string? Arc = null);

    // Khung chi phí → (ATK, DEF, HP). ATK + DEF + HP/2 ≈ 600 × chi phí + 300 (BR-CARD-03).
    private static readonly Dictionary<int, (int Atk, int Def, int Hp)> Base = new()
    { [1] = (400, 200, 600), [2] = (700, 300, 1000), [3] = (1000, 400, 1400), [4] = (1300, 500, 1800), [5] = (1500, 700, 2200), [6] = (1800, 800, 2600) };
    private static (int, int, int) Offset(string style) => style switch { "atk" => (150, -100, -100), "def" => (-150, 100, 100), _ => (0, 0, 0) };

    // Mỗi hệ 10 Anima theo thứ tự rarity: C C C C U U R R E L (Nihilum: ... E S).
    private static readonly string[] Rarity10 = ["common", "common", "common", "common", "uncommon", "uncommon", "rare", "rare", "epic", "legendary"];
    private static readonly int[] Cost10 = [1, 2, 2, 3, 3, 4, 4, 4, 5, 6];
    private static readonly string[] Style10 = ["bal", "atk", "def", "bal", "atk", "def", "bal", "atk", "def", "bal"];

    private static readonly (string Element, string[] Names)[] AnimaNames =
    [
        ("Luminara", ["Glimmerkin", "Dawnpetal", "Warmhush", "Solace Wisp", "Brightling", "Sunmote", "Hopeweaver", "Morningsong", "Radiantide", "Seraphel"]),
        ("Umbryx", ["Hollowmoth", "Duskveil", "Quietshade", "Lonely Lantern", "Murkling", "Nightbloom", "Whisperghast", "Shadowvigil", "Nocturne", "Veilmother"]),
        ("Pyraxis", ["Cinderpup", "Flarekin", "Ashtongue", "Blazeheart", "Emberling", "Sparkmane", "Furnacehound", "Scorchwing", "Emberfang", "Pyreclaw"]),
        ("Aqualis", ["Driftkoi", "Rainmemory", "Tidelet", "Mistwhisper", "Brinesong", "Dewdrop Sage", "Harborlight", "Seafoam Wraith", "Moonwake", "Tidemourn"]),
        ("Terrakin", ["Pebbleguard", "Rootbound", "Mossback", "Cragling", "Stonewarden", "Clayheart", "Stoneward", "Bouldersage", "Mountainroot", "Gaiawarden"]),
        ("Ventara", ["Breezeling", "Kitewing", "Wanderseed", "Gustkit", "Skydancer", "Cloudhopper", "Zephyrion", "Hilltop Dreamer", "Stormcaller", "Horizon Walker"]),
        ("Voltaris", ["Sparkit", "Jolt Imp", "Static Fox", "Arcling", "Thunderpup", "Flashwing", "Voltmane", "Stormspark", "Surgeflux", "Tempest Sovereign"]),
        ("Nihilum", ["Void Echo", "Empty Bell", "Hollow Choir", "Silent Static", "Blankmask", "Faded Page", "Grayfather", "Ashen Mirror", "Nullwalker", "The Nameless"]),
    ];

    private static readonly Dictionary<string, (string Skill, string? Arc)> Specials = new()
    {
        ["Seraphel"] = ("heal", null),
        ["Zephyrion"] = ("rush", null),
        ["Tidemourn"] = ("wave", "harbor"),
        ["Quietshade"] = ("", "weeper"),
        ["Nocturne"] = ("", "weeper"),
        ["Tidelet"] = ("", "harbor"),
    };

    // (tên, hệ, rarity, loại)
    private static readonly (string, string, string, string)[] Support =
    [
        ("Dawnsong", "Luminara", "common", "echo"), ("Veilsong", "Umbryx", "common", "echo"), ("Cinderword", "Pyraxis", "common", "echo"), ("Tidesong", "Aqualis", "common", "echo"),
        ("Rootword", "Terrakin", "uncommon", "echo"), ("Galeword", "Ventara", "uncommon", "echo"), ("Sparkchant", "Voltaris", "uncommon", "echo"), ("Hollowhymn", "Nihilum", "uncommon", "echo"),
        ("Hymn of Sunrise", "Luminara", "rare", "echo"), ("Lullaby of Tides", "Aqualis", "rare", "echo"), ("Chorus of Storms", "Voltaris", "rare", "echo"), ("Requiem of Tears", "Umbryx", "epic", "echo"),
        ("Unspoken Vow", "Umbryx", "common", "seal"), ("Smoldering Grudge", "Pyraxis", "common", "seal"), ("Stone Promise", "Terrakin", "common", "seal"),
        ("Sunken Letter", "Aqualis", "uncommon", "seal"), ("Wind-Locked Wish", "Ventara", "uncommon", "seal"),
        ("Snapped Wire", "Voltaris", "rare", "seal"), ("Lantern Memory", "Luminara", "rare", "seal"), ("Memory of Nothing", "Nihilum", "epic", "seal"),
    ];

    private static readonly Dictionary<string, int> MaxSupply = new() { ["common"] = 50_000, ["uncommon"] = 20_000, ["rare"] = 5_000, ["epic"] = 1_000, ["legendary"] = 300, ["secret"] = 100 };

    private static readonly string[] Locales4 = ["vi", "en", "zh-Hans", "zh-Hant"];
    private static readonly Dictionary<string, string> Generic = new()
    {
        ["vi"] = "Một cảm xúc thường ngày của ai đó, đủ mạnh để kết tinh thành hình hài nhỏ bé này.",
        ["en"] = "An everyday feeling of someone, strong enough to crystallize into this small form.",
        ["zh-Hans"] = "某人日常的一份情感，强烈到足以凝结成这个小小的形体。",
        ["zh-Hant"] = "某人日常的一份情感，強烈到足以凝結成這個小小的形體。",
    };

    // Truyện và tên hiệu của 8 thẻ có tên trong Master Document §7.
    private static readonly Dictionary<string, (string[] Epithet, string[] Story)> Named = new()
    {
        ["Seraphel"] = (["Kẻ mang hy vọng", "the Hopebringer", "带来希望者", "帶來希望者"], [
            "Ta sinh ra từ nụ cười của một đứa trẻ lần đầu nhìn thấy mặt trời sau cơn bão. Ta không nhớ tên đứa trẻ đó. Nhưng ta nhớ cảm giác đó — cảm giác rằng mọi thứ sẽ ổn thôi.",
            "I was born from the smile of a child seeing the sun for the first time after a storm. I don't remember the child's name. But I remember that feeling — the feeling that everything will be all right.",
            "我诞生于一个孩子在暴风雨后第一次看到太阳时的笑容。我不记得那个孩子的名字，但我记得那种感觉——一切都会好起来的感觉。",
            "我誕生於一個孩子在暴風雨後第一次看到太陽時的笑容。我不記得那個孩子的名字，但我記得那種感覺——一切都會好起來的感覺。"]),
        ["Nocturne"] = (["Giọt lệ lặng thinh", "the Silent Tear", "无声之泪", "無聲之淚"], [
            "Ta không khóc. Ta là nước mắt. Ta sinh ra từ người đàn ông đã khóc trong im lặng suốt 20 năm. Ông ấy không cho ai thấy. Nhưng ta thấy. Ta là tất cả những giọt nước mắt ông ấy không rơi.",
            "I do not cry. I am the tears. I was born from a man who wept in silence for twenty years. He let no one see. But I saw. I am every tear he never let fall.",
            "我不哭泣。我就是眼泪。我诞生于一个默默哭泣了二十年的男人。他不让任何人看见，但我看见了。我是他所有未曾落下的泪水。",
            "我不哭泣。我就是眼淚。我誕生於一個默默哭泣了二十年的男人。他不讓任何人看見，但我看見了。我是他所有未曾落下的淚水。"]),
        ["Emberfang"] = (["Kẻ bị cơn giận trói buộc", "the Ragebound", "愤怒的束缚者", "憤怒的束縛者"], [
            "Ta là cơn giận chưa từng được nói ra. Ta trung thành với người đã sinh ra ta, kể cả khi người đó muốn quên ta đi.",
            "I am the anger that was never spoken. I stay loyal to the one who made me, even when they want to forget me.",
            "我是从未说出口的愤怒。即使创造我的人想要忘记我，我依然忠于他。",
            "我是從未說出口的憤怒。即使創造我的人想要忘記我，我依然忠於他。"]),
        ["Tidemourn"] = (["Nỗi buồn sâu thẳm", "the Deep Sorrow", "深海之悲", "深海之悲"], [
            "Ta mang theo tiếng sóng của một bến cảng không còn ai chờ. Ta nhớ từng con tàu không trở về.",
            "I carry the sound of waves from a harbor where no one waits anymore. I remember every ship that never came home.",
            "我带着一个再也无人等待的港口的浪声。我记得每一艘没有归来的船。",
            "我帶著一個再也無人等待的港口的浪聲。我記得每一艘沒有歸來的船。"]),
        ["Stoneward"] = (["Kẻ bất khuất", "the Unyielding", "不屈者", "不屈者"], [
            "Ta được tạo nên từ một lời hứa không bao giờ bị phá vỡ.", "I was made from a promise that was never broken.", "我由一个从未被打破的承诺铸成。", "我由一個從未被打破的承諾鑄成。"]),
        ["Zephyrion"] = (["Ngọn gió tự do", "the Freewind", "自由之风", "自由之風"], [
            "Ta là câu hỏi “nếu như” của một đứa trẻ đứng trên đỉnh đồi.", "I am the \"what if\" of a child standing on a hilltop.", "我是一个站在山顶的孩子心中的“如果”。", "我是一個站在山頂的孩子心中的「如果」。"]),
        ["Surgeflux"] = (["Khoảnh khắc sáng tỏ", "the Revelation", "顿悟", "頓悟"], [
            "Ta là khoảnh khắc mọi thứ bỗng trở nên rõ ràng — rồi vỡ tung.", "I am the moment everything suddenly becomes clear — and then shatters.", "我是一切忽然变得清晰——然后破碎的那一刻。", "我是一切忽然變得清晰——然後破碎的那一刻。"]),
        ["The Nameless"] = ([null!, null!, null!, null!], ["Mẹ... con đang ở đây...", "Mother... I'm here...", "妈妈……我在这里……", "媽媽……我在這裡……"]),
    };

    public async Task SeedAsync(CancellationToken ct)
    {
        await uow.RunAsync(async () =>
        {
            if (await uow.ScalarAsync<int?>("SELECT 1 FROM catalog.season WHERE id=@s", ct, ("s", Season)) is not null) return;
            await uow.ExecAsync("INSERT INTO catalog.season(id,name,status,opened_at) VALUES(@s,'Awakening','open',now())", ct, ("s", Season));

            var id = 0;
            var all = new List<Def>();
            foreach (var (el, names) in AnimaNames)
                for (var i = 0; i < 10; i++)
                {
                    var rarity = el == "Nihilum" && i == 9 ? "secret" : Rarity10[i];
                    var sp = Specials.GetValueOrDefault(names[i]);
                    all.Add(new Def(names[i], el, rarity, CardTypes.Anima, Cost10[i], Style10[i], string.IsNullOrEmpty(sp.Skill) ? null : sp.Skill, sp.Arc));
                }
            foreach (var (n, el, r, t) in Support)
                all.Add(new Def(n, el, r, t, r switch { "rare" => 2, "epic" => 3, _ => 1 }));

            foreach (var d in all)
            {
                id++;
                int? atk = null, def = null, hp = null;
                if (d.Type == CardTypes.Anima)
                {
                    var b = Base[d.Cost]; var (oa, od, oh) = Offset(d.Style);
                    (atk, def, hp) = (b.Atk + oa, b.Def + od, b.Hp + oh);
                }
                await uow.ExecAsync(@"INSERT INTO catalog.card_definition(id,code,name,season_id,element,rarity,card_type,resonance_cost,atk,def,hp,skill_id,arc_id,published)
                    VALUES(@id,@code,@n,@s,@el,@r,@t,@c,@a,@d,@h,@sk,@arc,true)", ct,
                    ("id", id), ("code", $"AWK-{id:000}"), ("n", d.Name), ("s", Season), ("el", d.Element), ("r", d.Rarity), ("t", d.Type), ("c", d.Cost),
                    ("a", atk), ("d", def), ("h", hp), ("sk", d.Skill), ("arc", d.Arc));
                await uow.ExecAsync("INSERT INTO catalog.edition(card_definition_id,season_id,max_supply) VALUES(@id,@s,@m)", ct, ("id", id), ("s", Season), ("m", MaxSupply[d.Rarity]));
                var named = Named.GetValueOrDefault(d.Name);
                for (var li = 0; li < 4; li++)
                    await uow.ExecAsync("INSERT INTO catalog.card_translation(card_definition_id,locale,epithet,story) VALUES(@id,@l,@e,@st)", ct,
                        ("id", id), ("l", Locales4[li]), ("e", named.Epithet?[li]), ("st", named.Story?[li] ?? Generic[Locales4[li]]));
            }

            // Pack: Standard (bán), Welcome (cấp khi đăng ký), Forge (tỷ lệ lật thẻ rèn). Tỷ lệ phương án A của DT-01, chờ PO chốt (CF-01).
            await uow.ExecAsync(@"INSERT INTO catalog.pack_definition(code,name,kind,price_coin,price_gem,cards_per_pack,on_sale,season_id) VALUES
                ('awakening-standard','Awakening Standard','standard',1000,100,5,true,@s),
                ('welcome','Welcome Pack','welcome',NULL,NULL,5,false,@s),
                ('forge','Forge','forge',NULL,NULL,1,false,@s)", ct, ("s", Season));
            const string odds = """[{"rarity":"common","ppm":450000},{"rarity":"uncommon","ppm":250000},{"rarity":"rare","ppm":180000},{"rarity":"epic","ppm":70000},{"rarity":"legendary","ppm":40000},{"rarity":"secret","ppm":10000}]""";
            foreach (var code in new[] { "awakening-standard", "forge" })
                await uow.ExecAsync(@"INSERT INTO catalog.odds_version(pack_code,version,status,effective_from,created_by,approved_by,entries)
                    VALUES(@p,1,'active','2026-01-01','system','product-owner',@e::jsonb)", ct, ("p", code), ("e", odds));
        }, ct);
    }
}
