/* Dữ liệu giả cho prototype. Không phải dữ liệu thật; tỷ lệ theo BRD DT-01 phương án A (chờ PO chốt). */
window.ANIMA = (function () {
  const ELEMENTS = {
    Luminara: { glyph: "✦", hue: 45, label: "Hy vọng · Niềm vui" },
    Umbryx: { glyph: "☾", hue: 265, label: "Sợ hãi · Cô đơn" },
    Pyraxis: { glyph: "♨", hue: 12, label: "Giận dữ · Đam mê" },
    Aqualis: { glyph: "≈", hue: 200, label: "Nỗi buồn · Hoài niệm" },
    Terrakin: { glyph: "▲", hue: 95, label: "Kiên định · Bền bỉ" },
    Ventara: { glyph: "❋", hue: 165, label: "Tự do · Tò mò" },
    Voltaris: { glyph: "ϟ", hue: 290, label: "Phấn khích · Hỗn loạn" },
    Nihilum: { glyph: "∅", hue: 0, label: "Tuyệt vọng · Trống rỗng" },
  };
  const RARITIES = [
    { key: "common", name: "Common", vi: "Thường", color: "#6b7280", odds: 45, flip: 0.4 },
    { key: "uncommon", name: "Uncommon", vi: "Ít gặp", color: "#15803d", odds: 25, flip: 0.6 },
    { key: "rare", name: "Rare", vi: "Hiếm", color: "#1d4ed8", odds: 18, flip: 1.0 },
    { key: "epic", name: "Epic", vi: "Sử thi", color: "#7e22ce", odds: 7, flip: 1.5 },
    { key: "legendary", name: "Legendary", vi: "Huyền thoại", color: "#b45309", odds: 4, flip: 3.0 },
    { key: "secret", name: "Secret Rare", vi: "Bí ẩn", color: "rainbow", odds: 1, flip: 5.0 },
  ];
  const named = [
    ["Seraphel, the Hopebringer", "Luminara", "legendary", "Ta sinh ra từ nụ cười của một đứa trẻ lần đầu nhìn thấy mặt trời sau cơn bão. Ta không nhớ tên đứa trẻ đó. Nhưng ta nhớ cảm giác đó — cảm giác rằng mọi thứ sẽ ổn thôi."],
    ["Nocturne, the Silent Tear", "Umbryx", "epic", "Ta không khóc. Ta là nước mắt. Ta sinh ra từ người đàn ông đã khóc trong im lặng suốt 20 năm. Ông ấy không cho ai thấy. Nhưng ta thấy. Ta là tất cả những giọt nước mắt ông ấy không rơi."],
    ["Emberfang, the Ragebound", "Pyraxis", "epic", "Ta là cơn giận chưa từng được nói ra. Ta trung thành với người đã sinh ra ta, kể cả khi người đó muốn quên ta đi."],
    ["Tidemourn, the Deep Sorrow", "Aqualis", "legendary", "Ta mang theo tiếng sóng của một bến cảng không còn ai chờ. Ta nhớ từng con tàu không trở về."],
    ["Stoneward, the Unyielding", "Terrakin", "rare", "Ta được tạo nên từ một lời hứa không bao giờ bị phá vỡ."],
    ["Zephyrion, the Freewind", "Ventara", "rare", "Ta là câu hỏi “nếu như” của một đứa trẻ đứng trên đỉnh đồi."],
    ["Surgeflux, the Revelation", "Voltaris", "epic", "Ta là khoảnh khắc mọi thứ bỗng trở nên rõ ràng — rồi vỡ tung."],
    ["The Nameless", "Nihilum", "secret", "Mẹ... con đang ở đây..."],
  ];
  const fillerNames = {
    Luminara: ["Glimmerkin", "Dawnpetal", "Warmhush", "Solace Wisp", "Brightling"],
    Umbryx: ["Hollowmoth", "Duskveil", "Quietshade", "Lonely Lantern", "Murkling"],
    Pyraxis: ["Cinderpup", "Flarekin", "Ashtongue", "Blazeheart"],
    Aqualis: ["Driftkoi", "Rainmemory", "Tidelet", "Mistwhisper"],
    Terrakin: ["Pebbleguard", "Rootbound", "Mossback", "Cragling"],
    Ventara: ["Breezeling", "Kitewing", "Wanderseed", "Gustkit"],
    Voltaris: ["Sparkit", "Jolt Imp", "Static Fox", "Arcling"],
    Nihilum: ["Void Echo", "Empty Bell"],
  };
  const fillerRarity = ["common", "common", "uncommon", "common", "rare"];
  const cards = [];
  let id = 1;
  named.forEach(([name, el, r, story]) => cards.push({ id: id++, name, element: el, rarity: r, story }));
  Object.entries(fillerNames).forEach(([el, list]) =>
    list.forEach((n, i) =>
      cards.push({
        id: id++,
        name: n,
        element: el,
        rarity: el === "Nihilum" ? "epic" : fillerRarity[i % fillerRarity.length],
        story: "Một cảm xúc thường ngày của ai đó, đủ mạnh để kết tinh thành hình hài nhỏ bé này.",
      })
    )
  );
  const rarityIndex = (k) => RARITIES.findIndex((r) => r.key === k);
  function rollRarity() {
    let x = Math.random() * 100;
    for (const r of RARITIES) { if ((x -= r.odds) < 0) return r.key; }
    return "common";
  }
  function pickCard(rarity) {
    let pool = cards.filter((c) => c.rarity === rarity);
    if (!pool.length) pool = cards.filter((c) => rarityIndex(c.rarity) <= rarityIndex(rarity));
    return pool[Math.floor(Math.random() * pool.length)];
  }
  /* Mô phỏng phía server: quay 5 slot, áp pity (49 → pack thứ 50 bảo đảm Legendary). */
  function openPack(pityBefore) {
    let results = Array.from({ length: 5 }, () => pickCard(rollRarity()));
    const hasLeg = (arr) => arr.some((c) => rarityIndex(c.rarity) >= 4);
    let pityTriggered = false;
    if (pityBefore >= 49 && !hasLeg(results)) {
      let low = 0;
      results.forEach((c, i) => { if (rarityIndex(c.rarity) < rarityIndex(results[low].rarity)) low = i; });
      results[low] = pickCard("legendary");
      pityTriggered = true;
    }
    const pityAfter = hasLeg(results) ? 0 : pityBefore + 1;
    const order = results.slice().sort((a, b) => rarityIndex(a.rarity) - rarityIndex(b.rarity));
    const maxR = rarityIndex(order[order.length - 1].rarity);
    return { cards: order, pityAfter, pityTriggered, climax: maxR >= 3, maxRarity: RARITIES[maxR].key };
  }
  /* Số lượng phát hành tối đa mỗi Card Definition theo rarity (BRD BR-SUP-01, đề xuất). */
  const MAX_SUPPLY = { common: 50000, uncommon: 20000, rare: 5000, epic: 1000, legendary: 300, secret: 100 };
  /* Lò rèn: lật 1 slot theo tỷ lệ rèn (bằng tỷ lệ 1 slot của pack), không pity, không yếu tố tác động. */
  function revealSealed() { return pickCard(rollRarity()); }
  /* Bản dịch nội dung thẻ (BR-I18N-03): tên riêng giữ Latin; story và nhãn hệ dịch theo ngôn ngữ. */
  const EL_LABEL = {
    en: { Luminara: "Hope · Joy", Umbryx: "Fear · Loneliness", Pyraxis: "Anger · Passion", Aqualis: "Sorrow · Nostalgia", Terrakin: "Resolve · Endurance", Ventara: "Freedom · Curiosity", Voltaris: "Excitement · Chaos", Nihilum: "Despair · Emptiness" },
    "zh-Hans": { Luminara: "希望 · 喜悦", Umbryx: "恐惧 · 孤独", Pyraxis: "愤怒 · 热情", Aqualis: "悲伤 · 怀念", Terrakin: "坚定 · 坚韧", Ventara: "自由 · 好奇", Voltaris: "兴奋 · 混乱", Nihilum: "绝望 · 空虚" },
    "zh-Hant": { Luminara: "希望 · 喜悅", Umbryx: "恐懼 · 孤獨", Pyraxis: "憤怒 · 熱情", Aqualis: "悲傷 · 懷念", Terrakin: "堅定 · 堅韌", Ventara: "自由 · 好奇", Voltaris: "興奮 · 混亂", Nihilum: "絕望 · 空虛" },
  };
  const STORY = {
    en: {
      1: "I was born from the smile of a child seeing the sun for the first time after a storm. I don't remember the child's name. But I remember that feeling — the feeling that everything will be all right.",
      2: "I do not cry. I am the tears. I was born from a man who wept in silence for twenty years. He let no one see. But I saw. I am every tear he never let fall.",
      3: "I am the anger that was never spoken. I stay loyal to the one who made me, even when they want to forget me.",
      4: "I carry the sound of waves from a harbor where no one waits anymore. I remember every ship that never came home.",
      5: "I was made from a promise that was never broken.",
      6: "I am the \"what if\" of a child standing on a hilltop.",
      7: "I am the moment everything suddenly becomes clear — and then shatters.",
      8: "Mother... I'm here...",
      _: "An everyday feeling of someone, strong enough to crystallize into this small form.",
    },
    "zh-Hans": {
      1: "我诞生于一个孩子在暴风雨后第一次看到太阳时的笑容。我不记得那个孩子的名字，但我记得那种感觉——一切都会好起来的感觉。",
      2: "我不哭泣。我就是眼泪。我诞生于一个默默哭泣了二十年的男人。他不让任何人看见，但我看见了。我是他所有未曾落下的泪水。",
      3: "我是从未说出口的愤怒。即使创造我的人想要忘记我，我依然忠于他。",
      4: "我带着一个再也无人等待的港口的浪声。我记得每一艘没有归来的船。",
      5: "我由一个从未被打破的承诺铸成。",
      6: "我是一个站在山顶的孩子心中的“如果”。",
      7: "我是一切忽然变得清晰——然后破碎的那一刻。",
      8: "妈妈……我在这里……",
      _: "某人日常的一份情感，强烈到足以凝结成这个小小的形体。",
    },
    "zh-Hant": {
      1: "我誕生於一個孩子在暴風雨後第一次看到太陽時的笑容。我不記得那個孩子的名字，但我記得那種感覺——一切都會好起來的感覺。",
      2: "我不哭泣。我就是眼淚。我誕生於一個默默哭泣了二十年的男人。他不讓任何人看見，但我看見了。我是他所有未曾落下的淚水。",
      3: "我是從未說出口的憤怒。即使創造我的人想要忘記我，我依然忠於他。",
      4: "我帶著一個再也無人等待的港口的浪聲。我記得每一艘沒有歸來的船。",
      5: "我由一個從未被打破的承諾鑄成。",
      6: "我是一個站在山頂的孩子心中的「如果」。",
      7: "我是一切忽然變得清晰——然後破碎的那一刻。",
      8: "媽媽……我在這裡……",
      _: "某人日常的一份情感，強烈到足以凝結成這個小小的形體。",
    },
  };
  function storyOf(c, locale) { if (locale === "vi") return c.story; const m = STORY[locale] || STORY.en; return m[c.id] || m._; }
  function elLabel(el, locale) { if (locale === "vi") return ELEMENTS[el].label; return (EL_LABEL[locale] || EL_LABEL.en)[el]; }
  return { ELEMENTS, RARITIES, cards, rarityIndex, openPack, revealSealed, MAX_SUPPLY, storyOf, elLabel };
})();
