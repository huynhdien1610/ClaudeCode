# ANIMA — Hệ thống Kỹ năng Thẻ (bản triển khai)

Nguồn: bản thiết kế "Hệ thống Kỹ năng Thẻ & Các Chế độ Đấu v1.0" (PO, 2026-10-12). Tài liệu này ghi lại **cách máy đấu hiểu và cài đặt** từng kỹ năng, các quyết định đã chốt với PO và phần bổ sung (tương khắc ±20%). Khi tài liệu này và mã nguồn lệch nhau, sửa tài liệu cùng lúc với mã. Nội dung chưa được đưa vào BRD; chờ PO duyệt thì hợp nhất thành CR-005.

## 1. Quyết định đã chốt (PO trả lời ngày 2026-10-12)

| # | Câu hỏi | Quyết định |
|---|---|---|
| D-1 | Kỹ năng gắn với thẻ thế nào, khi BR-CARD-02 chỉ cho 1 kỹ năng/Anima? | **Cấp thẻ + cấp đội.** Mỗi Anima giữ 1 kỹ năng riêng (Active, Passive hoặc Trigger) lấy từ danh sách 4 kỹ năng cấp thẻ của hệ nó, lưu ở `skill_id`. **Resonance, Ultimate, Fusion là khả năng cấp đội**: dùng được khi đủ điều kiện trên sân, không cần thẻ nào "giữ" chúng. |
| D-2 | Sát thương thiết kế (30, 60, 120) so với Keeper 8.000 máu | **Nhân 10** (`SkillScale = 10`): 30 → 300, Ultimate 120 → 1.200, Burn 5 → 50/lượt. Phần trăm giữ nguyên. Một hằng số duy nhất để chỉnh cân bằng. |
| D-3 | SPD, dodge, Emotion Meter | Làm **Emotion Meter** đầy đủ. **Không** đổi thứ tự đánh theo SPD (vẫn lần lượt). SPD là chỉ số phụ (mặc định 100) chỉ ảnh hưởng **tỷ lệ né**; dodge là xác suất lấy từ RNG tất định của trận (phát lại vẫn đúng). |

## 2. Bổ sung: tương khắc ±20% lên kỹ năng

Lấy từ yêu cầu của PO: *"ngũ hành tương khắc sẽ giảm 20% khả năng hoặc tăng 20% khả năng"*.

- Quan hệ tính giữa **hệ của Anima dùng kỹ năng** và **hệ của mục tiêu địch** theo vòng khắc BR-ELM-02: người dùng **khắc** mục tiêu → **+20%**; mục tiêu **khắc** người dùng → **−20%**; còn lại 0%. Luminara khắc Nihilum → +20%. Nihilum không có quan hệ khắc nào khác với kỹ năng (đòn thường vẫn giữ ×1,1 của BR-ELM-03).
- Áp dụng cho **độ lớn** hiệu ứng lên **kẻ địch**: sát thương, sát thương Burn khi gây ra, phần trăm debuff (ATK −20% thành −24% hoặc −16%), xác suất stun. **Không** áp dụng cho thời lượng, hồi máu/buff lên đồng đội, hiệu ứng lên bản thân. AoE tính riêng cho từng mục tiêu.
- Đòn đánh thường giữ nguyên công thức BR-BTL-05 (hệ số 1,25 / 0,75); phần ±20% chỉ dành cho kỹ năng.

## 3. Quy ước cơ chế

- **Thời lượng "N lượt"** đếm theo lượt của **chủ sở hữu Anima chịu hiệu ứng**, trừ một lần ở cuối mỗi lượt của họ. Buff tự dùng trong lượt của mình tính luôn lượt hiện tại là lượt 1.
- **Hồi chiêu (CD)**: đặt sau khi dùng, giảm 1 ở đầu mỗi lượt của chủ; dùng được khi bằng 0.
- **Active**: dùng thay cho đòn đánh thường của Anima đó trong lượt (một hành động/Anima/lượt); Anima vừa ra sân chưa dùng được (giống BR-BTL-04); Anima bị stun không hành động được.
- **Sát thương kỹ năng** = `độ_lớn × 10 × (1 ± 20% tương khắc)`; trừ giáp: `max(50% sát thương, sát thương − DEF×10%)`, làm tròn hàng chục. Kỹ năng "bỏ qua DEF" thì không trừ giáp. Sát thương kỹ năng **không** phụ thuộc ATK (ATK chỉ ảnh hưởng đòn thường).
- **Taunt**: nếu bên địch có Anima đang Taunt, mọi đòn đánh thường và kỹ năng đơn mục tiêu của ta phải nhắm vào Anima đó.
- **Buff/debuff**: Nihilum (Emptiness) miễn mọi debuff; Aqualis (Deep Calm) miễn Fear và các debuff từ Chaos. Burn xếp chồng tối đa 1 lớp, 3 lớp khi Twin Fury đang hiệu lực.
- **Emotion Meter** chung hai bên, 0–100: +2 khi Anima tấn công (đòn thường), +3 khi Anima bị đánh thường, +5 khi Anima ngã, +5 khi dùng Active/Ultimate/Fusion. Đạt 100 → **Emotional Surge**: mọi Anima hai bên +20% ATK và +20% DEF trong 2 lượt của mỗi bên; Meter về 0.
- **Ultimate**: cần Meter ≥ 50 và một Anima cùng hệ trên sân (không stun, không Fear, chưa hành động trong lượt, không phải vừa ra sân); **tiêu 50 Meter**; tính là hành động của Anima đó.
- **Resonance**: tự động khi có từ 2 Anima cùng hệ trên sân (không tốn hành động).
- **Fusion**: cần đủ hai hệ trên sân; **1 lần/trận mỗi Fusion** mỗi bên; không tốn hành động của Anima nào. Tối đa 1 Fusion mỗi lượt.
- **Dodge** = dodge cộng thêm + (SPD hiệu lực − 100) × 0,25 điểm %, tối đa 75%. Né được thì đòn trúng bị hủy hoàn toàn. Không né được khi bị Undertow/Earth's Grasp. Burn, phản đòn, lan sét, Ultimate/Fusion AoE không né được.
- **Giá trị chưa chốt tôi tự đặt** (đánh dấu ⚑ bên dưới, chờ PO): chi phí Ultimate 50 Meter; trần Overcharge +50%; DEF giảm trừ 10%; Surge cộng cả DEF; "không thể one-shot" của Unyielding hiểu là sống sót 1 lần ở 1 HP.

## 4. Kỹ năng cấp thẻ (mỗi Anima giữ 1; phân theo thứ tự thẻ trong hệ: Active A, Passive, Trigger, Active B, lặp)

### Luminara
| Id | Tên | Loại | Hiệu ứng triển khai | CD |
|---|---|---|---|---|
| `luminara.dawns_embrace` | Dawn's Embrace | Active, đồng đội | Hồi 20% HP tối đa; nếu mục tiêu dưới 30% HP thì 40% | 2 |
| `luminara.radiant_aura` | Radiant Aura | Passive | Đầu mỗi lượt của chủ, mọi đồng đội +1 lớp DEF 5% (tối đa 25%) khi còn Anima giữ kỹ năng này trên sân | — |
| `luminara.hopes_echo` | Hope's Echo | Trigger | Khi một đồng đội ngã: hồi 15% HP cho mọi đồng đội còn lại; 1 lần/trận mỗi thẻ | — |
| `luminara.blinding_light` | Blinding Light | Active, địch | 300 sát thương; ATK địch −20% trong 2 lượt | 3 |

### Umbryx
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `umbryx.silent_tear` | Silent Tear | Active, địch | 250 sát thương; ×1,5 nếu mục tiêu đang có debuff | 1 |
| `umbryx.veil_of_sorrow` | Veil of Sorrow | Passive | HP dưới 50%: SPD +30%, dodge +20% | — |
| `umbryx.lonely_howl` | Lonely Howl | Trigger | Khi đồng đội ngã: mọi địch ATK −15% 2 lượt; 1 lần/trận mỗi thẻ | — |
| `umbryx.nightmare_touch` | Nightmare Touch | Active, địch | 200 sát thương; Fear 2 lượt (không làm người ra Ultimate được) | 3 |

### Pyraxis
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `pyraxis.ember_strike` | Ember Strike | Active, địch | 350 sát thương + Burn 50/lượt trong 3 lượt | 2 |
| `pyraxis.rage_bound` | Rage Bound | Passive | Mỗi 10% HP đã mất → +5% ATK (tối đa +50%) | — |
| `pyraxis.fury_unleashed` | Fury Unleashed | Trigger | Khi bị đánh (đòn thường hoặc kỹ năng): phản 20% sát thương nhận | — |
| `pyraxis.flame_vortex` | Flame Vortex | Active, mọi địch | 250 sát thương + Burn 30/lượt 3 lượt | 3 |

### Aqualis
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `aqualis.tidal_memory` | Tidal Memory | Active, địch | 200 sát thương; Emotion Meter +5 | 1 |
| `aqualis.deep_calm` | Deep Calm | Passive | Miễn Fear/Chaos; DEF +10% | — |
| `aqualis.echo_of_loss` | Echo of Loss | Trigger | Khi địch dùng Ultimate: sát thương Ultimate đó −50% | 2 |
| `aqualis.undertow` | Undertow | Active, địch | SPD −30% 2 lượt; không thể né 2 lượt | 3 |

### Terrakin
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `terrakin.stone_wall` | Stone Wall | Active, bản thân | DEF +50% 2 lượt; Taunt 2 lượt | 3 |
| `terrakin.unyielding` | Unyielding | Passive | HP dưới 30%: DEF +50%; sống sót 1 lần/trận với 1 HP khi nhận đòn chí mạng ⚑ | — |
| `terrakin.avalanche` | Avalanche | Trigger | Khi bị đánh: 150 sát thương phản lại; ×1,5 nếu DEF hiệu lực > 1.000 | — |
| `terrakin.earths_grasp` | Earth's Grasp | Active, địch | Địch không thể đổi chỗ/né 2 lượt | 2 |

### Ventara
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `ventara.wind_step` | Wind Step | Active, đồng đội | Đổi chỗ với đồng đội; cả hai SPD +20% 2 lượt | 1 |
| `ventara.free_spirit` | Free Spirit | Passive | Dodge +25%; né thành công → ATK +10% lượt sau | — |
| `ventara.curiosity` | Curiosity | Trigger | Đầu mỗi lượt của chủ: 30% nhận ngẫu nhiên ATK/DEF/SPD +15% trong 2 lượt | — |
| `ventara.gust_barrier` | Gust Barrier | Active, bản thân | Đòn kế tiếp −50%; dodge +15% 2 lượt | 2 |

### Voltaris
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `voltaris.surge_bolt` | Surge Bolt | Active, địch | 400 sát thương; 30% stun 1 lượt | 2 |
| `voltaris.overcharge` | Overcharge | Passive | Đầu mỗi lượt của chủ +1 lớp ATK 10% (tối đa 5 lớp ⚑); cuối lượt không hành động → mất nửa số lớp | — |
| `voltaris.chain_reaction` | Chain Reaction | Trigger | Khi gây sát thương (thường/kỹ năng): 25% lan 50% sát thương sang địch khác (50% khi có Voltage Storm) | — |
| `voltaris.static_field` | Static Field | Active, mọi địch | SPD −20% 2 lượt; mỗi địch 20% stun 1 lượt | 3 |

### Nihilum
| Id | Tên | Loại | Hiệu ứng | CD |
|---|---|---|---|---|
| `nihilum.void_touch` | Void Touch | Active, địch | 500 sát thương; bản thân mất 10% HP tối đa | 2 |
| `nihilum.emptiness` | Emptiness | Passive | Miễn mọi debuff; không thể hồi máu | — |
| `nihilum.despair` | Despair | Trigger | HP dưới 20%: ATK +100%; mất 5% HP tối đa mỗi đầu lượt | — |
| `nihilum.oblivion` | Oblivion | Active, địch | Xóa 1 buff của địch; 300 sát thương | 3 |

## 5. Khả năng cấp đội

### Resonance (tự động khi có 2+ Anima cùng hệ trên sân)
| Hệ | Tên | Hiệu ứng |
|---|---|---|
| Luminara | Sacred Bond | 1 lần/trận, ngay khi đủ điều kiện: mọi Luminara HP tối đa +30% (hồi tương ứng), DEF +20% trong 3 lượt |
| Umbryx | Shared Pain | Sát thương một Umbryx nhận được chia đều cho mọi Umbryx trên sân; mỗi Umbryx ATK +10% |
| Pyraxis | Twin Fury | Mỗi Pyraxis ATK +15%; Burn xếp chồng tối đa 3 lớp |
| Aqualis | Shared Memory | Đầu lượt: mọi Aqualis hồi 5% HP; nhận sát thương −10% |
| Terrakin | Mountain's Bond | DEF các Terrakin lấy trung bình cộng; HP tối đa +20% |
| Ventara | Chaos Theory | Đầu lượt: 1 Ventara ngẫu nhiên dùng miễn phí 1 Active đã sẵn sàng (không tính hành động, không CD) |
| Voltaris | Voltage Storm | Mỗi Voltaris ATK +25%; Chain Reaction lên 50% |
| Nihilum | The Void Calls | 1 lần/trận mỗi bên: một Nihilum sắp ngã mà còn Nihilum khác trên sân thì hồi sinh với 30% HP |

### Ultimate (Meter ≥ 50, tiêu 50)
| Hệ | Tên | Mục tiêu | Hiệu ứng |
|---|---|---|---|
| Luminara | Seraphel's Blessing | đồng đội | Hồi đầy HP, xóa mọi debuff |
| Umbryx | Nocturne's Requiem | mọi địch | 600 sát thương; người dùng hồi 30% HP |
| Pyraxis | Emberfang's Wrath | địch | 1.000 sát thương; nếu hạ được thì người dùng hồi 50% HP |
| Aqualis | Tidemourn's Lament | mọi đồng đội | Hồi 50% HP; xóa mọi debuff |
| Terrakin | Stoneward's Vow | bản thân | Bất tử 2 lượt (không nhận sát thương); mọi đồng đội hồi 30% HP |
| Ventara | Zephyrion's Gambit | ngẫu nhiên | 5 đòn, mỗi đòn 200 sát thương lên một địch ngẫu nhiên |
| Voltaris | Surgeflux's Revelation | mọi địch | 800 sát thương; mỗi địch 50% stun 1 lượt |
| Nihilum | The Nameless | địch | 1.200 sát thương; người dùng mất 30% HP tối đa |

### Fusion (1 lần/trận mỗi bên, cần đủ hai hệ trên sân)
| Id | Tên | Hệ | Hiệu ứng |
|---|---|---|---|
| `dawnbreak` | Dawnbreak | Luminara + Pyraxis | Mọi đồng đội hồi 30% HP, ATK +20% 2 lượt |
| `silent_storm` | Silent Storm | Umbryx + Voltaris | 400 sát thương mọi địch; stun 1 lượt |
| `eternal_tide` | Eternal Tide | Aqualis + Terrakin | Mọi đồng đội bất tử 1 lượt, hồi 20% HP |
| `chaos_theory_fusion` | Chaos Theory (Fusion) | Ventara + Voltaris | Mỗi địch nhận 1 debuff ngẫu nhiên (Burn, Fear, Slow, ATK −20%, DEF −20%) |
| `voids_edge` | Void's Edge | Nihilum + Umbryx | 600 sát thương lên 1 địch, bỏ qua DEF |
| `hopes_last_stand` | Hope's Last Stand | Luminara + Nihilum | Nếu tổng HP đội dưới 30% tổng HP tối đa: hồi đầy HP cho 1 đồng đội |

## 6. Câu hỏi còn mở cho PO

1. Chi phí Ultimate: tiêu 50 Meter (đang làm) hay chỉ cần đạt ngưỡng mà không tiêu?
2. DEF có nên giảm sát thương kỹ năng (đang giảm tối đa 50%) hay kỹ năng luôn bỏ qua DEF?
3. Trần của Overcharge và các hiệu ứng cộng dồn (đang +50%).
4. "Bất tử" của Stoneward's Vow / Eternal Tide có chặn cả Burn không (đang chặn mọi sát thương).
5. Thẻ Tiếng vọng và Ký ức phong ấn vẫn chưa có luật chơi.
6. Thế nào là "đồng đội ngã" khi hồi sinh bằng The Void Calls (đang tính là chưa ngã).
