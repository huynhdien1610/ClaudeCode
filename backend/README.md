# ANIMA — Backend (.NET 10)

Modular monolith theo [SOLUTION_DESIGN.md](../docs/SOLUTION_DESIGN.md). Phạm vi hiện có: **vòng chơi R1 trên website** — đăng ký, ví, mua và mở pack có thể kiểm chứng, bộ sưu tập, Lò rèn, quy đổi Gem ↔ Coin, gói chào mừng. Chưa có: chợ, đấu giá, NFT, điểm danh/ads (chỉ app), thanh toán thật, trận đấu (R2). Website người chơi dùng backend này: xem [web/apps/player](../web/apps/player/README.md).

## Chạy thử

```bash
infra/dev-postgres.sh start                       # PostgreSQL cục bộ (không cần Docker); in ra chuỗi kết nối
# hoặc: docker compose -f infra/docker-compose.yml up -d  (khi đã có file compose)
dotnet run --project backend/src/Anima.Api        # http://localhost:5080, tự migrate + seed 100 thẻ tạm
```

Development bật `Dev:MockTopUp` (nạp Gem giả: `POST /v1/dev/topup`) và `Identity:DevLogOtp` (mã OTP in ra log). Hai cờ này **không** bật ngoài Development.

| Lệnh | Việc |
|---|---|
| `dotnet build backend/Anima.sln` | Build (cảnh báo là lỗi) |
| `dotnet test backend/Anima.sln` | 275 test (81 đơn vị, 42 kiến trúc, 152 tích hợp): đơn vị, kiến trúc, tích hợp trên PostgreSQL thật |
| `dotnet format backend/Anima.sln --verify-no-changes` | Kiểm tra định dạng |
| `UPDATE_OPENAPI=1 dotnet test backend/tests/Anima.IntegrationTests --filter OpenApiContract` | Cập nhật snapshot hợp đồng API khi cố ý đổi API |

Test tích hợp tạo một database riêng cho mỗi lớp test từ `ANIMA_TEST_PG` (mặc định `Host=127.0.0.1;Port=55432;Username=postgres`). Trên CI dùng service `postgres:17`.

## Cấu trúc

```
backend/
├─ src/
│  ├─ Anima.Contracts/        netstandard2.1, dùng chung với Unity: mã lỗi (đúng tên trong BDD), hằng số tiền tệ/rarity/hệ
│  ├─ Anima.SharedKernel/     UnitOfWork (1 connection + 1 transaction/request), Migrator SQL, Idempotency, FieldCipher (AES-GCM), lỗi nghiệp vụ
│  ├─ Anima.Modules.*/        mỗi module: SQL migration riêng (schema riêng) + service + endpoint
│  └─ Anima.Api/              host: JWT, CORS, OpenAPI, health, khởi động (migrate + seed)
└─ tests/  Anima.UnitTests · Anima.ArchTests · Anima.IntegrationTests
```

| Module | Schema | Nội dung | Quy tắc BRD chính |
|---|---|---|---|
| Identity | `identity` | đăng ký, đăng nhập (PBKDF2, khóa 15 phút sau 5 lần sai), OTP SĐT, ngôn ngữ; PII mã hóa AES-GCM | BR-ACC-01/02/05, BR-GEO-03/04, BR-I18N-01 |
| Fairness | `fairness` | seed commit–reveal, đổi seed, công cụ kiểm chứng | BR-PF-01 → 05 |
| Economy | `economy` | tham số kinh tế có version, maker-checker ở DB | BR-ECO-03 |
| Wallet | `wallet` | ledger append-only (trigger), số dư, quy đổi, hạn mức ngày | BR-WAL-01/03/05/06 |
| Catalog | `catalog` | mùa, 100 thẻ (seed tạm), số lượng phát hành, pack, tỷ lệ có version | BR-CARD-*, BR-SUP-*, BR-PACK-01, BR-ADM-03 |
| Collection | `collection` | Card Instance (serial, `#n/N`), tiến độ set, story theo quyền | BR-SUP-02, US-05.* |
| Gacha | `gacha` | mua pack idempotent, mở pack, pity, gói chào mừng | BR-PACK-02/04/05/06, BR-NEW-01/04, BR-SUP-04 |
| Forge | `forge` | rèn 2 → 1, lật thẻ chưa lật, hạn mức ngày | BR-FRG-01 → 07 |
| Deck | `deck` | bộ bài 30 lá, tối đa 10 bộ, bộ mặc định, kiểm luật BR-DECK-01→04, gỡ thẻ rời trạng thái Owned (BR-DECK-06), `IDeckApi.RequireBattleReadyAsync` cho trận (BR-DECK-07) | BR-DECK-01→07 |
| Battle | `battle` | máy đấu lõi thuần logic (BR-BTL, BR-ELM: Cộng hưởng, tấn công, hệ khắc/sinh, chuỗi nhân quả, đột tử, hết giờ, đổi tay), tất định và phát lại được; bot luyện tập; trận luyện tập lưu seed + hành động. **Chưa có**: kỹ năng thẻ, Tiếng vọng, Ký ức phong ấn, luật sàn, hẹn giờ thật, giao hữu, xếp hạng | BR-BTL-01→10, BR-ELM-01→06 (không có BR-ARN) |
| Quest | `quest` | nhiệm vụ Tân thủ 7 ngày, nhận thưởng (pack cơ bản gắn chặt tài khoản, 200 Coin ngày 6–7), làm bù đến hết ngày 7 | BR-NEW-03/04/05 |
| Payment | `payment` | nạp Gem qua cổng web: bảng giá công khai, đơn hàng, webhook ký HMAC idempotent, hoàn tiền thu hồi Gem (có thể âm → Restricted NEGATIVE_GEM, nạp bù tự gỡ); cổng giả lập `sandbox`, cổng thật cài `IPaymentGateway` | BR-WEB-04/05, BR-WAL-02/04, SC-WAL-01/02/06/07/10–15 |
| Admin | `admin` | đăng nhập quản trị (JWT riêng), 6 vai trò, audit log append-only, tra cứu tài khoản (PII che), khóa/mở, tỷ lệ rơi maker-checker, tham số kinh tế, bồi thường, dashboard | BR-ADM-*, SC-ADM-01..14 |

Ranh giới module do `Anima.ArchTests` canh: chỉ tham chiếu module được phép, không có vòng, không dùng lớp cài đặt (`*Service`) của module khác, không dùng `System.Random`.

## API chính (đầy đủ ở [contracts/openapi/anima.v1.json](../contracts/openapi/anima.v1.json))

Lỗi trả JSON `{ "code": "...", "message": "...", "details": ... }`; `code` cố định theo BDD. Mọi `POST` có tiền hoặc tạo bản ghi cần header `Idempotency-Key`.

| Việc | Endpoint |
|---|---|
| Đăng ký / đăng nhập | `POST /v1/accounts`, `POST /v1/auth/login` (trả `accessToken`, dùng `Authorization: Bearer`) |
| Hồ sơ, ngôn ngữ, SĐT | `GET /v1/me`, `PUT /v1/me/locale`, `POST /v1/me/phone/otp`, `POST /v1/me/phone/verify` |
| Ví | `GET /v1/wallet`, `GET /v1/wallet/ledger`, `POST /v1/wallet/convert` |
| Cửa hàng (công khai) | `GET /v1/packs`, `GET /v1/packs/{code}` (có tỷ lệ rơi), `GET /v1/economy` |
| Mua / mở pack | `POST /v1/packs/{code}/purchase`, `GET /v1/me/packs`, `POST /v1/pack-instances/{id}/open`, `GET /v1/pack-instances/{id}/opening`, `GET /v1/me/pity/{code}` |
| Bộ sưu tập | `GET /v1/collection`, `GET /v1/collection/progress`, `GET /v1/cards`, `GET /v1/cards/{id}` |
| Lò rèn | `GET /v1/forge/info`, `POST /v1/forge`, `GET /v1/me/sealed`, `POST /v1/forge/sealed/{id}/reveal` |
| Công bằng | `GET /v1/fairness`, `PUT /v1/fairness/client-seed`, `POST /v1/fairness/rotate`, `GET /v1/fairness/history`, `POST /v1/fairness/verify` (công khai) |

Luồng chơi trên website: đăng ký → nhận 100 Coin + gói chào mừng → mở gói chào mừng (5 Common, 5 hệ, gắn chặt tài khoản) → nạp Gem (dev: `/v1/dev/topup`) → mua pack → mở pack → rèn thẻ thừa → kiểm chứng kết quả bằng `rotate` + `verify`.

## Quyết định kỹ thuật khác với tài liệu (cần Tech Lead xác nhận)

| Điểm | Tài liệu | Bản này | Lý do |
|---|---|---|---|
| Truy cập dữ liệu | EF Core + Npgsql | Npgsql + SQL tường minh, migration SQL viết tay | Ledger, kho số lượng, khóa seed cần kiểm soát từng câu lệnh và khóa dòng; ràng buộc/trigger nằm trong SQL. EF có thể thêm cho phần đọc phức tạp |
| Đăng nhập | Firebase Auth | Email + mật khẩu và JWT do backend cấp | Không có Firebase trong môi trường dev; đã tách ở `IdentityService`, thay bằng xác minh Firebase ID token khi chốt (T023) |
| Lỗi nghiệp vụ | `Result<T>` | `DomainException` có mã | Tự rollback transaction đang chạy; ít code lặp |
| PostgreSQL | 17 | 16 khi chạy cục bộ trong sandbox; CI dùng 17 | Chỉ khác môi trường chạy, SQL không dùng tính năng riêng của 17 |
| BDD | Reqnroll | xUnit, mỗi test ghi mã `SC-*` trong tên | Chuyển sang Reqnroll khi chốt bộ step; kịch bản đã có mã để ánh xạ |

## Việc còn lại / khoảng trống đã biết

- **Chưa có:** rate limit (NFR-16), thiết bị gắn tài khoản và phiên web tối đa 3 (BR-ACC-03, BR-WEB-02), thanh toán thật (IAP, cổng web, đối soát), ma trận tính năng theo quốc gia đầy đủ (hiện chỉ chặn quốc gia và tuổi tối thiểu), admin (catalog, duyệt tỷ lệ, bồi thường), nhiệm vụ Tân thủ 7 ngày, trận đấu/bộ bài (R2), chợ, NFT.
- **Dữ liệu tạm:** 100 thẻ và chỉ số trong `CatalogSeeder` là placeholder theo khung BR-CARD-03; kỹ năng và truyện (trừ 8 thẻ có tên) chờ Game Designer và Content (T125).
- **Chờ quyết định PO:** tỷ lệ rơi phương án A (CF-01), ngưỡng pity 49 (Q-09), cách xử lý khi hết bản giữa chừng — hiện hạ xuống rarity gần nhất và ghi lý do (Q-39).
- **Trạng thái cổng:** Intake/Analysis chưa qua (`.vibe/checkpoint.json`); code được bắt đầu theo chỉ đạo trực tiếp của PO. Task trong backlog chưa chuyển trạng thái.

## Cổng thanh toán (nạp Gem trên web)

Cấu hình ở mục `Payment` (appsettings hoặc biến môi trường `Payment__*`):

| Khóa | Ý nghĩa |
| --- | --- |
| `Provider` | tên cổng đang dùng; mặc định `sandbox` (cổng giả lập, không có tiền thật) |
| `WebhookSecret` | khóa HMAC-SHA256 ký webhook, header `X-Signature` = hex(HMAC(secret, thân request gốc)). **Bắt buộc đặt riêng** ở môi trường thật |
| `Sandbox` | `true` bật trang thanh toán thử và `POST /v1/payments/sandbox/{orderId}/simulate`. **Không bật ở production** |
| `PublicBaseUrl` | địa chỉ website người chơi |

Webhook của cổng gọi `POST /payments/webhook/{provider}` (không JWT). Gem chỉ được cộng khi chữ ký đúng và số tiền/tiền tệ khớp đơn; mỗi gateway transaction ID chỉ ghi nhận một lần; mọi webhook (kể cả sai chữ ký) vào `payment.webhook_log`. **Chưa có cổng thật nào được tích hợp**: cần hợp đồng merchant (VNPay, MoMo, Stripe...) rồi cài `IPaymentGateway` (tạo phiên thanh toán + xác thực webhook của cổng đó) và, nếu cổng yêu cầu, thêm truy vấn trạng thái chủ động (BR-WEB-04).
