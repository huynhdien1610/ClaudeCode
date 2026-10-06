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
    { key: "common", name: "Common", vi: "Thường", color: "#9CA3AF", odds: 45, flip: 0.4 },
    { key: "uncommon", name: "Uncommon", vi: "Ít gặp", color: "#22C55E", odds: 25, flip: 0.6 },
    { key: "rare", name: "Rare", vi: "Hiếm", color: "#3B82F6", odds: 18, flip: 1.0 },
    { key: "epic", name: "Epic", vi: "Sử thi", color: "#A855F7", odds: 7, flip: 1.5 },
    { key: "legendary", name: "Legendary", vi: "Huyền thoại", color: "#F59E0B", odds: 4, flip: 3.0 },
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
  return { ELEMENTS, RARITIES, cards, rarityIndex, openPack };
})();
