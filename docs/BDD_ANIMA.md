# BDD — ANIMA: Echoes of the Heart
## Đặc tả hành vi (Behavior-Driven Development)

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | BDD-ANIMA-001 |
| Phiên bản | 0.5 (Draft) — thêm 12D Đấu trường (CR-004); 12C toàn cầu (CR-003); 12B tài sản số (CR-002); 12A website (CR-001) |
| Ngày | 2026-10-06 |
| Nguồn | [BRD](BRD_ANIMA.md) v1.0, [PRD](PRD_ANIMA.md) v0.5 |
| Trạng thái | **DRAFT — một số kịch bản phụ thuộc quyết định của PO** |

Tài liệu này là bộ kịch bản đầy đủ. Mục 11 của BRD chỉ là trích đoạn; khi hai bên khác nhau, tài liệu này là bản chuẩn. Các ID kịch bản trong BRD được giữ nguyên.

---

## 0. Quy ước

### 0.1. Cấu trúc

- `Tính năng` ↔ Epic trong BRD. `Quy tắc` ↔ Business Rule. `Kịch bản` mô tả đúng một hành vi.
- `Cho trước` mô tả trạng thái; `Khi` là **một** hành động hoặc sự kiện; `Thì` là kết quả **quan sát được** (số dư, trạng thái, mã lỗi, bản ghi).
- `Sơ đồ kịch bản` + bảng `Ví dụ` dùng khi kết quả phụ thuộc từ 3 điều kiện trở lên.
- Mọi kiểm tra quyền và quy tắc được thực hiện ở **backend**. Kịch bản "unauthorized" gửi yêu cầu thẳng tới API, bỏ qua giao diện.
- Kịch bản không mô tả thao tác giao diện (nút, màn hình, vị trí).

### 0.2. Tag

| Tag | Ý nghĩa |
|---|---|
| `@R1`, `@R2` | Release áp dụng |
| `@happy`, `@bien`, `@negative`, `@unauthorized`, `@trangthai`, `@tinhtoan`, `@dongthoi`, `@idempotency`, `@tichhop` | Loại kịch bản |
| `@cho-Qxx`, `@cho-CFxx` | Phụ thuộc câu hỏi mở / mâu thuẫn trong BRD; số liệu có thể đổi khi PO quyết định |

### 0.3. Dữ liệu cấu hình dùng chung

Các giá trị dưới đây lấy từ đề xuất trong BRD. Khi PO thay đổi, chỉ cập nhật bảng này và các kịch bản có tag `@cho-*` tương ứng.

| Tham số | Giá trị |
|---|---|
| Giá pack "Awakening Standard" | 1,000 Coin hoặc 100 Gem |
| Số thẻ/pack | 5 (`@cho-Q08`) |
| Ngưỡng pity | 49 pack liên tiếp không có Legendary+ → pack thứ 50 đảm bảo (`@cho-Q09`) |
| Đổi Gem → Coin / Coin → Gem | 1 Gem → 9 Coin; 11 Coin → 1 Gem; tối đa 1,000 Gem/ngày từ Coin (CR-002) |
| Phí rèn | 50 Coin hoặc 5 Gem (CR-002) |
| Phí rút NFT, thời gian chờ | 200 Coin hoặc 20 Gem; 30 ngày (CR-002, `@cho-Q37`) |
| Thưởng điểm danh ngày 1→7 | 20, 30, 40, 50, 60, 80, 150 Coin |
| Mốc streak 14 / 30 / 100 | +200 / +1,000 / +5,000 Coin + thẻ độc quyền |
| Streak Freeze | 200 Coin hoặc 3 ads; giữ tối đa 2 (`@cho-Q15`) |
| Thưởng video / playable | 8 / 15 Coin |
| Bonus ad thứ 5 / thứ 10 | +10 / +20 Coin |
| Giới hạn ads, cooldown | 10 lượt/ngày, 60 giây |
| Referral | 500 Coin; tối đa 20 lượt/tháng |
| Phí chợ | 5% giá cố định, 10% đấu giá, làm tròn lên |
| Khoảng giá Rare (ví dụ) | 100 – 50,000 Coin (`@cho-Q19`) |
| Ngưỡng tranh chấp cần Fraud Analyst duyệt | > 10,000 Coin hoặc > 1,000 Gem |
| Múi giờ mặc định của tài khoản thử | Asia/Ho_Chi_Minh (UTC+7) |

---

## 1. Tính năng: EP-01 — Tài khoản & Xác thực

```gherkin
Tính năng: EP-01 — Tài khoản & Xác thực
  Bối cảnh chung:
    Cho trước ngày hiện tại trên server là 2026-10-06 theo múi giờ Asia/Ho_Chi_Minh

  Quy tắc: BR-ACC-01 — Người dùng phải từ 13 tuổi

    @R1 @happy
    Kịch bản: SC-ACC-01 — Đăng ký khi đủ 13 tuổi
      Cho trước người dùng khai ngày sinh 2010-05-01
      Khi người dùng gửi yêu cầu đăng ký bằng email "an@example.com"
      Thì tài khoản được tạo ở trạng thái "Unverified"

    @R1 @bien
    Kịch bản: SC-ACC-02 — Đúng ngày sinh nhật 13 tuổi
      Cho trước người dùng khai ngày sinh 2013-10-06
      Khi người dùng gửi yêu cầu đăng ký
      Thì tài khoản được tạo ở trạng thái "Unverified"

    @R1 @negative
    Kịch bản: SC-ACC-03 — Thiếu một ngày để đủ 13 tuổi
      Cho trước người dùng khai ngày sinh 2013-10-07
      Khi người dùng gửi yêu cầu đăng ký
      Thì server từ chối với mã lỗi AGE_BELOW_MINIMUM
      Và không có tài khoản nào được tạo

    @R1 @negative @cho-Q21
    Kịch bản: SC-ACC-04 — Người dưới 18 tuổi nạp Gem khi chưa có đồng ý của người giám hộ
      Cho trước tài khoản "U16" có ngày sinh 2010-05-01 và chưa có xác nhận của người giám hộ
      Khi U16 gửi receipt nạp 500 Gem hợp lệ
      Thì server từ chối với mã lỗi GUARDIAN_CONSENT_REQUIRED
      Và số dư Gem của U16 không đổi

  Quy tắc: BR-ACC-02 — Email và SĐT là duy nhất

    @R1 @happy
    Kịch bản: SC-ACC-05 — Email chưa dùng
      Cho trước chưa có tài khoản nào dùng email "binh@example.com"
      Khi người dùng đăng ký bằng email "binh@example.com"
      Thì tài khoản được tạo

    @R1 @bien
    Kịch bản: SC-ACC-06 — Email trùng khác chữ hoa/thường
      Cho trước đã có tài khoản dùng email "binh@example.com"
      Khi người dùng đăng ký bằng email "Binh@Example.com"
      Thì server từ chối với mã lỗi EMAIL_ALREADY_USED

    @R1 @bien
    Kịch bản: SC-ACC-07 — SĐT trùng khác định dạng
      Cho trước SĐT "+84901234567" đã được xác thực cho tài khoản "A1"
      Khi tài khoản "A2" xác thực SĐT "0901234567"
      Thì server từ chối với mã lỗi PHONE_ALREADY_USED
      Và trạng thái của A2 vẫn là "Unverified"

  Quy tắc: BR-ACC-03 — Một thiết bị một tài khoản

    @R1 @negative
    Kịch bản: SC-ACC-08 — Đăng nhập tài khoản khác trên thiết bị đã gắn
      Cho trước thiết bị "D1" đang gắn với tài khoản "A1"
      Khi tài khoản "A2" đăng nhập trên D1
      Thì server từ chối với mã lỗi DEVICE_BOUND_TO_OTHER_ACCOUNT

    @R1 @happy
    Kịch bản: SC-ACC-09 — Chuyển tài khoản sang thiết bị mới
      Cho trước A1 đang gắn với D1 và chưa chuyển thiết bị lần nào trong 30 ngày qua
      Khi A1 đăng nhập trên thiết bị mới "D2"
      Thì A1 được gắn với D2
      Và D1 không còn gắn với tài khoản nào
      Và số lần chuyển thiết bị trong 30 ngày của A1 là 1

    @R1 @bien
    Kịch bản: SC-ACC-10 — Lần chuyển thứ 3 trong 30 ngày
      Cho trước A1 đã chuyển thiết bị 2 lần, lần đầu vào 2026-09-10
      Khi A1 đăng nhập trên thiết bị mới "D4" ngày 2026-10-06
      Thì server từ chối với mã lỗi DEVICE_TRANSFER_LIMIT

    @R1 @bien
    Kịch bản: SC-ACC-11 — Lần chuyển cũ đã ra khỏi cửa sổ 30 ngày
      Cho trước A1 đã chuyển thiết bị 2 lần, vào 2026-09-05 và 2026-09-20
      Khi A1 đăng nhập trên thiết bị mới "D4" ngày 2026-10-06
      Thì A1 được gắn với D4

  Quy tắc: BR-ACC-05 — OTP

    @R1 @happy
    Kịch bản: SC-ACC-12 — Nhập đúng OTP trong thời hạn
      Cho trước A1 nhận OTP "482913" lúc 10:00:00
      Khi A1 nhập "482913" lúc 10:04:59
      Thì trạng thái của A1 là "Verified"

    @R1 @bien
    Kịch bản: SC-ACC-13 — OTP hết hạn
      Cho trước A1 nhận OTP "482913" lúc 10:00:00
      Khi A1 nhập "482913" lúc 10:05:01
      Thì server từ chối với mã lỗi OTP_EXPIRED

    @R1 @bien
    Kịch bản: SC-ACC-14 — Nhập sai lần thứ 5
      Cho trước A1 đã nhập sai OTP 4 lần
      Khi A1 nhập sai OTP lần thứ 5 lúc 10:02:00
      Thì A1 bị khóa xác thực đến 10:32:00
      Và mọi lần nhập OTP trước 10:32:00 bị từ chối với mã lỗi OTP_LOCKED

    @R1 @negative
    Kịch bản: SC-ACC-15 — Yêu cầu gửi OTP lần thứ 6 trong ngày
      Cho trước SĐT "+84901234567" đã được gửi OTP 5 lần trong ngày 2026-10-06
      Khi A1 yêu cầu gửi OTP tới SĐT đó
      Thì server từ chối với mã lỗi OTP_DAILY_LIMIT
      Và không có SMS nào được gửi

    @R1 @negative
    Kịch bản: SC-ACC-16 — OTP cũ sau khi đã gửi OTP mới
      Cho trước A1 nhận OTP "111111" rồi yêu cầu gửi lại và nhận "222222"
      Khi A1 nhập "111111"
      Thì server từ chối với mã lỗi OTP_INVALID

  Quy tắc: US-01.2 — Khóa đăng nhập khi sai mật khẩu

    @R1 @bien
    Kịch bản: SC-ACC-17 — Sai mật khẩu lần thứ 5
      Cho trước A1 đã nhập sai mật khẩu 4 lần liên tiếp
      Khi A1 nhập sai mật khẩu lần thứ 5 lúc 09:00:00
      Thì đăng nhập bằng mật khẩu của A1 bị khóa đến 09:15:00

    @R1 @happy
    Kịch bản: SC-ACC-18 — Đăng nhập đúng xóa bộ đếm sai
      Cho trước A1 đã nhập sai mật khẩu 4 lần liên tiếp
      Khi A1 đăng nhập đúng mật khẩu
      Thì đăng nhập thành công
      Và bộ đếm sai mật khẩu của A1 là 0

  Quy tắc: Vòng đời tài khoản (BRD mục 8.1)

    @R1 @trangthai
    Sơ đồ kịch bản: SC-ACC-19 — Chuyển trạng thái tài khoản
      Cho trước tài khoản "A1" ở trạng thái <từ>
      Khi <actor> thực hiện <hành động>
      Thì kết quả là <kết quả>
      Và trạng thái của A1 là <sau>

      Ví dụ:
        | từ               | actor          | hành động                          | kết quả                  | sau              |
        | Unverified       | A1             | xác thực OTP thành công            | thành công               | Verified         |
        | Verified         | Fraud Analyst  | hạn chế, lý do "nghi farm ads"     | thành công               | Restricted       |
        | Restricted       | Fraud Analyst  | gỡ hạn chế, số dư Gem = 0          | thành công               | Verified         |
        | Verified         | Fraud Analyst  | ban, lý do "bot"                   | thành công               | Banned           |
        | Verified         | Fraud Analyst  | ban, không nhập lý do              | REASON_REQUIRED          | Verified         |
        | Banned           | Fraud Analyst  | gỡ ban                             | FORBIDDEN                | Banned           |
        | Banned           | Super Admin    | gỡ ban sau khiếu nại được chấp nhận | thành công              | Verified         |
        | Verified         | A1             | yêu cầu xóa tài khoản              | thành công               | Pending Deletion |
        | Banned           | A1             | yêu cầu xóa tài khoản              | ACCOUNT_BANNED           | Banned           |
        | Pending Deletion | A1             | đăng nhập và hủy yêu cầu xóa       | thành công               | Verified         |

    @R1 @bien
    Kịch bản: SC-ACC-20 — Xóa tài khoản sau đúng 30 ngày
      Cho trước A1 yêu cầu xóa tài khoản lúc 2026-10-06 10:00:00
      Khi job xóa chạy lúc 2026-11-05 10:00:00
      Thì trạng thái của A1 là "Deleted"
      Và email, SĐT, thiết bị của A1 bị xóa khỏi dữ liệu cá nhân
      Và các bút toán ledger của A1 được giữ lại ở dạng ẩn danh

    @R1 @bien
    Kịch bản: SC-ACC-21 — Chưa đủ 30 ngày
      Cho trước A1 yêu cầu xóa tài khoản lúc 2026-10-06 10:00:00
      Khi job xóa chạy lúc 2026-11-05 09:59:59
      Thì trạng thái của A1 vẫn là "Pending Deletion"
```

---

## 2. Tính năng: EP-01 / BR-ACC-04 — Quyền theo trạng thái người chơi

```gherkin
Tính năng: Quyền theo trạng thái người chơi
  Quy tắc: BR-ACC-04 và ma trận BRD mục 12.1 — kiểm tra ở backend

    @R1 @R2 @unauthorized
    Sơ đồ kịch bản: SC-PERM-01 — Hành động theo trạng thái tài khoản
      Cho trước tài khoản "A1" ở trạng thái <trạng thái> với lý do hạn chế <lý do>
      Khi A1 gửi trực tiếp tới API yêu cầu <hành động>
      Thì kết quả là <kết quả>

      Ví dụ:
        | trạng thái | lý do         | hành động              | kết quả                       |
        | Unverified | —             | mua pack               | thành công                    |
        | Unverified | —             | điểm danh              | thành công (@cho-Q07)         |
        | Unverified | —             | niêm yết thẻ           | PHONE_VERIFICATION_REQUIRED   |
        | Unverified | —             | mua listing            | PHONE_VERIFICATION_REQUIRED   |
        | Unverified | —             | gửi tin nhắn           | PHONE_VERIFICATION_REQUIRED   |
        | Verified   | —             | niêm yết thẻ           | thành công                    |
        | Restricted | FRAUD         | mua pack               | ACCOUNT_RESTRICTED            |
        | Restricted | FRAUD         | mở pack đã mua         | thành công                    |
        | Restricted | FRAUD         | nạp Gem                | ACCOUNT_RESTRICTED            |
        | Restricted | FRAUD         | nhận thưởng ads        | ACCOUNT_RESTRICTED            |
        | Restricted | NEGATIVE_GEM  | nạp Gem                | thành công                    |
        | Restricted | NEGATIVE_GEM  | mua pack bằng Coin     | ACCOUNT_RESTRICTED            |
        | Restricted | bất kỳ        | xem bộ sưu tập         | thành công                    |
        | Banned     | —             | đăng nhập              | ACCOUNT_BANNED                |
```

---

## 3. Tính năng: EP-02 — Ví & Ledger

```gherkin
Tính năng: EP-02 — Ví & Ledger
  Bối cảnh chung:
    Cho trước tài khoản "P1" ở trạng thái Verified

  Quy tắc: BR-WAL-01 — Ledger bất biến, số dư bằng tổng bút toán

    @R1 @happy @tinhtoan
    Kịch bản: SC-WAL-03 — Số dư bằng tổng bút toán
      Cho trước ledger Coin của P1 gồm +1,000 (điểm danh), +110 (ads), −1,000 (mua pack)
      Khi P1 xem số dư
      Thì số dư Coin là 110

    @R1 @negative
    Kịch bản: SC-WAL-04 — Sửa bút toán đã ghi
      Cho trước bút toán "LE-1001" +1,000 Coin của P1
      Khi Super Admin gửi yêu cầu sửa số tiền của LE-1001 thành +2,000
      Thì server từ chối với mã lỗi LEDGER_IMMUTABLE
      Và LE-1001 giữ nguyên +1,000

    @R1 @happy
    Kịch bản: SC-WAL-05 — Điều chỉnh bằng bút toán đảo
      Cho trước bút toán "LE-1001" +1,000 Coin được cộng nhầm cho P1
      Khi việc điều chỉnh được duyệt theo maker-checker với mã ticket "T-55"
      Thì ledger của P1 có thêm bút toán −1,000 Coin tham chiếu LE-1001 và T-55
      Và LE-1001 không bị thay đổi

  Quy tắc: BR-WAL-02 — Nạp Gem chỉ sau khi xác thực receipt

    @R1 @happy @tichhop
    Kịch bản: SC-WAL-06 — Receipt hợp lệ
      Cho trước P1 có 0 Gem
      Khi app gửi receipt hợp lệ "GPA.1234" cho gói 500 Gem
      Thì số dư Gem của P1 là 500
      Và ledger có bút toán +500 Gem tham chiếu "GPA.1234"

    @R1 @idempotency
    Kịch bản: SC-WAL-01 — Receipt gửi trùng
      Cho trước receipt "GPA.1234" đã được cộng 500 Gem cho P1
      Khi app gửi lại receipt "GPA.1234"
      Thì số dư Gem của P1 không đổi
      Và server trả kết quả của lần xử lý đầu

    @R1 @negative
    Kịch bản: SC-WAL-07 — Receipt không hợp lệ
      Cho trước store trả về receipt "FAKE-1" không hợp lệ
      Khi app gửi receipt "FAKE-1"
      Thì server từ chối với mã lỗi RECEIPT_INVALID
      Và sự kiện được ghi vào log gian lận

    @R1 @tichhop
    Kịch bản: SC-WAL-08 — Store không phản hồi
      Cho trước API xác thực của store bị timeout
      Khi app gửi receipt "GPA.2000" cho gói 500 Gem
      Thì server trả trạng thái RECEIPT_PENDING
      Và số dư Gem của P1 không đổi
      Và receipt được thử xác thực lại trong vòng 24 giờ

    @R1 @tichhop
    Kịch bản: SC-WAL-09 — Xác thực lại thành công
      Cho trước receipt "GPA.2000" đang ở trạng thái chờ
      Khi lần thử lại xác thực với store thành công
      Thì số dư Gem của P1 tăng 500
      Và P1 nhận thông báo nạp thành công

  Quy tắc: BR-WAL-03 — Coin không âm; Gem chỉ âm do thu hồi

    @R1 @bien
    Kịch bản: SC-PACK-07 — Tiêu vừa hết Coin
      Cho trước P1 có 1,000 Coin
      Khi P1 mua pack "Awakening Standard" bằng Coin
      Thì số dư Coin của P1 là 0

    @R1 @negative
    Kịch bản: SC-PACK-06 — Không đủ Coin
      Cho trước P1 có 999 Coin
      Khi P1 mua pack bằng Coin
      Thì server từ chối với mã lỗi INSUFFICIENT_BALANCE
      Và số dư Coin của P1 vẫn là 999

    @R1 @negative
    Kịch bản: SC-WAL-10 — Gem âm không được dùng để mua
      Cho trước P1 có −400 Gem
      Khi P1 mua pack bằng Gem
      Thì server từ chối với mã lỗi ACCOUNT_RESTRICTED

  Quy tắc: BR-WAL-04 — Thu hồi Gem khi hoàn tiền

    @R1 @happy @tichhop
    Kịch bản: SC-WAL-11 — Hoàn tiền khi Gem còn đủ
      Cho trước P1 nạp 500 Gem qua giao dịch "GPA.3000" và hiện có 800 Gem
      Khi store thông báo hoàn tiền giao dịch "GPA.3000"
      Thì số dư Gem của P1 là 300
      Và trạng thái của P1 vẫn là "Verified"

    @R1 @bien @tichhop
    Kịch bản: SC-WAL-12 — Hoàn tiền khi Gem còn vừa đúng
      Cho trước P1 nạp 500 Gem qua giao dịch "GPA.3001" và hiện có 500 Gem
      Khi store thông báo hoàn tiền giao dịch "GPA.3001"
      Thì số dư Gem của P1 là 0
      Và trạng thái của P1 vẫn là "Verified"

    @R1 @negative @tichhop
    Kịch bản: SC-WAL-02 — Hoàn tiền khi Gem đã tiêu
      Cho trước P1 nạp 500 Gem và đã tiêu 400 Gem, còn 100 Gem
      Khi store thông báo hoàn tiền giao dịch 500 Gem đó
      Thì số dư Gem của P1 là −400
      Và P1 chuyển sang trạng thái "Restricted" với lý do NEGATIVE_GEM

    @R1 @trangthai
    Kịch bản: SC-WAL-13 — Tự gỡ hạn chế khi nạp bù đủ
      Cho trước P1 có −400 Gem và đang "Restricted" với lý do NEGATIVE_GEM
      Khi P1 nạp thành công 500 Gem
      Thì số dư Gem của P1 là 100
      Và trạng thái của P1 trở lại "Verified"

    @R1 @bien @trangthai
    Kịch bản: SC-WAL-14 — Nạp bù chưa đủ
      Cho trước P1 có −400 Gem và đang "Restricted" với lý do NEGATIVE_GEM
      Khi P1 nạp thành công 300 Gem
      Thì số dư Gem của P1 là −100
      Và trạng thái của P1 vẫn là "Restricted"

    @R1 @idempotency @tichhop
    Kịch bản: SC-WAL-15 — Thông báo hoàn tiền gửi trùng
      Cho trước thông báo hoàn tiền "GPA.3000" đã được xử lý
      Khi store gửi lại thông báo hoàn tiền "GPA.3000"
      Thì số dư Gem của P1 không đổi

  Quy tắc: BR-WAL-05 — Đổi Gem sang Coin một chiều

    @R2 @happy @tinhtoan @cho-Q05
    Kịch bản: SC-WAL-16 — Đổi 100 Gem
      Cho trước P1 có 100 Gem và 0 Coin
      Khi P1 đổi 100 Gem sang Coin
      Thì P1 có 0 Gem và 900 Coin

    @R2 @bien @cho-Q05
    Kịch bản: SC-WAL-17 — Đổi 1 Gem
      Cho trước P1 có 1 Gem
      Khi P1 đổi 1 Gem sang Coin
      Thì P1 nhận 9 Coin

    @R2 @negative
    Kịch bản: SC-WAL-18 — Đổi 0 Gem
      Khi P1 đổi 0 Gem sang Coin
      Thì server từ chối với mã lỗi INVALID_AMOUNT

    @R1 @happy
    Kịch bản: SC-WAL-19 — Đổi Coin sang Gem (CR-002: được phép)
      Cho trước P1 có 9,900 Coin và 0 Gem
      Khi P1 đổi 9,900 Coin sang Gem
      Thì P1 có 900 Gem và 0 Coin
```

---

## 4. Tính năng: Kinh tế chung

```gherkin
Tính năng: Kinh tế chung
  Quy tắc: BR-ECO-01 — Không rút tiền

    @R1 @negative
    Kịch bản: SC-ECO-01 — Yêu cầu rút Coin ra tiền thật
      Cho trước P1 có 50,000 Coin
      Khi P1 gửi yêu cầu rút 50,000 Coin
      Thì server trả mã lỗi NOT_SUPPORTED
      Và số dư Coin của P1 vẫn là 50,000

  Quy tắc: BR-ECO-02 — Không chuyển Gem/Coin trực tiếp

    @R1 @negative
    Kịch bản: SC-ECO-02 — Chuyển Coin cho người khác
      Cho trước P1 có 5,000 Coin
      Khi P1 gửi yêu cầu chuyển 1,000 Coin cho P2
      Thì server trả mã lỗi NOT_SUPPORTED
      Và số dư của P1 và P2 không đổi

  Quy tắc: BR-ECO-03 — Tham số có version và thời điểm hiệu lực

    @R1 @bien
    Sơ đồ kịch bản: SC-ECO-03 — Đổi thưởng video từ 8 xuống 6 Coin
      Cho trước thưởng video là 8 Coin, version mới 6 Coin hiệu lực từ 2026-11-01 00:00:00
      Khi ad network gửi SSV hợp lệ cho lượt xem hoàn thành lúc <thời điểm>
      Thì P1 nhận <thưởng> Coin

      Ví dụ:
        | thời điểm           | thưởng |
        | 2026-10-31 23:59:59 | 8      |
        | 2026-11-01 00:00:00 | 6      |

    @R1 @negative
    Kịch bản: SC-ECO-04 — Đặt thời điểm hiệu lực trong quá khứ
      Khi Economy Manager tạo version thưởng mới có hiệu lực từ 2026-10-01
      Thì server từ chối với mã lỗi EFFECTIVE_TIME_IN_PAST

  Quy tắc: BR-ECO-04 — Trần chi phí thưởng ads 50% doanh thu ads

    @R1 @tinhtoan @cho-Q04
    Sơ đồ kịch bản: SC-ECO-05 — Kiểm soát chi phí thưởng trong tháng
      Cho trước doanh thu ads thực nhận từ đầu tháng là $10,000
      Và chi phí thưởng ads quy đổi từ đầu tháng là <chi phí>
      Khi job kiểm soát kinh tế chạy
      Thì kết quả là <kết quả>

      Ví dụ:
        | chi phí | kết quả                                                         |
        | $4,499  | không có cảnh báo                                               |
        | $4,500  | gửi cảnh báo cho Economy Manager                                |
        | $5,000  | chuyển sang bảng thưởng "giảm" đã cấu hình đến hết tháng        |
```

---

## 5. Tính năng: EP-03/EP-04 — Mua và mở pack

```gherkin
Tính năng: EP-03/EP-04 — Mua và mở pack
  Bối cảnh chung:
    Cho trước Pack Definition "Awakening Standard" đang bán, giá 1,000 Coin hoặc 100 Gem, 5 thẻ/pack
    Và tài khoản "P1" ở trạng thái Verified

  Quy tắc: BR-PACK-01 — Tỷ lệ công khai khớp với tỷ lệ server dùng

    @R1 @happy
    Kịch bản: SC-PACK-10 — Mua theo version tỷ lệ đã xem
      Cho trước version tỷ lệ đang bán là "v1"
      Khi P1 mua pack kèm mã version "v1" đã xem
      Thì Pack Instance được tạo với version "v1"

    @R1 @bien
    Kịch bản: SC-PACK-11 — Version tỷ lệ đổi giữa lúc xem và lúc mua
      Cho trước P1 xem tỷ lệ version "v1"
      Và version "v2" có hiệu lực trước khi P1 xác nhận mua
      Khi P1 mua pack kèm mã version "v1"
      Thì server từ chối với mã lỗi ODDS_CHANGED
      Và phản hồi chứa tỷ lệ version "v2"
      Và không có tiền nào bị trừ

  Quy tắc: BR-PACK-02 — Kết quả do server quyết định trước animation

    @R1 @happy
    Kịch bản: SC-PACK-01 — Skip không đổi kết quả
      Cho trước P1 có 1 Pack Instance "Unopened"
      Và server đã quay kết quả gồm 4 Common và 1 Epic khi P1 mở pack
      Khi P1 chọn skip animation
      Thì bộ sưu tập của P1 có thêm đúng 4 thẻ Common và 1 thẻ Epic đã quay
      Và Pack Instance ở trạng thái "Opened"

    @R1 @bien
    Kịch bản: SC-PACK-02 — Mất kết nối sau khi server đã quay
      Cho trước server đã ghi kết quả mở pack của P1
      Khi kết nối của P1 bị mất trước khi animation kết thúc
      Thì kết quả đã ghi không thay đổi
      Và lần mở app tiếp theo P1 nhận lại kết quả của pack đó

    @R1 @bien
    Kịch bản: SC-PACK-03 — Mất kết nối trước khi server nhận yêu cầu mở
      Cho trước P1 có 1 Pack Instance "Unopened"
      Khi yêu cầu mở pack không tới được server
      Thì Pack Instance vẫn ở trạng thái "Unopened"
      Và không có thẻ mới nào được thêm

    @R1 @negative @trangthai
    Kịch bản: SC-PACK-04 — Mở lại pack đã mở
      Cho trước Pack Instance của P1 ở trạng thái "Opened"
      Khi P1 gửi lại yêu cầu mở pack đó
      Thì server trả lại đúng kết quả đã ghi lần đầu với mã PACK_ALREADY_OPENED
      Và không có thẻ mới nào được thêm

    @R1 @unauthorized
    Kịch bản: SC-PACK-12 — Mở pack của người khác
      Cho trước Pack Instance "PI-77" thuộc P2
      Khi P1 gửi yêu cầu mở PI-77
      Thì server từ chối với mã lỗi PACK_NOT_OWNED

    @R1 @dongthoi
    Kịch bản: SC-PACK-13 — Hai yêu cầu mở cùng một pack đồng thời
      Cho trước P1 có Pack Instance "PI-78" ở trạng thái "Unopened"
      Khi hai yêu cầu mở PI-78 tới server cùng lúc
      Thì chỉ một lần quay được thực hiện
      Và cả hai yêu cầu nhận cùng một kết quả

  Quy tắc: US-03.1 — Mua pack idempotent

    @R1 @idempotency
    Kịch bản: SC-PACK-05 — Gửi trùng yêu cầu mua
      Cho trước P1 có 2,500 Coin
      Khi P1 gửi 2 yêu cầu mua cùng idempotency key "K-1"
      Thì số dư Coin của P1 là 1,500
      Và P1 có đúng 1 Pack Instance mới

    @R1 @happy
    Kịch bản: SC-PACK-14 — Hai lần mua khác idempotency key
      Cho trước P1 có 2,500 Coin
      Khi P1 gửi 2 yêu cầu mua với key "K-1" và "K-2"
      Thì số dư Coin của P1 là 500
      Và P1 có 2 Pack Instance mới

    @R1 @negative
    Kịch bản: SC-PACK-15 — Mua pack đã ngừng bán
      Cho trước Pack Definition "Promo Halloween" đã ngừng bán
      Khi P1 mua "Promo Halloween"
      Thì server từ chối với mã lỗi PACK_NOT_ON_SALE

  Quy tắc: BR-PACK-03 — Tỷ lệ rơi theo slot

    @R1 @tinhtoan @cho-CF01
    Sơ đồ kịch bản: SC-PACK-16 — Kiểm định thống kê bộ quay
      Cho trước version tỷ lệ có <rarity> là <tỷ lệ>
      Khi bộ quay mô phỏng 1,000,000 slot
      Thì tỷ lệ <rarity> quan sát được nằm trong khoảng <khoảng tin cậy 99%>

      Ví dụ:
        | rarity      | tỷ lệ | khoảng tin cậy 99%  |
        | Legendary   | 4%    | 3.950% – 4.050%     |
        | Secret Rare | 1%    | 0.974% – 1.026%     |
        | Epic        | 10%   | 9.923% – 10.077%    |

  Quy tắc: BR-PACK-04 — Snapshot version tỷ lệ

    @R1 @happy
    Kịch bản: SC-PACK-08 — Tỷ lệ đổi sau khi mua
      Cho trước P1 mua pack khi version tỷ lệ là "v1"
      Và version "v2" có hiệu lực sau đó
      Khi P1 mở pack đã mua
      Thì server quay kết quả theo "v1"
      Và bản ghi mở pack lưu version "v1"

    @R1 @bien
    Kịch bản: SC-PACK-17 — Mua đúng thời điểm version mới có hiệu lực
      Cho trước version "v2" có hiệu lực từ 2026-11-01 00:00:00
      Khi P1 mua pack lúc 2026-11-01 00:00:00
      Thì Pack Instance được tạo với version "v2"

    @R1 @happy
    Kịch bản: SC-PACK-18 — Mở pack thuộc Pack Definition đã ngừng bán
      Cho trước P1 có Pack Instance "Unopened" của "Promo Halloween" mua khi còn bán
      Và "Promo Halloween" đã ngừng bán
      Khi P1 mở pack đó
      Thì server quay kết quả theo version đã lưu khi mua

  Quy tắc: BR-PACK-05 — Pity

    @R1 @tinhtoan @cho-Q09
    Sơ đồ kịch bản: SC-PACK-09 — Bộ đếm pity
      Cho trước bộ đếm pity của P1 cho "Awakening Standard" là <trước>
      Khi P1 mở một pack có kết quả quay ngẫu nhiên <kết quả quay>
      Thì pack chứa <bảo đảm>
      Và bộ đếm pity sau khi mở là <sau>

      Ví dụ:
        | trước | kết quả quay        | bảo đảm                    | sau |
        | 0     | không có Legendary+ | không áp dụng              | 1   |
        | 48    | không có Legendary+ | không áp dụng              | 49  |
        | 49    | không có Legendary+ | ít nhất 1 Legendary        | 0   |
        | 49    | có 1 Secret Rare    | Secret Rare giữ nguyên     | 0   |
        | 12    | có 1 Legendary      | không áp dụng              | 0   |

    @R1 @negative @cho-Q09
    Kịch bản: SC-PACK-19 — Pity không dùng chung giữa các loại pack
      Cho trước bộ đếm pity của P1 cho "Awakening Standard" là 49
      Và bộ đếm pity của P1 cho "Awakening Premium" là 3
      Khi P1 mở một pack "Awakening Premium" không có Legendary+
      Thì không có bảo đảm Legendary nào được áp dụng
      Và bộ đếm "Awakening Premium" là 4
      Và bộ đếm "Awakening Standard" vẫn là 49

    @R1 @happy
    Kịch bản: SC-PACK-20 — Xem bộ đếm pity
      Cho trước bộ đếm pity của P1 cho "Awakening Standard" là 17
      Khi P1 xem thông tin pack "Awakening Standard"
      Thì phản hồi chứa bộ đếm pity 17 và ngưỡng 50

  Quy tắc: BR-PACK-06 — Lật theo thứ tự rarity tăng dần

    @R1 @happy
    Kịch bản: SC-PACK-21 — Thứ tự lật
      Cho trước server quay được Epic, Common, Rare, Common, Legendary
      Khi P1 mở pack
      Thì thứ tự lật trả về là Common, Common, Rare, Epic, Legendary

  Quy tắc: BR-PACK-07 — Climax chỉ khi có Epic trở lên

    @R1 @bien
    Sơ đồ kịch bản: SC-PACK-22 — Kích hoạt Climax
      Cho trước thẻ hiếm nhất trong pack là <rarity cao nhất>
      Khi P1 mở pack
      Thì cờ Climax của lần mở là <climax>

      Ví dụ:
        | rarity cao nhất | climax |
        | Common          | không  |
        | Rare            | không  |
        | Epic            | có     |
        | Legendary       | có     |
        | Secret Rare     | có     |

  Quy tắc: BR-PACK-08 — Skip luôn khả dụng

    @R1 @bien
    Kịch bản: SC-PACK-23 — Skip khi có Secret Rare
      Cho trước server quay được 1 Secret Rare trong pack của P1
      Khi P1 chọn skip animation
      Thì P1 đi thẳng tới phần tổng kết
      Và Secret Rare có trong bộ sưu tập của P1

  Quy tắc: BR-PACK-09 — Số thẻ mỗi pack

    @R1 @happy @cho-Q08
    Kịch bản: SC-PACK-24 — Pack có đúng 5 thẻ
      Khi P1 mở một pack "Awakening Standard"
      Thì kết quả có đúng 5 Card Instance mới

  Quy tắc: Vòng đời Pack Instance (BRD mục 8.3)

    @R1 @trangthai @cho-Q12
    Sơ đồ kịch bản: SC-PACK-25 — Pack khi giao dịch Gem gốc bị hoàn tiền
      Cho trước P1 mua pack bằng Gem từ giao dịch nạp "GPA.4000"
      Và Pack Instance ở trạng thái <trạng thái>
      Khi store thông báo hoàn tiền "GPA.4000"
      Thì Pack Instance ở trạng thái <sau>

      Ví dụ:
        | trạng thái | sau      |
        | Unopened   | Revoked  |
        | Opened     | Opened   |
```

---

## 6. Tính năng: EP-05 — Bộ sưu tập

```gherkin
Tính năng: EP-05 — Bộ sưu tập
  Bối cảnh chung:
    Cho trước set "Awakening" có 100 Card Definition (@cho-CF06)

  Quy tắc: US-05.2 — Tiến độ set đếm Card Definition khác nhau

    @R1 @tinhtoan
    Kịch bản: SC-COL-01 — Bản trùng không cộng tiến độ
      Cho trước P1 sở hữu 3 bản "Seraphel" và 1 bản "Nocturne"
      Khi P1 xem tiến độ set "Awakening"
      Thì tiến độ là "2/100"

    @R1 @bien
    Kịch bản: SC-COL-02 — Hoàn thành set
      Cho trước P1 sở hữu 99 Card Definition khác nhau của set "Awakening"
      Khi P1 mở pack và nhận Card Definition thứ 100 còn thiếu
      Thì tiến độ là "100/100"
      Và sự kiện "hoàn thành set" được ghi nhận

    @R2 @bien
    Kịch bản: SC-COL-03 — Bán bản duy nhất làm giảm tiến độ
      Cho trước P1 sở hữu đúng 1 bản "Nocturne" và tiến độ là "45/100"
      Khi giao dịch bán "Nocturne" của P1 hoàn tất
      Thì tiến độ là "44/100"

  Quy tắc: US-05.4 — Story Fragment chỉ mở với thẻ đã sở hữu

    @R1 @happy
    Kịch bản: SC-COL-04 — Đọc story thẻ đã sở hữu
      Cho trước P1 sở hữu "Seraphel, the Hopebringer"
      Khi P1 yêu cầu Story Fragment của "Seraphel"
      Thì phản hồi chứa toàn văn Story Fragment

    @R1 @negative
    Kịch bản: SC-COL-05 — Đọc story thẻ chưa từng sở hữu
      Cho trước P1 chưa từng sở hữu "The Nameless"
      Khi P1 gửi trực tiếp yêu cầu Story Fragment của "The Nameless"
      Thì server từ chối với mã lỗi CARD_NOT_COLLECTED

  Quy tắc: NFR-10 — Xem offline

    @R1 @bien
    Kịch bản: SC-COL-06 — Mở bộ sưu tập khi không có mạng
      Cho trước bộ sưu tập của P1 được đồng bộ lần cuối lúc 08:00 với 120 thẻ
      Khi P1 xem bộ sưu tập lúc không có mạng
      Thì P1 thấy 120 thẻ kèm thời điểm đồng bộ 08:00
      Và mọi yêu cầu mua, bán, mở pack bị từ chối với mã lỗi OFFLINE
```

---

## 7. Tính năng: EP-07 — Điểm danh

```gherkin
Tính năng: EP-07 — Điểm danh
  Bối cảnh chung:
    Cho trước tài khoản "P2" có múi giờ Asia/Ho_Chi_Minh, thiết bị hợp lệ

  Quy tắc: BR-CHK-01 — Một lần mỗi ngày theo múi giờ tài khoản

    @R1 @negative
    Kịch bản: SC-CHK-03 — Điểm danh lần hai trong ngày
      Cho trước P2 đã điểm danh ngày 2026-10-12
      Khi P2 điểm danh lần nữa trong ngày 2026-10-12
      Thì server từ chối với mã lỗi ALREADY_CHECKED_IN
      Và số dư Coin không đổi

    @R1 @bien
    Kịch bản: SC-CHK-02 — 23:59 và 00:01
      Cho trước P2 điểm danh lúc 23:59 ngày 2026-10-11 (giờ tài khoản)
      Khi P2 điểm danh lúc 00:01 ngày 2026-10-12 (giờ tài khoản)
      Thì lần điểm danh thứ hai được chấp nhận là ngày streak kế tiếp

    @R1 @negative
    Kịch bản: SC-CHK-07 — Đổi múi giờ thiết bị để điểm danh thêm
      Cho trước P2 đã điểm danh lúc 20:00 ngày 2026-10-12 giờ Việt Nam
      Và thiết bị của P2 được đổi sang múi giờ UTC+14, hiển thị ngày 2026-10-13
      Khi P2 điểm danh
      Thì server từ chối với mã lỗi ALREADY_CHECKED_IN

  Quy tắc: BR-CHK-02 / BR-CHK-03 — Thưởng ngày và mốc streak

    @R1 @tinhtoan
    Sơ đồ kịch bản: SC-CHK-08 — Thưởng theo ngày streak
      Cho trước streak hiện tại của P2 là <trước>
      Khi P2 điểm danh ngày kế tiếp
      Thì P2 nhận <coin> Coin
      Và streak là <sau>

      Ví dụ:
        | trước | sau | coin  | giải thích                              |
        | 0     | 1   | 20    | ngày 1                                  |
        | 6     | 7   | 150   | ngày 7                                  |
        | 7     | 8   | 20    | chu kỳ mới, ngày 1 (@cho-Q13)           |
        | 13    | 14  | 350   | 150 (ngày 7 chu kỳ 2) + 200 mốc 14      |
        | 29    | 30  | 1,030 | 30 (ngày 2 chu kỳ 5) + 1,000 mốc 30     |
        | 99    | 100 | 5,030 | 30 (ngày 2 chu kỳ 15) + 5,000 mốc 100   |

    @R1 @happy
    Kịch bản: SC-CHK-09 — Nhận thẻ độc quyền ở mốc 100 ngày
      Cho trước streak hiện tại của P2 là 99
      Khi P2 điểm danh ngày kế tiếp
      Thì P2 nhận 1 Card Instance độc quyền được đánh dấu soulbound

    @R1 @bien
    Kịch bản: SC-CHK-10 — Nhận lại mốc ở chuỗi streak mới
      Cho trước P2 đã nhận mốc 14 ngày ở một chuỗi trước, sau đó streak bị reset
      Và streak hiện tại là 13
      Khi P2 điểm danh ngày kế tiếp
      Thì P2 nhận thêm 200 Coin của mốc 14 ngày

    @R1 @tinhtoan
    Kịch bản: SC-CHK-01 — Ngày 7
      Cho trước P2 đã điểm danh 6 ngày liên tiếp đến ngày 2026-10-10
      Khi P2 điểm danh ngày 2026-10-11
      Thì P2 nhận 150 Coin
      Và streak của P2 là 7

  Quy tắc: BR-CHK-04 — Bỏ lỡ 1 ngày thì reset

    @R1 @negative
    Kịch bản: SC-CHK-04 — Bỏ lỡ một ngày, không có Freeze
      Cho trước streak của P2 là 5 và P2 không có Streak Freeze
      Và P2 không điểm danh ngày 2026-10-13
      Khi P2 điểm danh ngày 2026-10-14
      Thì streak của P2 là 1
      Và P2 nhận 20 Coin

  Quy tắc: BR-CHK-05 / BR-CHK-06 — Streak Freeze

    @R1 @happy
    Kịch bản: SC-CHK-05 — Bỏ lỡ một ngày, có Freeze
      Cho trước streak của P2 là 5 và P2 có 1 Streak Freeze
      Và P2 không điểm danh ngày 2026-10-13
      Khi P2 điểm danh ngày 2026-10-14
      Thì streak của P2 là 6
      Và P2 còn 0 Streak Freeze

    @R1 @bien
    Kịch bản: SC-CHK-06 — Bỏ lỡ hai ngày, chỉ có một Freeze
      Cho trước streak của P2 là 5 và P2 có 1 Streak Freeze
      Và P2 không điểm danh ngày 2026-10-13 và 2026-10-14
      Khi P2 điểm danh ngày 2026-10-15
      Thì streak của P2 là 1
      Và P2 vẫn còn 1 Streak Freeze

    @R1 @happy @cho-Q15
    Kịch bản: SC-CHK-11 — Mua Freeze bằng Coin
      Cho trước P2 có 500 Coin và 0 Streak Freeze
      Khi P2 mua 1 Streak Freeze bằng Coin
      Thì P2 có 300 Coin và 1 Streak Freeze

    @R1 @bien @cho-Q15
    Kịch bản: SC-CHK-12 — Mua Freeze khi đã giữ tối đa
      Cho trước P2 có 2 Streak Freeze
      Khi P2 mua thêm 1 Streak Freeze
      Thì server từ chối với mã lỗi FREEZE_LIMIT
      Và số dư Coin không đổi

    @R1 @negative
    Kịch bản: SC-CHK-13 — Không đủ Coin mua Freeze
      Cho trước P2 có 199 Coin
      Khi P2 mua 1 Streak Freeze bằng Coin
      Thì server từ chối với mã lỗi INSUFFICIENT_BALANCE

    @R1 @bien @cho-Q15
    Kịch bản: SC-CHK-14 — Lấy Freeze bằng ads khi chỉ còn 2 lượt ads trong ngày
      Cho trước P2 đã dùng 8/10 lượt ads hôm nay
      Khi P2 yêu cầu lấy Streak Freeze bằng 3 ads
      Thì server từ chối với mã lỗi FREEZE_AD_LIMIT

  Quy tắc: BR-FRD-01 — Thiết bị không hợp lệ

    @R1 @negative @cho-Q07
    Kịch bản: SC-CHK-15 — Điểm danh trên thiết bị đã root
      Cho trước thiết bị của P2 bị phát hiện đã root
      Khi P2 điểm danh
      Thì server từ chối với mã lỗi DEVICE_NOT_ELIGIBLE
      Và streak của P2 không tăng
```

---

## 8. Tính năng: EP-07/EP-09 — Rewarded ads

```gherkin
Tính năng: EP-07/EP-09 — Rewarded ads
  Bối cảnh chung:
    Cho trước tài khoản "P3" Verified, SĐT Việt Nam (Tier 2), thiết bị hợp lệ, múi giờ Asia/Ho_Chi_Minh

  Quy tắc: BR-ADS-01 — Tối đa 10 lượt có thưởng/ngày

    @R1 @bien
    Kịch bản: SC-ADS-07 — Lượt thứ 10
      Cho trước P3 đã hoàn thành 9 lượt video trong ngày
      Khi ad network gửi SSV hợp lệ cho lượt video thứ 10
      Thì P3 nhận 28 Coin (8 thưởng + 20 bonus)

    @R1 @negative
    Kịch bản: SC-ADS-02 — Lượt thứ 11
      Cho trước P3 đã hoàn thành 10 lượt có thưởng trong ngày
      Khi P3 yêu cầu xem lượt thứ 11
      Thì server từ chối với mã lỗi DAILY_AD_LIMIT_REACHED

    @R1 @bien @cho-Q16
    Kịch bản: SC-ADS-08 — Survey không tính vào giới hạn
      Cho trước P3 đã hoàn thành 10 lượt video trong ngày
      Khi P3 hoàn thành một survey có thưởng 50 Coin và SSV hợp lệ
      Thì P3 nhận 50 Coin

  Quy tắc: BR-ADS-02 — Cooldown 60 giây

    @R1 @bien
    Kịch bản: SC-ADS-03 — 59 giây
      Cho trước lượt ad gần nhất của P3 được ghi nhận cách đây 59 giây
      Khi P3 yêu cầu xem ad mới
      Thì server từ chối với mã lỗi AD_COOLDOWN
      Và phản hồi nêu thời gian chờ còn lại là 1 giây

    @R1 @bien
    Kịch bản: SC-ADS-09 — Đúng 60 giây
      Cho trước lượt ad gần nhất của P3 được ghi nhận cách đây 60 giây
      Khi P3 yêu cầu xem ad mới
      Thì yêu cầu được chấp nhận

  Quy tắc: BR-ADS-03 — Reset lúc 00:00 giờ tài khoản

    @R1 @bien
    Sơ đồ kịch bản: SC-ADS-10 — Reset giới hạn ngày
      Cho trước P3 đã hoàn thành 10 lượt ads trong ngày 2026-10-11
      Khi P3 yêu cầu xem ad lúc <thời điểm> giờ tài khoản
      Thì kết quả là <kết quả>

      Ví dụ:
        | thời điểm           | kết quả                 |
        | 2026-10-11 23:59:59 | DAILY_AD_LIMIT_REACHED  |
        | 2026-10-12 00:00:00 | được chấp nhận          |

  Quy tắc: BR-ADS-04 — Chỉ cộng khi có SSV hợp lệ, mỗi transaction một lần

    @R1 @happy @tichhop
    Kịch bản: SC-ADS-11 — SSV hợp lệ
      Cho trước P3 chưa xem ad nào hôm nay
      Khi ad network gửi SSV hợp lệ cho ad transaction "TX-100"
      Thì P3 nhận 8 Coin
      Và số lượt ads hôm nay của P3 là 1

    @R1 @idempotency @tichhop
    Kịch bản: SC-ADS-04 — SSV gửi trùng
      Cho trước P3 đã được cộng thưởng cho ad transaction "TX-881"
      Khi ad network gửi lại SSV cho "TX-881"
      Thì số dư Coin của P3 không đổi
      Và server trả kết quả thành công của lần xử lý đầu

    @R1 @negative @tichhop
    Kịch bản: SC-ADS-12 — Chữ ký SSV sai
      Khi server nhận SSV cho "TX-200" có chữ ký không hợp lệ
      Thì server từ chối với mã lỗi SIGNATURE_INVALID
      Và không có Coin nào được cộng
      Và sự kiện được ghi vào log gian lận

    @R1 @tichhop
    Kịch bản: SC-ADS-05 — Client báo xem xong nhưng không có SSV
      Cho trước P3 báo đã xem xong một video
      Khi không có SSV hợp lệ nào cho lượt đó trong 10 phút
      Thì P3 không được cộng Coin
      Và lượt đó không tính vào giới hạn 10 lượt

  Quy tắc: BR-ADS-05 — Bonus lượt thứ 5 và thứ 10

    @R1 @tinhtoan
    Kịch bản: SC-ADS-01 — Lượt thứ 5
      Cho trước P3 đã hoàn thành 4 lượt video trong ngày
      Khi ad network gửi SSV hợp lệ cho lượt video thứ 5
      Thì P3 nhận 18 Coin (8 thưởng + 10 bonus)

    @R1 @tinhtoan
    Kịch bản: SC-ADS-13 — Tổng một ngày xem đủ 10 video
      Cho trước P3 chưa xem ad nào hôm nay
      Khi P3 hoàn thành 10 lượt video có SSV hợp lệ trong ngày
      Thì tổng Coin từ ads hôm nay của P3 là 110

    @R1 @tinhtoan
    Kịch bản: SC-ADS-14 — Playable ở lượt thứ 5
      Cho trước P3 đã hoàn thành 4 lượt video trong ngày
      Khi ad network gửi SSV hợp lệ cho một playable ở lượt thứ 5
      Thì P3 nhận 25 Coin (15 thưởng + 10 bonus)

  Quy tắc: BR-ADS-06 / BR-FRD-02 — Thưởng theo tier của SĐT, không theo IP

    @R1 @tinhtoan @cho-Q04
    Sơ đồ kịch bản: SC-ADS-15 — Xác định tier khi tính thưởng
      Cho trước bảng thưởng video: Tier 1 = <t1>, Tier 2 = <t2>
      Và SĐT đã xác thực của tài khoản thuộc <quốc gia SĐT>
      Và IP của lượt xem thuộc <quốc gia IP>
      Khi ad network gửi SSV hợp lệ cho lượt xem đầu tiên trong ngày
      Thì tài khoản nhận <thưởng> Coin

      Ví dụ:
        | t1 | t2 | quốc gia SĐT | quốc gia IP     | thưởng | lý do                        |
        | 8  | 8  | Việt Nam     | Việt Nam        | 8      | mặc định cùng mức            |
        | 10 | 3  | Việt Nam     | Việt Nam        | 3      | Tier 2                       |
        | 10 | 3  | Việt Nam     | Mỹ (qua VPN)    | 3      | theo SĐT, không theo IP      |
        | 10 | 3  | Mỹ           | Mỹ              | 10     | Tier 1                       |

  Quy tắc: BR-FRD-01 — Thiết bị không hợp lệ

    @R1 @negative
    Kịch bản: SC-ADS-06 — Thiết bị đã root
      Cho trước thiết bị của P3 bị phát hiện đã root
      Khi ad network gửi SSV hợp lệ cho một lượt video
      Thì P3 không được cộng Coin
      Và sự kiện được ghi vào log gian lận
```

---

## 9. Tính năng: EP-07 — Referral

```gherkin
Tính năng: EP-07 — Referral
  Bối cảnh chung:
    Cho trước "R1" là Verified Player có mã mời "ANIMA-R1"

  Quy tắc: BR-REF-01 — Thưởng khi người được mời đạt mốc

    @R2 @happy @cho-Q18
    Kịch bản: SC-REF-01 — Người được mời đạt mốc
      Cho trước "N1" đăng ký bằng mã "ANIMA-R1" và đã xác thực SĐT
      Và N1 đã điểm danh 6 ngày khác nhau và mở 3 pack
      Khi N1 điểm danh ngày khác nhau thứ 7
      Thì R1 nhận 500 Coin

    @R2 @bien @cho-Q18
    Kịch bản: SC-REF-02 — Đủ ngày nhưng thiếu pack
      Cho trước N1 dùng mã "ANIMA-R1", đã xác thực, đã điểm danh 7 ngày khác nhau và mở 2 pack
      Khi job kiểm tra referral chạy
      Thì R1 không nhận thưởng cho N1

    @R2 @negative
    Kịch bản: SC-REF-03 — Người được mời chưa xác thực SĐT
      Cho trước N2 dùng mã "ANIMA-R1", chưa xác thực SĐT, đã điểm danh 7 ngày và mở 3 pack
      Khi job kiểm tra referral chạy
      Thì R1 không nhận thưởng cho N2

    @R2 @negative
    Kịch bản: SC-REF-04 — Tự nhập mã của chính mình
      Khi R1 nhập mã mời "ANIMA-R1" cho tài khoản của mình
      Thì server từ chối với mã lỗi SELF_REFERRAL_FORBIDDEN

  Quy tắc: BR-REF-02 — Tối đa 20 lượt/tháng

    @R2 @bien
    Sơ đồ kịch bản: SC-REF-05 — Giới hạn tháng
      Cho trước R1 đã nhận thưởng <đã nhận> lượt referral trong tháng 10/2026
      Khi thêm một người được mời của R1 đạt mốc trong tháng 10/2026
      Thì R1 nhận <thưởng> Coin

      Ví dụ:
        | đã nhận | thưởng |
        | 19      | 500    |
        | 20      | 0      |

  Quy tắc: BR-REF-03 — Không thưởng khi có dấu hiệu cùng một người

    @R2 @negative
    Kịch bản: SC-REF-06 — Cùng fingerprint thiết bị
      Cho trước N3 dùng mã "ANIMA-R1" và có fingerprint thiết bị trùng với một thiết bị R1 từng dùng
      Và N3 đã đạt mốc hoạt động
      Khi job kiểm tra referral chạy
      Thì R1 không nhận thưởng cho N3
      Và cặp R1–N3 được gắn cờ cho Fraud Analyst
```

---

## 10. Tính năng: EP-06 — Chợ giao dịch

```gherkin
Tính năng: EP-06 — Chợ giao dịch
  Bối cảnh chung:
    Cho trước Seller "S1" và Buyer "B1" đều Verified, không bị hạn chế
    Và khoảng giá cho thẻ Rare là 100 – 50,000 Coin

  Quy tắc: BR-MKT-01 — Điều kiện tham gia chợ

    @R2 @unauthorized
    Kịch bản: SC-MKT-04 — Player chưa xác thực mua thẻ
      Cho trước tài khoản "U1" chưa xác thực SĐT
      Khi U1 gửi yêu cầu mua trực tiếp tới API chợ
      Thì server từ chối với mã lỗi PHONE_VERIFICATION_REQUIRED
      Và không có bút toán nào được ghi

    @R2 @unauthorized
    Kịch bản: SC-MKT-07 — Tài khoản bị hạn chế niêm yết
      Cho trước S1 ở trạng thái "Restricted"
      Khi S1 niêm yết một thẻ Rare giá 1,000 Coin
      Thì server từ chối với mã lỗi ACCOUNT_RESTRICTED

  Quy tắc: BR-MKT-02 — Thẻ đang niêm yết bị khóa

    @R2 @negative
    Kịch bản: SC-MKT-08 — Niêm yết lại thẻ đang niêm yết
      Cho trước thẻ "Tidemourn #0101" của S1 đang ở trạng thái "Listed"
      Khi S1 niêm yết "Tidemourn #0101" lần nữa
      Thì server từ chối với mã lỗi CARD_LOCKED

    @R2 @negative @trangthai
    Kịch bản: SC-MKT-09 — Đưa thẻ đang niêm yết vào đấu giá
      Cho trước thẻ Epic "Nocturne #0042" của S1 đang "Listed"
      Khi S1 tạo phiên đấu giá cho "Nocturne #0042"
      Thì server từ chối với mã lỗi CARD_LOCKED

  Quy tắc: BR-MKT-03 — Thẻ soulbound không giao dịch

    @R2 @negative
    Kịch bản: SC-MKT-03 — Niêm yết thẻ độc quyền streak 100 ngày
      Cho trước S1 sở hữu thẻ soulbound nhận từ mốc streak 100 ngày
      Khi S1 niêm yết thẻ đó
      Thì server từ chối với mã lỗi CARD_NOT_TRADABLE

  Quy tắc: BR-MKT-04 — Khoảng giá theo rarity

    @R2 @bien @cho-Q19
    Sơ đồ kịch bản: SC-MKT-10 — Giá niêm yết thẻ Rare
      Khi S1 niêm yết một thẻ Rare với giá <giá> Coin
      Thì kết quả là <kết quả>

      Ví dụ:
        | giá    | kết quả             |
        | 99     | PRICE_OUT_OF_RANGE  |
        | 100    | thành công          |
        | 50,000 | thành công          |
        | 50,001 | PRICE_OUT_OF_RANGE  |

  Quy tắc: BR-MKT-05 — Phí thu từ Seller, làm tròn lên

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-MKT-01 — Tính phí
      Cho trước S1 bán thẻ qua <hình thức> với giá <giá>
      Khi giao dịch hoàn tất
      Thì phí là <phí> và S1 nhận <nhận>
      Và B1 bị trừ đúng <giá>

      Ví dụ:
        | hình thức   | giá         | phí | nhận  |
        | giá cố định | 1,000 Coin  | 50  | 950   |
        | giá cố định | 999 Coin    | 50  | 949   |
        | giá cố định | 10 Coin     | 1   | 9     |
        | đấu giá     | 2,345 Gem   | 235 | 2,110 |

  Quy tắc: BR-MKT-06 — Một loại tiền mỗi listing

    @R2 @negative
    Kịch bản: SC-MKT-11 — Mua listing Gem khi chỉ có Coin
      Cho trước S1 niêm yết thẻ giá 300 Gem
      Và B1 có 0 Gem và 100,000 Coin
      Khi B1 mua listing đó
      Thì server từ chối với mã lỗi INSUFFICIENT_BALANCE
      Và số Coin của B1 không bị trừ

  Quy tắc: US-06.2 — Mua nguyên tử và đồng thời

    @R2 @happy
    Kịch bản: SC-MKT-12 — Mua thành công
      Cho trước S1 niêm yết "Tidemourn #0101" giá 1,000 Coin
      Và B1 có 5,000 Coin, S1 có 0 Coin
      Khi B1 mua listing đó
      Thì B1 có 4,000 Coin và sở hữu "Tidemourn #0101"
      Và S1 có 950 Coin
      Và listing ở trạng thái "Sold"

    @R2 @dongthoi
    Kịch bản: SC-MKT-02 — Hai người mua cùng lúc
      Cho trước S1 niêm yết "Nocturne #0042" giá 3,000 Coin
      Và B1 và B2 đều có 5,000 Coin
      Khi B1 và B2 cùng gửi yêu cầu mua cùng một thời điểm
      Thì đúng một người mua thành công và nhận thẻ
      Và người còn lại nhận mã lỗi LISTING_NOT_AVAILABLE và số dư giữ nguyên 5,000 Coin

    @R2 @negative
    Kịch bản: SC-MKT-13 — Tự mua listing của mình
      Cho trước S1 niêm yết một thẻ giá 1,000 Coin
      Khi S1 mua listing đó
      Thì server từ chối với mã lỗi SELF_PURCHASE_FORBIDDEN

    @R2 @idempotency
    Kịch bản: SC-MKT-14 — Gửi trùng yêu cầu mua
      Cho trước S1 niêm yết một thẻ giá 1,000 Coin và B1 có 5,000 Coin
      Khi B1 gửi 2 yêu cầu mua listing đó cùng idempotency key "M-1"
      Thì B1 có 4,000 Coin
      Và cả hai yêu cầu nhận cùng một kết quả thành công

  Quy tắc: Vòng đời Listing (BRD mục 8.4)

    @R2 @trangthai
    Sơ đồ kịch bản: SC-MKT-15 — Chuyển trạng thái listing
      Cho trước listing "L-1" của S1 ở trạng thái <từ>, hết hạn lúc 2026-10-10 12:00:00
      Khi <sự kiện>
      Thì listing ở trạng thái <sau>
      Và thẻ ở trạng thái <thẻ>

      Ví dụ:
        | từ     | sự kiện                                          | sau       | thẻ             |
        | Active | S1 hủy niêm yết                                  | Cancelled | Owned (S1)      |
        | Active | job chạy lúc 2026-10-10 12:00:00                 | Expired   | Owned (S1)      |
        | Active | job chạy lúc 2026-10-10 11:59:59                 | Active    | Listed          |
        | Active | CS Agent gỡ listing vi phạm                      | Removed   | Owned (S1)      |
        | Sold   | S1 hủy niêm yết                                  | Sold      | Owned (B1)      |
        | Expired| B1 mua listing                                   | Expired   | Owned (S1)      |

  Quy tắc: BR-MKT-07 — Đấu giá chỉ cho Epic+, thời hạn cố định

    @R2 @negative
    Kịch bản: SC-MKT-16 — Đấu giá thẻ Rare
      Cho trước S1 sở hữu thẻ Rare "Zephyrion #0300"
      Khi S1 tạo phiên đấu giá cho thẻ đó
      Thì server từ chối với mã lỗi AUCTION_RARITY_NOT_ALLOWED

    @R2 @bien
    Sơ đồ kịch bản: SC-MKT-17 — Thời hạn phiên đấu giá
      Cho trước S1 sở hữu thẻ Epic "Nocturne #0042"
      Khi S1 tạo phiên đấu giá thời hạn <giờ> giờ
      Thì kết quả là <kết quả>

      Ví dụ:
        | giờ | kết quả                   |
        | 24  | thành công                |
        | 36  | AUCTION_DURATION_INVALID  |
        | 72  | thành công                |
        | 96  | AUCTION_DURATION_INVALID  |

  Quy tắc: BR-MKT-08 — Bước giá và tạm giữ tiền

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-MKT-18 — Bid hợp lệ
      Cho trước phiên đấu giá có giá hiện tại 1,000 Coin (bước giá tối thiểu 50)
      Khi B1 đặt bid <bid> Coin
      Thì kết quả là <kết quả>

      Ví dụ:
        | bid   | kết quả      |
        | 1,049 | BID_TOO_LOW  |
        | 1,050 | thành công   |

    @R2 @bien
    Kịch bản: SC-MKT-19 — Bid đầu tiên bằng giá khởi điểm
      Cho trước phiên đấu giá chưa có bid, giá khởi điểm 2,000 Coin
      Khi B1 đặt bid 2,000 Coin
      Thì bid được chấp nhận

    @R2 @happy
    Kịch bản: SC-MKT-20 — Tạm giữ và hoàn tiền khi bị vượt giá
      Cho trước B1 có 5,000 Coin và đang dẫn đầu với bid 2,000 Coin (đang tạm giữ 2,000)
      Khi B2 đặt bid hợp lệ 2,100 Coin
      Thì số dư khả dụng của B1 là 5,000 Coin
      Và 2,100 Coin của B2 được tạm giữ

    @R2 @negative
    Kịch bản: SC-MKT-21 — Bid vượt số dư khả dụng
      Cho trước B1 có 1,500 Coin khả dụng
      Khi B1 đặt bid 2,000 Coin
      Thì server từ chối với mã lỗi INSUFFICIENT_BALANCE

  Quy tắc: BR-MKT-09 — Gia hạn khi bid ở 2 phút cuối

    @R2 @bien
    Kịch bản: SC-MKT-05 — Bid trong 2 phút cuối
      Cho trước phiên đấu giá kết thúc lúc 20:00:00
      Khi B1 đặt bid hợp lệ lúc 19:58:30
      Thì thời điểm kết thúc mới là 20:00:30

    @R2 @bien
    Kịch bản: SC-MKT-06 — Bid ngay trước mốc 2 phút
      Cho trước phiên đấu giá kết thúc lúc 20:00:00
      Khi B1 đặt bid hợp lệ lúc 19:57:59
      Thì thời điểm kết thúc giữ nguyên 20:00:00

    @R2 @negative
    Kịch bản: SC-MKT-22 — Bid sau khi phiên kết thúc
      Cho trước phiên đấu giá kết thúc lúc 20:00:00
      Khi B1 đặt bid lúc 20:00:01
      Thì server từ chối với mã lỗi AUCTION_ENDED

  Quy tắc: BR-MKT-10 — Seller và tài khoản liên kết không được bid

    @R2 @unauthorized
    Kịch bản: SC-MKT-23 — Seller tự bid
      Cho trước S1 có phiên đấu giá đang chạy
      Khi S1 đặt bid vào phiên của mình
      Thì server từ chối với mã lỗi SELF_BID_FORBIDDEN

    @R2 @unauthorized
    Kịch bản: SC-MKT-24 — Tài khoản cùng fingerprint với Seller
      Cho trước "S1b" có fingerprint thiết bị trùng với S1
      Khi S1b đặt bid vào phiên của S1
      Thì server từ chối với mã lỗi SELF_BID_FORBIDDEN
      Và sự kiện được gắn cờ cho Fraud Analyst

  Quy tắc: Vòng đời Auction (BRD mục 8.4)

    @R2 @trangthai @tinhtoan
    Kịch bản: SC-MKT-25 — Kết thúc có người thắng
      Cho trước phiên đấu giá của S1 cho "Nocturne #0042" có bid cao nhất 3,000 Coin của B1
      Khi phiên kết thúc
      Thì B1 sở hữu "Nocturne #0042"
      Và 3,000 Coin tạm giữ của B1 được chuyển đi
      Và S1 nhận 2,700 Coin
      Và phiên ở trạng thái "Settled"

    @R2 @trangthai
    Kịch bản: SC-MKT-26 — Kết thúc không có bid
      Cho trước phiên đấu giá của S1 không có bid nào
      Khi phiên kết thúc
      Thì phiên ở trạng thái "Ended-NoBid"
      Và thẻ trở lại trạng thái "Owned" của S1

    @R2 @trangthai @negative
    Kịch bản: SC-MKT-27 — Seller hủy phiên đã có bid
      Cho trước phiên đấu giá của S1 đã có 1 bid
      Khi S1 hủy phiên
      Thì server từ chối với mã lỗi AUCTION_HAS_BIDS

  Quy tắc: BR-MKT-11 — Tranh chấp

    @R2 @happy
    Kịch bản: SC-MKT-28 — CS hủy giao dịch dưới ngưỡng
      Cho trước giao dịch "TR-9" B1 mua thẻ của S1 giá 2,000 Coin, B1 vẫn giữ thẻ
      Và có bằng chứng S1 lừa đảo trong ticket "T-80"
      Khi CS Agent hủy giao dịch TR-9 kèm ticket T-80
      Thì ledger ghi bút toán đảo: B1 +2,000 Coin, S1 −1,900 Coin
      Và thẻ trở về S1 ở trạng thái "Locked"
      Và bút toán gốc của TR-9 không bị thay đổi

    @R2 @bien
    Kịch bản: SC-MKT-29 — Hủy giao dịch trên ngưỡng cần Fraud Analyst duyệt
      Cho trước giao dịch "TR-10" giá 10,001 Coin
      Khi CS Agent hủy giao dịch TR-10
      Thì yêu cầu ở trạng thái "Chờ duyệt"
      Và chưa có bút toán đảo nào được ghi cho đến khi Fraud Analyst duyệt

    @R2 @negative
    Kịch bản: SC-MKT-30 — Hủy giao dịch khi thẻ đã được bán tiếp
      Cho trước B1 đã bán tiếp thẻ của giao dịch "TR-9" cho B2
      Khi CS Agent hủy giao dịch TR-9
      Thì server từ chối với mã lỗi DISPUTE_CARD_TRANSFERRED
      Và yêu cầu được chuyển cho Fraud Analyst xử lý thủ công
```

---

## 11. Tính năng: Chống gian lận

```gherkin
Tính năng: Chống gian lận
  Quy tắc: BR-FRD-01 — Thiết bị không hợp lệ

    @R1 @negative
    Sơ đồ kịch bản: SC-FRD-01 — Quyền lợi theo cờ thiết bị
      Cho trước thiết bị của P1 có cờ <cờ>
      Khi P1 thực hiện <hành động>
      Thì kết quả là <kết quả>

      Ví dụ:
        | cờ           | hành động         | kết quả               |
        | emulator     | nhận thưởng ads   | DEVICE_NOT_ELIGIBLE   |
        | jailbreak    | điểm danh         | DEVICE_NOT_ELIGIBLE   |
        | root         | mua pack bằng Gem | thành công            |
        | không có     | nhận thưởng ads   | thành công            |
        | không xác định | nhận thưởng ads | thành công, gắn cờ    |

  Quy tắc: BR-FRD-03 — Captcha khi nghi tự động hóa

    @R1 @negative
    Kịch bản: SC-FRD-02 — Điểm danh thiếu captcha khi bị yêu cầu
      Cho trước điểm rủi ro tự động hóa của P1 vượt ngưỡng nên P1 bị yêu cầu captcha
      Khi P1 điểm danh không kèm captcha token hợp lệ
      Thì server từ chối với mã lỗi CAPTCHA_REQUIRED

  Quy tắc: BR-FRD-04 — Gắn cờ giao dịch bất thường

    @R2 @bien
    Sơ đồ kịch bản: SC-FRD-03 — Quy tắc gắn cờ
      Cho trước <điều kiện>
      Khi giao dịch tiếp theo hoàn tất
      Thì giao dịch <gắn cờ>

      Ví dụ:
        | điều kiện                                                       | gắn cờ           |
        | cặp S1–B1 đã giao dịch 1 lần trong 7 ngày (đây là lần thứ 2)    | không bị gắn cờ  |
        | cặp S1–B1 đã giao dịch 2 lần trong 7 ngày (đây là lần thứ 3)    | bị gắn cờ        |
        | giá gợi ý 1,000 Coin, giá bán 4,000 Coin (lệch 300%)            | không bị gắn cờ  |
        | giá gợi ý 1,000 Coin, giá bán 4,001 Coin (lệch hơn 300%)        | bị gắn cờ        |
        | tài khoản mua tạo cách đây 6 ngày 23 giờ, mua thẻ Legendary     | bị gắn cờ        |
        | tài khoản mua tạo cách đây đúng 7 ngày, mua thẻ Legendary       | không bị gắn cờ  |
```

---

## 12. Tính năng: EP-10 — Admin

```gherkin
Tính năng: EP-10 — Admin
  Quy tắc: BR-ADM-01 — Audit log

    @R1 @happy
    Kịch bản: SC-ADM-04 — Ghi audit khi ban người dùng
      Cho trước Fraud Analyst "F1"
      Khi F1 ban tài khoản "P9" với lý do "bot farm ads"
      Thì audit log có bản ghi: người thực hiện F1, hành động BAN, đối tượng P9, trạng thái trước "Verified", trạng thái sau "Banned", lý do "bot farm ads"

    @R1 @negative
    Kịch bản: SC-ADM-05 — Sửa audit log
      Cho trước bản ghi audit "AU-500"
      Khi Super Admin gửi yêu cầu xóa AU-500
      Thì server từ chối với mã lỗi AUDIT_IMMUTABLE

    @R1 @happy
    Kịch bản: SC-ADM-06 — Xem SĐT đầy đủ được ghi log
      Cho trước SĐT của P9 là "+84901234123"
      Khi Fraud Analyst "F1" xem SĐT đầy đủ của P9
      Thì F1 thấy "+84901234123"
      Và audit log có bản ghi F1 xem PII của P9

    @R1 @unauthorized
    Kịch bản: SC-ADM-07 — CS Agent chỉ thấy SĐT đã che
      Cho trước SĐT của P9 là "+84901234123"
      Khi CS Agent "C1" xem thông tin P9
      Thì C1 thấy SĐT "090****123"

  Quy tắc: BR-ADM-02 — Maker-checker

    @R1 @happy
    Kịch bản: SC-ADM-08 — Người khác duyệt
      Cho trước Economy Manager "E1" tạo bản nháp tỷ lệ "v3" hiệu lực 2026-11-01
      Khi Super Admin "SA1" duyệt "v3"
      Thì "v3" ở trạng thái "Đã duyệt"
      Và audit log ghi E1 là người tạo, SA1 là người duyệt

    @R1 @negative
    Kịch bản: SC-ADM-01 — Tự duyệt
      Cho trước Economy Manager "E1" tạo bản nháp tỷ lệ "v3"
      Khi E1 duyệt bản nháp "v3"
      Thì server từ chối với mã lỗi SELF_APPROVAL_FORBIDDEN

    @R1 @bien
    Sơ đồ kịch bản: SC-ADM-02 — Tổng tỷ lệ phải bằng 100%
      Khi Economy Manager gửi duyệt bảng tỷ lệ có tổng <tổng>
      Thì kết quả là <kết quả>

      Ví dụ:
        | tổng    | kết quả                |
        | 99.99%  | DROP_RATE_SUM_INVALID  |
        | 100.00% | đã gửi duyệt           |
        | 100.01% | DROP_RATE_SUM_INVALID  |

  Quy tắc: BR-ADM-03 — Không sửa tỷ lệ đang bán

    @R1 @negative
    Kịch bản: SC-ADM-09 — Sửa trực tiếp version đang hiệu lực
      Cho trước version "v1" của "Awakening Standard" đang hiệu lực
      Khi Economy Manager sửa tỷ lệ Legendary của "v1" từ 4% thành 3%
      Thì server từ chối với mã lỗi VERSION_LOCKED

  Quy tắc: BR-ADM-04 — Không cộng Gem trực tiếp; bồi thường có ticket

    @R1 @negative
    Kịch bản: SC-ADM-10 — Cộng Gem cho người chơi
      Khi Super Admin gửi yêu cầu cộng 1,000 Gem cho P1
      Thì server từ chối với mã lỗi GEM_GRANT_FORBIDDEN

    @R1 @negative
    Kịch bản: SC-ADM-11 — Bồi thường Coin không có ticket
      Khi CS Agent tạo yêu cầu bồi thường 500 Coin cho P1 không kèm mã ticket
      Thì server từ chối với mã lỗi TICKET_REQUIRED

    @R1 @happy
    Kịch bản: SC-ADM-12 — Bồi thường Coin đúng quy trình
      Cho trước CS Agent "C1" tạo yêu cầu bồi thường 500 Coin cho P1 với ticket "T-90"
      Khi Fraud Analyst "F1" duyệt yêu cầu
      Thì P1 nhận 500 Coin
      Và bút toán tham chiếu T-90, người tạo C1, người duyệt F1

  Quy tắc: US-10.2 — Không xóa cứng thẻ đã phát hành

    @R1 @negative
    Kịch bản: SC-ADM-13 — Xóa Card Definition đã có bản
      Cho trước Card Definition "Seraphel" đã có 1,200 Card Instance
      Khi Content Manager xóa "Seraphel"
      Thì server từ chối với mã lỗi CARD_HAS_INSTANCES

    @R1 @happy
    Kịch bản: SC-ADM-14 — Ngừng phát hành thẻ
      Cho trước Card Definition "Seraphel" đang phát hành
      Khi Content Manager ngừng phát hành "Seraphel"
      Thì "Seraphel" không xuất hiện trong version tỷ lệ mới
      Và 1,200 Card Instance hiện có không bị ảnh hưởng

  Quy tắc: Ma trận phân quyền admin (BRD mục 12.2)

    @R1 @unauthorized
    Sơ đồ kịch bản: SC-ADM-03 — Hành động ngoài quyền
      Cho trước admin <vai trò> đã đăng nhập
      Khi admin gửi trực tiếp tới API yêu cầu <hành động>
      Thì server từ chối với mã lỗi FORBIDDEN
      Và hành động bị ghi vào audit log

      Ví dụ:
        | vai trò         | hành động                             |
        | CS Agent        | sửa tỷ lệ rơi                         |
        | CS Agent        | ban tài khoản                         |
        | Content Manager | đổi phí giao dịch                     |
        | Economy Manager | sửa Story Fragment                    |
        | Finance Viewer  | khóa tài khoản                        |
        | Fraud Analyst   | gỡ ban                                |
        | Super Admin     | sửa Card Definition                   |
        | Super Admin     | tự duyệt thay đổi tỷ lệ của chính mình |
```

---

## 12A. Tính năng: Website người chơi (CR-001)

```gherkin
Tính năng: Website người chơi — đa nền tảng
  Bối cảnh chung:
    Cho trước tài khoản "P1" ở trạng thái Verified, dùng cả app mobile và website

  Quy tắc: BR-WEB-01 — Một tài khoản, dữ liệu dùng chung

    @R1 @happy
    Kịch bản: SC-WEB-01 — Mua pack trên web, mở trên app
      Cho trước P1 có 2,000 Coin
      Khi P1 mua 1 pack "Awakening Standard" bằng Coin trên website
      Thì lần tải dữ liệu kế tiếp trên app, P1 có 1,000 Coin và 1 Pack Instance "Unopened"

    @R1 @dongthoi
    Kịch bản: SC-WEB-02 — Mở cùng một pack đồng thời trên app và web
      Cho trước P1 có Pack Instance "PI-90" ở trạng thái "Unopened"
      Khi app và website cùng gửi yêu cầu mở PI-90
      Thì chỉ một lần quay được thực hiện
      Và cả hai nền tảng nhận cùng một kết quả

    @R1 @dongthoi
    Kịch bản: SC-WEB-03 — Tiêu Coin đồng thời trên hai nền tảng
      Cho trước P1 có 1,500 Coin
      Khi app và website cùng gửi yêu cầu mua pack giá 1,000 Coin với hai idempotency key khác nhau
      Thì đúng một yêu cầu thành công
      Và yêu cầu còn lại nhận mã lỗi INSUFFICIENT_BALANCE
      Và số dư Coin của P1 là 500

  Quy tắc: BR-WEB-02 — Phiên web

    @R1 @bien @cho-Q35
    Kịch bản: SC-WEB-04 — Đăng nhập phiên web thứ 4
      Cho trước P1 có 3 phiên web đang hoạt động, phiên cũ nhất tạo lúc 2026-10-01 08:00
      Khi P1 đăng nhập website trên một trình duyệt đã từng dùng
      Thì phiên tạo lúc 2026-10-01 08:00 bị đăng xuất
      Và P1 có đúng 3 phiên web đang hoạt động

    @R1 @negative @cho-Q35
    Kịch bản: SC-WEB-05 — Trình duyệt mới không nhập OTP
      Cho trước P1 chưa từng đăng nhập trên trình duyệt "B-NEW"
      Khi P1 đăng nhập đúng mật khẩu trên B-NEW nhưng không hoàn tất OTP
      Thì server không cấp phiên web với mã lỗi OTP_REQUIRED

    @R1 @happy
    Kịch bản: SC-WEB-06 — Phiên web không chiếm thiết bị mobile
      Cho trước điện thoại "D1" đang gắn với tài khoản P1
      Khi tài khoản "P2" đăng nhập website trên trình duyệt của điện thoại D1
      Thì đăng nhập web của P2 thành công
      Và D1 vẫn gắn với P1

  Quy tắc: BR-WEB-03 — Điểm danh và ads chỉ trên app ở R1

    @R1 @unauthorized @cho-Q32
    Sơ đồ kịch bản: SC-WEB-07 — Hành động kiếm Coin gửi từ phiên web
      Khi P1 gửi trực tiếp tới API yêu cầu <hành động> bằng phiên web
      Thì server từ chối với mã lỗi PLATFORM_NOT_SUPPORTED
      Và không có Coin nào được cộng

      Ví dụ:
        | hành động                    |
        | điểm danh                    |
        | nhận thưởng rewarded ad      |
        | lấy Streak Freeze bằng ads   |

  Quy tắc: BR-WEB-04 — Nạp Gem qua cổng thanh toán web

    @R1 @happy @tichhop @cho-Q31
    Kịch bản: SC-WEB-08 — IPN hợp lệ
      Cho trước P1 tạo đơn nạp "WO-100" gói 550 Gem trên website
      Khi cổng thanh toán gửi IPN có chữ ký hợp lệ báo "WO-100" thanh toán thành công với gateway transaction "GW-7001"
      Thì số dư Gem của P1 tăng 550
      Và ledger có bút toán tham chiếu "GW-7001"

    @R1 @negative @tichhop
    Kịch bản: SC-WEB-09 — Chỉ có return URL, không có IPN
      Cho trước P1 tạo đơn nạp "WO-101" gói 550 Gem
      Khi trình duyệt của P1 quay về trang kết quả với tham số "thành công" nhưng server chưa nhận IPN và truy vấn cổng trả "đang xử lý"
      Thì số dư Gem của P1 không đổi
      Và đơn "WO-101" ở trạng thái "Chờ xác nhận"

    @R1 @idempotency @tichhop
    Kịch bản: SC-WEB-10 — IPN gửi trùng
      Cho trước IPN cho "GW-7001" đã được xử lý và cộng 550 Gem
      Khi cổng thanh toán gửi lại IPN cho "GW-7001"
      Thì số dư Gem của P1 không đổi

    @R1 @negative @tichhop
    Kịch bản: SC-WEB-11 — Chữ ký IPN sai
      Khi server nhận IPN cho đơn "WO-102" có chữ ký không hợp lệ
      Thì server từ chối với mã lỗi SIGNATURE_INVALID
      Và sự kiện được ghi vào log gian lận

  Quy tắc: BR-WEB-06 — Mở pack trên trình duyệt không hỗ trợ Unity Web

    @R1 @bien
    Kịch bản: SC-WEB-12 — Chế độ rút gọn
      Cho trước trình duyệt của P1 không chạy được bản Unity Web
      Và server quay được Epic, Common, Rare, Common, Legendary khi P1 mở pack
      Khi P1 mở pack trên website
      Thì website hiển thị chế độ rút gọn với thứ tự lật Common, Common, Rare, Epic, Legendary
      Và bộ sưu tập của P1 có đúng 5 thẻ đó
```

---

## 12B. Tính năng: Tài sản số — CR-002

```gherkin
Tính năng: Số lượng phát hành giới hạn (BR-SUP)
  Bối cảnh chung:
    Cho trước mùa "Awakening S1" đang mở bán
    Và rarity Legendary của pack "Awakening Standard" gồm "Seraphel" (tối đa 300 bản) và "Tidemourn" (tối đa 300 bản)

  Quy tắc: BR-SUP-02 — Số thứ tự trong edition

    @R1 @bien
    Kịch bản: SC-SUP-01 — Bản cuối cùng của một thẻ
      Cho trước "Seraphel" đã phát hành 299/300 bản
      Khi một slot của P1 quay ra Legendary và chọn trúng "Seraphel"
      Thì P1 nhận "Seraphel #300/300" với serial duy nhất toàn hệ thống
      Và "Seraphel" hiển thị "Đã phát hành hết 300/300"

  Quy tắc: BR-SUP-03 — Chỉ chọn thẻ còn bản

    @R1 @happy
    Kịch bản: SC-SUP-02 — Thẻ hết bản bị loại khỏi lượt chọn
      Cho trước "Seraphel" đã phát hành 300/300 và "Tidemourn" đã phát hành 120/300
      Khi một slot của P1 quay ra Legendary
      Thì P1 nhận "Tidemourn #121/300"

  Quy tắc: BR-SUP-04 — Ngừng bán khi một rarity hết sạch

    @R1 @negative
    Kịch bản: SC-SUP-03 — Mọi Legendary đã hết
      Cho trước "Seraphel" 300/300 và "Tidemourn" 300/300
      Khi bản Legendary cuối cùng được phát hành
      Thì pack "Awakening Standard" chuyển sang trạng thái "Tạm ngừng bán"
      Và mọi yêu cầu mua pack này bị từ chối với mã lỗi PACK_SUPPLY_EXHAUSTED cho đến khi version tỷ lệ mới được công bố
      Và Pack Instance đã mua trước đó vẫn mở được theo BR-SUP-03 với các rarity còn bản

  Quy tắc: BR-SUP-05 — Mùa đã đóng

    @R1 @negative
    Kịch bản: SC-SUP-04 — Không phát hành thêm thẻ của mùa đã đóng
      Cho trước mùa "Awakening S1" đã đóng và mùa "S2" đang mở
      Khi P1 lật một thẻ chưa lật từ Lò rèn
      Thì kết quả là một thẻ thuộc mùa "S2"
      Và số bản đã phát hành của mọi thẻ mùa "S1" không đổi

Tính năng: Kiểm chứng công bằng — Commit–reveal (BR-PF)
  Bối cảnh chung:
    Cho trước bảng tỷ lệ v1 tính theo phần triệu: Common [0, 450000), Uncommon [450000, 700000), Rare [700000, 880000), Epic [880000, 950000), Legendary [950000, 990000), Secret [990000, 1000000)
    Và cách tính theo SOLUTION_DESIGN mục 8.1

  Quy tắc: BR-PF-02 — Kết quả tính lại được

    @R1 @tinhtoan
    Kịch bản: SC-PF-01 — Vector kiểm thử chuẩn
      Cho trước server seed "anima-demo-server-seed-001", client seed "keeper2049", nonce 1
      Khi P1 mở một pack 5 slot với pity 0
      Thì giá trị quay của slot 0 → 4 lần lượt là 457142, 594361, 140124, 227524, 278885
      Và kết quả rarity là Uncommon, Uncommon, Common, Common, Common
      Và nonce của P1 tăng lên 2

  Quy tắc: BR-PF-03 — Công bố mã băm trước khi quay

    @R1 @happy
    Kịch bản: SC-PF-02 — Mã băm hiển thị trước lần quay đầu
      Cho trước P1 vừa nhận server seed mới "anima-demo-server-seed-001"
      Khi P1 xem màn hình kiểm chứng trước khi mở pack
      Thì phản hồi chứa mã băm "9bda19bd88620c85d06a774f70f150a0379a20995f91a3525caf895375f6ccdf"
      Và không chứa server seed

  Quy tắc: BR-PF-04 — Công bố seed cũ khi đổi seed

    @R1 @happy
    Kịch bản: SC-PF-03 — Đổi seed
      Cho trước P1 đã quay 12 lần với server seed có mã băm "9bda19bd…cdf"
      Khi P1 yêu cầu đổi seed và đặt client seed mới "my-lucky-seed"
      Thì server công bố server seed cũ "anima-demo-server-seed-001"
      Và SHA-256 của seed cũ bằng mã băm đã công bố
      Và P1 nhận mã băm của server seed mới và nonce về 1

    @R1 @negative
    Kịch bản: SC-PF-04 — Xin seed hiện tại khi chưa đổi
      Khi P1 gửi yêu cầu xem server seed đang dùng
      Thì server từ chối với mã lỗi SEED_NOT_REVEALED

    @R1 @dongthoi
    Kịch bản: SC-PF-05 — Đổi seed trong khi đang mở pack
      Cho trước P1 gửi yêu cầu mở pack và yêu cầu đổi seed cùng lúc
      Khi server xử lý hai yêu cầu
      Thì lần mở pack dùng trọn vẹn một seed (cũ hoặc mới), không trộn
      Và bản ghi mở pack lưu mã băm của seed đã dùng

Tính năng: Lò rèn (BR-FRG)
  Bối cảnh chung:
    Cho trước P1 ở trạng thái Verified, có 500 Coin và 20 Gem
    Và P1 sở hữu thẻ Common "Driftkoi #12001" và "Cinderpup #8812"
    Và phí rèn là 50 Coin hoặc 5 Gem

  Quy tắc: BR-FRG-01 / BR-FRG-02 — 2 thẻ + phí → 1 thẻ chưa lật

    @R1 @happy @tinhtoan
    Kịch bản: SC-FRG-01 — Rèn trả bằng Coin
      Khi P1 rèn "Driftkoi #12001" và "Cinderpup #8812", chọn trả bằng Coin
      Thì P1 có 450 Coin và 20 Gem
      Và hai thẻ đầu vào ở trạng thái "Burned"
      Và P1 có 1 thẻ chưa lật mới
      Và số bản đã hủy của "Driftkoi" và "Cinderpup" mỗi thẻ tăng 1

    @R1 @happy @tinhtoan
    Kịch bản: SC-FRG-02 — Rèn trả bằng Gem
      Khi P1 rèn hai thẻ đó, chọn trả bằng Gem
      Thì P1 có 500 Coin và 15 Gem

    @R1 @negative
    Kịch bản: SC-FRG-03 — Không đủ phí thì không hủy thẻ
      Cho trước P1 có 49 Coin và 4 Gem
      Khi P1 rèn hai thẻ đó, chọn trả bằng Coin
      Thì server từ chối với mã lỗi INSUFFICIENT_BALANCE
      Và hai thẻ vẫn ở trạng thái "Owned"

    @R1 @negative
    Sơ đồ kịch bản: SC-FRG-04 — Đầu vào không hợp lệ
      Khi P1 rèn với đầu vào <đầu vào>
      Thì server từ chối với mã lỗi <mã lỗi>
      Và không có thẻ nào bị hủy, không trừ phí

      Ví dụ:
        | đầu vào                                        | mã lỗi                     |
        | chỉ 1 thẻ "Driftkoi #12001"                    | FORGE_REQUIRES_TWO_CARDS   |
        | "Driftkoi #12001" hai lần                      | FORGE_DUPLICATE_INPUT      |
        | 1 thẻ của P1 và 1 thẻ của P2                   | CARD_NOT_OWNED             |
        | 1 thẻ đang niêm yết trên chợ                   | CARD_LOCKED                |
        | 1 thẻ soulbound streak 100 ngày                | CARD_NOT_FORGEABLE         |
        | 1 thẻ đang nằm trong ví ngoài (In Wallet)      | CARD_NOT_IN_ACCOUNT        |

  Quy tắc: BR-FRG-03 — Không có gì tác động vào tỷ lệ rèn

    @R1 @tinhtoan
    Kịch bản: SC-FRG-05 — Pity của pack không áp dụng cho rèn
      Cho trước bộ đếm pity "Awakening Standard" của P1 là 49
      Và giá trị quay khi lật thẻ rèn là 140124
      Khi P1 lật thẻ chưa lật
      Thì P1 nhận một thẻ Common
      Và bộ đếm pity "Awakening Standard" vẫn là 49

    @R1 @happy
    Kịch bản: SC-FRG-06 — Lật thẻ rèn dùng commit–reveal
      Cho trước P1 có 1 thẻ chưa lật và nonce hiện tại là 7
      Khi P1 lật thẻ đó
      Thì kết quả tính từ HMAC(server seed, "client seed:7:0")
      Và nonce của P1 là 8

  Quy tắc: BR-FRG-06 — Thẻ chưa lật không giao dịch được

    @R1 @negative
    Kịch bản: SC-FRG-07 — Niêm yết thẻ chưa lật
      Khi P1 niêm yết một thẻ chưa lật
      Thì server từ chối với mã lỗi SEALED_CARD_NOT_TRADABLE

  Quy tắc: BR-FRG-07 — Giới hạn lượt rèn theo ngày

    @R1 @bien
    Sơ đồ kịch bản: SC-FRG-08 — Hạn mức rèn
      Cho trước tài khoản <trạng thái> đã rèn <đã rèn> lần hôm nay
      Khi tài khoản rèn thêm 1 lần
      Thì kết quả là <kết quả>

      Ví dụ:
        | trạng thái | đã rèn | kết quả             |
        | Unverified | 4      | thành công          |
        | Unverified | 5      | FORGE_DAILY_LIMIT   |
        | Verified   | 99     | thành công          |
        | Verified   | 100    | FORGE_DAILY_LIMIT   |

  Quy tắc: BR-FRG-01 — Đồng thời

    @R1 @dongthoi
    Kịch bản: SC-FRG-09 — Cùng một thẻ trong hai lần rèn đồng thời
      Cho trước P1 sở hữu "Driftkoi #12001", "Cinderpup #8812" và "Sparkit #3301"
      Khi P1 gửi cùng lúc rèn ("Driftkoi #12001", "Cinderpup #8812") và rèn ("Driftkoi #12001", "Sparkit #3301")
      Thì đúng một lần rèn thành công
      Và lần còn lại bị từ chối với mã lỗi CARD_NOT_AVAILABLE, thẻ của nó không bị hủy, không bị trừ phí

Tính năng: Quy đổi Gem ↔ Coin (BR-WAL-05, BR-WAL-06)
  Bối cảnh chung:
    Cho trước tỷ lệ 1 Gem → 9 Coin và 11 Coin → 1 Gem
    Và P1 ở trạng thái Verified

    @R1 @happy @tinhtoan
    Kịch bản: SC-WAL-20 — Đổi Coin sang Gem, làm tròn xuống
      Cho trước P1 có 120 Coin và 0 Gem
      Khi P1 đổi 120 Coin sang Gem
      Thì P1 có 10 Gem và 10 Coin

    @R1 @negative
    Kịch bản: SC-WAL-21 — Chưa đủ cho 1 Gem
      Cho trước P1 có 10 Coin
      Khi P1 đổi 10 Coin sang Gem
      Thì server từ chối với mã lỗi INVALID_AMOUNT
      Và P1 vẫn có 10 Coin

    @R1 @bien @cho-Q38
    Sơ đồ kịch bản: SC-WAL-22 — Hạn mức đổi Coin → Gem mỗi ngày
      Cho trước P1 đã đổi được <đã đổi> Gem từ Coin hôm nay và có 50,000 Coin
      Khi P1 đổi 110 Coin sang Gem
      Thì kết quả là <kết quả>

      Ví dụ:
        | đã đổi | kết quả                         |
        | 990    | thành công, nhận 10 Gem         |
        | 991    | COIN_TO_GEM_DAILY_LIMIT         |

    @R1 @unauthorized
    Kịch bản: SC-WAL-23 — Tài khoản chưa xác thực đổi Coin → Gem
      Cho trước tài khoản "U1" chưa xác thực SĐT, có 1,100 Coin
      Khi U1 gửi trực tiếp tới API yêu cầu đổi 1,100 Coin sang Gem
      Thì server từ chối với mã lỗi PHONE_VERIFICATION_REQUIRED

Tính năng: NFT — rút và nạp thẻ (BR-NFT) — R2
  Bối cảnh chung:
    Cho trước chức năng NFT đã được bật sau gate pháp lý
    Và P1 Verified, đã KYC, 25 tuổi, ví "0xA1…" đã liên kết
    Và phí rút là 200 Coin hoặc 20 Gem, thời gian chờ 30 ngày

  Quy tắc: BR-NFT-02 — Điều kiện rút

    @R2 @happy @tichhop
    Kịch bản: SC-NFT-01 — Rút thẻ hợp lệ
      Cho trước P1 có "Tidemourn #121/300" từ 2026-09-01, có 1,000 Coin
      Khi P1 yêu cầu rút "Tidemourn #121/300" về ví "0xA1…" trên website ngày 2026-10-06, trả bằng Coin
      Thì P1 có 800 Coin và thẻ ở trạng thái "Withdrawing"
      Và khi giao dịch mint được xác nhận trên chuỗi, thẻ ở trạng thái "In Wallet"
      Và ví "0xA1…" sở hữu token có ID bằng serial của "Tidemourn #121/300"

    @R2 @bien
    Sơ đồ kịch bản: SC-NFT-02 — Thời gian chờ
      Cho trước P1 có thẻ từ lúc <có thẻ>
      Khi P1 yêu cầu rút thẻ lúc 2026-10-06 10:00:00
      Thì kết quả là <kết quả>

      Ví dụ:
        | có thẻ               | kết quả                    |
        | 2026-09-06 10:00:00  | thành công                 |
        | 2026-09-06 10:00:01  | WITHDRAW_COOLDOWN          |

    @R2 @unauthorized
    Sơ đồ kịch bản: SC-NFT-03 — Không đủ điều kiện rút
      Cho trước <điều kiện>
      Khi P1 yêu cầu rút một thẻ đủ thời gian chờ
      Thì server từ chối với mã lỗi <mã lỗi>

      Ví dụ:
        | điều kiện                                        | mã lỗi                       |
        | P1 chưa KYC                                      | KYC_REQUIRED                 |
        | P1 17 tuổi                                       | AGE_BELOW_MINIMUM            |
        | yêu cầu gửi từ app mobile                        | PLATFORM_NOT_SUPPORTED       |
        | thẻ là soulbound                                 | CARD_NOT_TRADABLE            |
        | thẻ đang niêm yết trên chợ                       | CARD_LOCKED                  |
        | P1 chưa liên kết ví                              | WALLET_NOT_LINKED            |
        | ví của P1 nằm trong danh sách trừng phạt         | WALLET_SCREENING_FAILED      |
        | chức năng rút đang bị tạm dừng do sự cố          | NFT_WITHDRAWALS_PAUSED       |

    @R2 @tichhop
    Kịch bản: SC-NFT-04 — Giao dịch mint thất bại
      Cho trước thẻ của P1 ở trạng thái "Withdrawing"
      Khi giao dịch mint thất bại hoặc chưa được xác nhận sau 2 giờ
      Thì thẻ trở lại trạng thái "Owned"
      Và phí rút được hoàn lại cho P1

    @R2 @happy
    Kịch bản: SC-NFT-05 — Thẻ từ Coin quảng cáo được rút
      Cho trước P1 có thẻ mở từ pack mua bằng Coin kiếm từ quảng cáo, đã qua thời gian chờ
      Khi P1 yêu cầu rút thẻ đó
      Thì yêu cầu được chấp nhận như mọi thẻ khác

  Quy tắc: BR-NFT-03 — Liên kết ví

    @R2 @negative
    Kịch bản: SC-NFT-06 — Ví đã liên kết với tài khoản khác
      Cho trước ví "0xB2…" đã liên kết với P2
      Khi P1 liên kết ví "0xB2…" bằng chữ ký hợp lệ
      Thì server từ chối với mã lỗi WALLET_ALREADY_LINKED

    @R2 @negative
    Kịch bản: SC-NFT-07 — Chữ ký sai
      Khi P1 liên kết ví "0xA1…" với chữ ký không khớp thông điệp
      Thì server từ chối với mã lỗi SIGNATURE_INVALID

  Quy tắc: BR-NFT-05 — Nạp lại

    @R2 @happy @tichhop
    Kịch bản: SC-NFT-08 — Nạp từ ví đã liên kết
      Cho trước ví "0xA1…" của P1 sở hữu token "Tidemourn #121/300"
      Khi ví "0xA1…" gửi token vào ví lưu ký của ANIMA và giao dịch đủ số xác nhận
      Thì "Tidemourn #121/300" ở trạng thái "Owned" trong tài khoản P1

    @R2 @tichhop
    Kịch bản: SC-NFT-09 — Người mua ở sàn ngoài nạp vào tài khoản của mình
      Cho trước P2 mua token "Tidemourn #121/300" ở sàn ngoài bằng ví "0xC3…" đã liên kết với P2
      Khi ví "0xC3…" gửi token vào ví lưu ký và đủ số xác nhận
      Thì "Tidemourn #121/300" thuộc P2 ở trạng thái "Owned"

    @R2 @negative @tichhop
    Kịch bản: SC-NFT-10 — Nạp từ ví chưa liên kết
      Khi ví "0xD4…" chưa liên kết tài khoản nào gửi một token ANIMA vào ví lưu ký
      Thì token được giữ ở trạng thái "Chờ liên kết"
      Và không tài khoản nào nhận thẻ cho đến khi chủ ví liên kết ví bằng chữ ký hợp lệ

    @R2 @idempotency @tichhop
    Kịch bản: SC-NFT-11 — Sự kiện chuỗi xử lý trùng
      Cho trước sự kiện chuyển token với tx hash "0x9f…01" đã được xử lý
      Khi bộ lắng nghe chuỗi nhận lại sự kiện "0x9f…01"
      Thì trạng thái thẻ không đổi

  Quy tắc: BR-NFT-08 — Công ty không thu hồi NFT

    @R2 @unauthorized
    Kịch bản: SC-NFT-12 — Admin cố thu hồi NFT trong ví người chơi
      Cho trước token "Tidemourn #121/300" nằm trong ví "0xA1…"
      Khi Super Admin gửi yêu cầu thu hồi hoặc hủy token đó
      Thì server từ chối với mã lỗi NFT_NOT_IN_CUSTODY
      Và smart contract không có hàm cho phép công ty chuyển token khỏi ví người chơi

  Quy tắc: BR-ECO-01 — Không mua lại bằng tiền

    @R1 @negative
    Kịch bản: SC-ECO-06 — Không tồn tại chức năng bán thẻ cho công ty
      Cho trước P1 sở hữu "Seraphel #12/300"
      Khi P1 gửi yêu cầu bán "Seraphel #12/300" cho nền tảng để nhận tiền
      Thì server trả mã lỗi NOT_SUPPORTED
```

## 12C. Tính năng: Toàn cầu và đa ngôn ngữ — CR-003

```gherkin
Tính năng: Quốc gia pháp lý và ma trận tính năng (BR-GEO)
  Bối cảnh chung:
    Cho trước ma trận tính năng:
      | quốc gia  | mua pack bằng tiền | Lò rèn | NFT | tuổi tối thiểu |
      | VN        | bật                | bật    | tắt | 13             |
      | SG        | bật                | bật    | bật | 13             |
      | Quốc gia X| tắt                | tắt    | tắt | 16             |

  Quy tắc: BR-GEO-01 — Thứ tự xác định quốc gia pháp lý

    @R1 @happy
    Sơ đồ kịch bản: SC-GEO-01 — Xác định quốc gia khi đăng ký
      Cho trước quốc gia store là <store>, quốc gia SĐT là <sđt>, quốc gia IP là <ip>
      Khi người dùng hoàn tất đăng ký
      Thì quốc gia pháp lý của tài khoản là <kết quả>

      Ví dụ:
        | store | sđt | ip  | kết quả |
        | TW    | VN  | SG  | TW      |
        | —     | VN  | SG  | VN      |
        | —     | —   | SG  | SG      |

    @R1 @unauthorized
    Kịch bản: SC-GEO-02 — Người chơi tự đổi quốc gia
      Cho trước tài khoản P1 có quốc gia pháp lý VN
      Khi P1 gửi trực tiếp tới API yêu cầu đổi quốc gia thành SG
      Thì server từ chối với mã lỗi FORBIDDEN
      Và quốc gia pháp lý của P1 vẫn là VN

  Quy tắc: BR-GEO-02 — Tính năng bị tắt theo quốc gia

    @R1 @negative
    Sơ đồ kịch bản: SC-GEO-03 — Gọi tính năng bị tắt
      Cho trước tài khoản có quốc gia pháp lý <quốc gia>
      Khi tài khoản gửi yêu cầu <hành động>
      Thì kết quả là <kết quả>

      Ví dụ:
        | quốc gia   | hành động                 | kết quả                           |
        | VN         | rèn 2 thẻ                 | thành công                        |
        | Quốc gia X | rèn 2 thẻ                 | FEATURE_NOT_AVAILABLE_IN_REGION   |
        | Quốc gia X | mua pack bằng Gem         | FEATURE_NOT_AVAILABLE_IN_REGION   |
        | VN         | rút thẻ về ví NFT         | FEATURE_NOT_AVAILABLE_IN_REGION   |
        | SG         | rút thẻ về ví NFT         | thành công nếu đủ BR-NFT-02       |

    @R1 @negative
    Kịch bản: SC-GEO-04 — Đổi ma trận không qua duyệt
      Cho trước Economy Manager "E1" tạo bản nháp bật NFT cho VN
      Khi E1 tự duyệt bản nháp
      Thì server từ chối với mã lỗi SELF_APPROVAL_FORBIDDEN

  Quy tắc: BR-GEO-03 — Quốc gia bị trừng phạt

    @R1 @negative
    Kịch bản: SC-GEO-05 — Đăng ký từ quốc gia bị trừng phạt
      Cho trước IP của người dùng thuộc một quốc gia trong danh sách trừng phạt
      Khi người dùng gửi yêu cầu đăng ký
      Thì server từ chối với mã lỗi REGION_BLOCKED
      Và không có tài khoản nào được tạo

  Quy tắc: BR-GEO-04 — Tuổi theo quốc gia

    @R1 @bien
    Sơ đồ kịch bản: SC-GEO-06 — Tuổi tối thiểu theo ma trận
      Cho trước ngày hiện tại là 2026-10-06 và quốc gia pháp lý là <quốc gia>
      Khi người dùng sinh ngày <ngày sinh> đăng ký
      Thì kết quả là <kết quả>

      Ví dụ:
        | quốc gia   | ngày sinh  | kết quả              |
        | VN         | 2013-10-06 | thành công           |
        | Quốc gia X | 2010-10-06 | thành công           |
        | Quốc gia X | 2010-10-07 | AGE_BELOW_MINIMUM    |

  Quy tắc: BR-GEO-05 — Xác suất từng thẻ

    @R1 @tinhtoan
    Kịch bản: SC-GEO-07 — Hiển thị xác suất từng Card Definition
      Cho trước thị trường của P1 yêu cầu công bố xác suất từng vật phẩm
      Và Legendary có tỷ lệ 4% mỗi slot, gồm 2 Card Definition còn bản
      Khi P1 xem tỷ lệ của pack
      Thì mỗi Card Definition Legendary hiển thị xác suất 2% mỗi slot
      Và khi chỉ còn 1 Card Definition Legendary còn bản, thẻ đó hiển thị 4% mỗi slot

  Quy tắc: BR-GEO-08 — Đổi quốc gia không mất tài sản

    @R2 @trangthai
    Kịch bản: SC-GEO-08 — Chuyển sang quốc gia tắt chợ
      Cho trước P1 có quốc gia pháp lý SG và một thẻ đang niêm yết trên chợ
      Khi CS đổi quốc gia pháp lý của P1 sang Quốc gia X có bằng chứng, ghi audit
      Thì listing bị gỡ và thẻ trở về trạng thái "Owned"
      Và mọi thẻ, Gem, Coin của P1 giữ nguyên

Tính năng: Đa ngôn ngữ (BR-I18N)

  Quy tắc: BR-I18N-01 — Ngôn ngữ mặc định và đồng bộ

    @R1 @happy
    Sơ đồ kịch bản: SC-I18N-01 — Chọn ngôn ngữ mặc định
      Cho trước ngôn ngữ thiết bị là <thiết bị>
      Khi người dùng mở app lần đầu
      Thì ngôn ngữ giao diện là <kết quả>

      Ví dụ:
        | thiết bị | kết quả |
        | vi-VN    | vi      |
        | zh-TW    | zh-Hant |
        | zh-CN    | zh-Hans |
        | zh-SG    | zh-Hans |
        | ja-JP    | en      |

    @R1 @happy
    Kịch bản: SC-I18N-02 — Đổi ngôn ngữ trên app, web theo
      Cho trước P1 đang dùng ngôn ngữ vi trên app và web
      Khi P1 đổi ngôn ngữ thành zh-Hant trên app
      Thì lần tải trang kế tiếp trên website hiển thị zh-Hant

  Quy tắc: BR-I18N-02 / BR-I18N-03 — Đủ bản dịch trước khi phát hành

    @R1 @negative
    Kịch bản: SC-I18N-03 — Mở bán set thiếu bản dịch story
      Cho trước set "Awakening" có 100 Story Fragment, trong đó 3 chưa có bản zh-Hant
      Khi Content Manager gửi duyệt mở bán set
      Thì server từ chối với mã lỗi TRANSLATION_INCOMPLETE
      Và phản hồi liệt kê 3 Card Definition và ngôn ngữ còn thiếu

    @R1 @bien
    Kịch bản: SC-I18N-04 — Chuỗi giao diện thiếu bản dịch
      Cho trước chuỗi "forge.title" chưa có bản zh-Hans
      Khi người chơi dùng zh-Hans mở màn Lò rèn
      Thì chuỗi hiển thị bằng tiếng Anh
      Và báo cáo CI liệt kê "forge.title" thiếu zh-Hans

  Quy tắc: BR-I18N-04 — Mã lỗi không đổi theo ngôn ngữ

    @R1 @happy
    Kịch bản: SC-I18N-05 — Cùng lỗi, khác ngôn ngữ
      Cho trước P1 có 999 Coin và đang dùng zh-Hans
      Khi P1 mua pack giá 1,000 Coin
      Thì mã lỗi trả về là INSUFFICIENT_BALANCE
      Và thông điệp hiển thị bằng tiếng Trung giản thể
```

## 12D. Tính năng: Đấu trường — CR-004

```gherkin
Tính năng: Bộ bài (BR-DECK)
  Bối cảnh chung:
    Cho trước tài khoản "P1" Verified, có đủ thẻ cần thiết ở trạng thái Owned

  Quy tắc: BR-DECK-01 → 03 — Cấu trúc bộ bài

    @R2 @bien
    Sơ đồ kịch bản: SC-DECK-01 — Kiểm tra bộ bài khi lưu
      Cho trước bộ bài gồm <anima> Anima và <hỗ trợ> bài hỗ trợ, <điều kiện thêm>
      Khi P1 lưu bộ bài
      Thì kết quả là <kết quả>

      Ví dụ:
        | anima | hỗ trợ | điều kiện thêm                               | kết quả                   |
        | 24    | 6      | không                                        | thành công                |
        | 25    | 5      | không                                        | thành công                |
        | 23    | 7      | không                                        | SUPPORT_LIMIT_EXCEEDED    |
        | 24    | 5      | không                                        | DECK_SIZE_INVALID         |
        | 25    | 6      | không                                        | DECK_SIZE_INVALID         |
        | 30    | 0      | có 3 bản "Driftkoi"                          | COPY_LIMIT_EXCEEDED       |
        | 30    | 0      | có 2 bản cùng một Legendary                  | COPY_LIMIT_EXCEEDED       |
        | 30    | 0      | có 5 thẻ Epic                                | RARITY_LIMIT_EXCEEDED     |
        | 30    | 0      | có 2 Secret Rare khác nhau                   | RARITY_LIMIT_EXCEEDED     |
        | 30    | 0      | có 1 thẻ đang niêm yết trên chợ              | CARD_NOT_AVAILABLE        |
        | 30    | 0      | có 1 thẻ đang nằm trong ví ngoài (In Wallet) | CARD_NOT_IN_ACCOUNT       |

  Quy tắc: BR-DECK-05 — Số bộ được lưu

    @R2 @bien
    Sơ đồ kịch bản: SC-DECK-02 — Lưu bộ thứ n
      Cho trước P1 đã lưu <đã lưu> bộ bài hợp lệ
      Khi P1 lưu thêm một bộ hợp lệ
      Thì kết quả là <kết quả>

      Ví dụ:
        | đã lưu | kết quả            |
        | 9      | thành công         |
        | 10     | DECK_SLOT_LIMIT    |

  Quy tắc: BR-DECK-06 / BR-DECK-07 — Bộ bài chưa hợp lệ

    @R2 @trangthai
    Kịch bản: SC-DECK-03 — Bán một thẻ đang nằm trong bộ đã lưu
      Cho trước bộ "Bão Luminara" của P1 hợp lệ, có thẻ "Brightling #4012"
      Khi giao dịch bán "Brightling #4012" của P1 trên chợ hoàn tất
      Thì "Brightling #4012" bị gỡ khỏi bộ "Bão Luminara"
      Và bộ "Bão Luminara" có 29 lá và trạng thái "chưa hợp lệ"

    @R2 @negative
    Kịch bản: SC-DECK-04 — Vào trận bằng bộ chưa hợp lệ
      Cho trước bộ "Bão Luminara" của P1 có 29 lá
      Khi P1 gửi yêu cầu vào trận giao hữu với bộ đó
      Thì server từ chối với mã lỗi DECK_INVALID

  Quy tắc: BR-DECK-08 — Khóa thẻ trong trận

    @R2 @trangthai
    Sơ đồ kịch bản: SC-DECK-05 — Thao tác với thẻ của bộ đang đấu
      Cho trước P1 đang trong một trận dùng bộ có thẻ "Tidemourn #121/300"
      Khi P1 gửi yêu cầu <hành động> với "Tidemourn #121/300"
      Thì kết quả là <kết quả>

      Ví dụ:
        | hành động           | kết quả        |
        | niêm yết trên chợ   | CARD_IN_MATCH  |
        | đưa vào Lò rèn      | CARD_IN_MATCH  |
        | rút về ví NFT       | CARD_IN_MATCH  |

    @R2 @happy
    Kịch bản: SC-DECK-06 — Mở khóa sau trận
      Cho trước trận của P1 vừa kết thúc
      Khi P1 niêm yết "Tidemourn #121/300"
      Thì niêm yết thành công

  Quy tắc: BR-DECK-04 / BR-NEW-04 — Thẻ gắn chặt tài khoản dùng được

    @R2 @happy
    Kịch bản: SC-DECK-07 — Bộ bài toàn thẻ Tân thủ
      Cho trước P1 chỉ có 30 thẻ Anima Common gắn chặt tài khoản, không thẻ nào quá 2 bản
      Khi P1 lưu bộ gồm cả 30 thẻ đó
      Thì bộ được lưu ở trạng thái hợp lệ

Tính năng: Luật trận đấu (BR-BTL)
  Bối cảnh chung:
    Cho trước P1 và P2 đang đấu trên sàn không có luật riêng ảnh hưởng tới kịch bản

  Quy tắc: BR-BTL-03 — Năng lượng Cộng hưởng

    @R2 @bien
    Sơ đồ kịch bản: SC-BTL-01 — Cộng hưởng theo lượt
      Khi P1 bắt đầu lượt thứ <lượt>
      Thì P1 có <cộng hưởng> điểm Cộng hưởng

      Ví dụ:
        | lượt | cộng hưởng |
        | 1    | 1          |
        | 6    | 6          |
        | 9    | 6          |

  Quy tắc: BR-BTL-04 — Anima vừa ra sân

    @R2 @negative
    Kịch bản: SC-BTL-02 — Tấn công ngay lượt ra sân
      Cho trước P1 vừa đưa "Cinderpup" (không có kỹ năng xung phong) ra sân trong lượt này
      Khi P1 cho "Cinderpup" tấn công
      Thì server từ chối với mã lỗi ATTACK_NOT_ALLOWED

  Quy tắc: BR-BTL-05 / BR-ELM-04 — Công thức sát thương

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-BTL-03 — Tính sát thương
      Cho trước Anima tấn công hệ <hệ công> có ATK hiệu lực <atk>
      Và mục tiêu hệ <hệ thủ> có DEF hiệu lực <def>
      Khi Anima tấn công mục tiêu
      Thì mục tiêu mất <sát thương> HP

      Ví dụ:
        | hệ công  | atk  | hệ thủ   | def  | sát thương | giải thích                         |
        | Voltaris | 1800 | Aqualis  | 900  | 900        | không khắc: 900 × 1.0              |
        | Voltaris | 800  | Aqualis  | 1000 | 100        | tối thiểu 100                      |
        | Umbryx   | 1600 | Aqualis  | 700  | 1130       | khắc: 900 × 1.25 = 1125 → 1130     |
        | Aqualis  | 1600 | Umbryx   | 700  | 680        | bị khắc: 900 × 0.75 = 675 → 680    |
        | Nihilum  | 1600 | Terrakin | 700  | 990        | Nihilum: 900 × 1.1                 |
        | Luminara | 1600 | Nihilum  | 700  | 1350       | Hy vọng khắc Trống rỗng: 900 × 1.5 |

    @R2 @negative
    Kịch bản: SC-BTL-04 — Đánh thẳng Keeper khi đối phương còn Anima
      Cho trước sân Anima của P2 còn 1 thẻ
      Khi P1 chọn tấn công thẳng Keeper của P2
      Thì server từ chối với mã lỗi DIRECT_ATTACK_NOT_ALLOWED

    @R2 @happy @tinhtoan
    Kịch bản: SC-BTL-05 — Đánh thẳng Keeper khi sân trống
      Cho trước sân Anima của P2 trống và Keeper của P2 còn 8,000 máu
      Khi Anima của P1 có ATK hiệu lực 1,800 tấn công thẳng Keeper
      Thì Keeper của P2 còn 6,200 máu

  Quy tắc: BR-BTL-06 — Anima hết HP

    @R2 @bien
    Kịch bản: SC-BTL-06 — HP về đúng 0
      Cho trước Anima của P2 còn 900 HP
      Khi Anima đó nhận 900 sát thương
      Thì Anima đó rời sân vào mộ

  Quy tắc: BR-BTL-09 — Đột tử

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-BTL-07 — Mất máu khi bắt đầu lượt
      Cho trước Keeper của P1 còn 5,000 máu
      Khi P1 bắt đầu lượt thứ <lượt> của mình
      Thì Keeper của P1 còn <máu> máu

      Ví dụ:
        | lượt | máu   |
        | 9    | 5,000 |
        | 10   | 4,500 |
        | 11   | 4,000 |

  Quy tắc: BR-BTL-08 — Hết thời gian

    @R2 @bien
    Sơ đồ kịch bản: SC-BTL-08 — Hết giờ liên tiếp
      Cho trước P1 đã hết giờ <số lần> lượt liên tiếp và đã dùng hết quỹ dự phòng
      Khi lượt tiếp theo của P1 cũng hết 20 giây mà P1 không thao tác
      Thì kết quả trận là <kết quả>

      Ví dụ:
        | số lần | kết quả                      |
        | 1      | trận tiếp tục, lượt chuyển P2 |
        | 2      | P1 thua                      |

  Quy tắc: BR-BTL-07 — Điều kiện thắng

    @R2 @negative
    Kịch bản: SC-BTL-09 — Phải bốc khi hết bài
      Cho trước bộ bài của P1 không còn lá nào
      Khi P1 bắt đầu lượt và phải bốc 1 lá
      Thì P1 thua trận

    @R2 @bien
    Sơ đồ kịch bản: SC-BTL-10 — Mất kết nối
      Cho trước P1 mất kết nối
      Khi P1 kết nối lại sau <giây> giây
      Thì kết quả là <kết quả>

      Ví dụ:
        | giây | kết quả                |
        | 59   | trận tiếp tục          |
        | 61   | P1 thua do mất kết nối |

  Quy tắc: BR-BTL-02 — Đổi tay

    @R2 @negative
    Kịch bản: SC-BTL-11 — Đổi tay lần thứ hai
      Cho trước P1 đã đổi tay một lần
      Khi P1 yêu cầu đổi tay lần nữa
      Thì server từ chối với mã lỗi MULLIGAN_USED

  Quy tắc: BR-BTL-10 — Phát lại trận

    @R2 @happy
    Kịch bản: SC-BTL-12 — Phát lại cho ra cùng kết quả
      Cho trước trận "M-501" đã kết thúc với P1 thắng và Keeper của P1 còn 2,350 máu
      Khi hệ thống phát lại "M-501" từ dữ liệu đã lưu
      Thì kết quả phát lại là P1 thắng và Keeper của P1 còn 2,350 máu

Tính năng: Hệ và nhân quả (BR-ELM)

  Quy tắc: BR-ELM-02 / BR-ELM-03 — Bảng khắc chế

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-ELM-01 — Hệ số theo cặp hệ
      Khi Anima hệ <công> tấn công Anima hệ <thủ>
      Thì hệ số sát thương là <hệ số>

      Ví dụ:
        | công     | thủ      | hệ số |
        | Umbryx   | Aqualis  | 1.25  |
        | Pyraxis  | Terrakin | 1.25  |
        | Aqualis  | Ventara  | 1.25  |
        | Terrakin | Voltaris | 1.25  |
        | Ventara  | Luminara | 1.25  |
        | Voltaris | Umbryx   | 1.25  |
        | Luminara | Pyraxis  | 1.25  |
        | Pyraxis  | Luminara | 0.75  |
        | Voltaris | Aqualis  | 1.0   |
        | Nihilum  | Luminara | 1.1   |
        | Luminara | Nihilum  | 1.5   |
        | Pyraxis  | Nihilum  | 1.0   |

  Quy tắc: BR-ELM-05 — Sinh

    @R2 @tinhtoan
    Kịch bản: SC-ELM-02 — Ra sân khi có hệ sinh ra mình
      Cho trước sân của P1 có một Anima Umbryx
      Khi P1 đưa Anima Pyraxis có ATK 1,500 và HP 2,000 ra sân
      Thì Anima Pyraxis có ATK 1,700 và HP 2,200

    @R2 @negative
    Kịch bản: SC-ELM-03 — Ngược chiều vòng sinh
      Cho trước sân của P1 có một Anima Pyraxis
      Khi P1 đưa Anima Umbryx có ATK 1,500 ra sân
      Thì Anima Umbryx giữ ATK 1,500

    @R2 @negative
    Kịch bản: SC-ELM-04 — Nihilum không nhận sinh
      Cho trước sân của P1 có Anima của cả 7 hệ trong vòng
      Khi P1 đưa Anima Nihilum có ATK 2,000 ra sân
      Thì Anima Nihilum giữ ATK 2,000

  Quy tắc: BR-ELM-06 — Chuỗi nhân quả

    @R2 @happy
    Sơ đồ kịch bản: SC-ELM-05 — Kích hoạt chuỗi
      Cho trước sân của P1 có 3 Anima hệ <ba hệ>
      Khi P1 bắt đầu giai đoạn tấn công
      Thì chuỗi nhân quả <kích hoạt>

      Ví dụ:
        | ba hệ                        | kích hoạt                         |
        | Umbryx, Pyraxis, Aqualis     | có, cả 3 +300 ATK đến hết lượt    |
        | Luminara, Umbryx, Pyraxis    | có (vòng khép kín)                |
        | Umbryx, Aqualis, Terrakin    | không (không liên tiếp)           |

Tính năng: Sàn đấu (BR-ARN)

  Quy tắc: BR-ARN-02 — Hệ chủ nhà và hệ bị yếu

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-ARN-01 — Chỉ số khi vào sân
      Cho trước trận diễn ra ở sàn <sàn>
      Khi Anima hệ <hệ> có ATK <atk>, DEF 700, HP <hp> ra sân
      Thì Anima có ATK <atk mới>, DEF 700, HP <hp mới>

      Ví dụ:
        | sàn            | hệ       | atk  | hp   | atk mới | hp mới | giải thích             |
        | Thành Luminara | Luminara | 1600 | 2000 | 1840    | 2300   | chủ nhà +15%           |
        | Thành Luminara | Pyraxis  | 1500 | 1800 | 1350    | 1620   | bị Luminara khắc −10%  |
        | Thành Luminara | Aqualis  | 1500 | 1800 | 1500    | 1800   | không ảnh hưởng        |
        | Tháp Sấm       | Voltaris | 1550 | 1050 | 1780    | 1210   | 1782.5 → 1780; 1207.5 → 1210 |
        | Vết Nứt        | Pyraxis  | 1500 | 1800 | 1500    | 1800   | Vết Nứt không có hệ bị yếu |

    @R2 @tinhtoan
    Kịch bản: SC-ARN-02 — Kết hợp sàn và khắc chế
      Cho trước trận ở sàn Thành Luminara
      Và Anima Luminara của P1 có ATK hiệu lực 1,840
      Và Anima Pyraxis của P2 có DEF 700 và HP hiện tại 1,620
      Khi Anima Luminara tấn công Anima Pyraxis
      Thì Anima Pyraxis mất 1,430 HP (1,140 × 1.25 = 1,425 → 1,430)
      Và Anima Pyraxis còn 190 HP

  Quy tắc: BR-ARN-03 — Luật riêng

    @R2 @bien
    Kịch bản: SC-ARN-03 — Núi Cuồng Nộ không để DEF âm
      Cho trước trận ở sàn Núi Cuồng Nộ
      Khi Anima có ATK 1,000 và DEF 100 ra sân
      Thì Anima có ATK 1,200 và DEF 0

    @R2 @tinhtoan
    Kịch bản: SC-ARN-04 — Vết Nứt đột tử sớm
      Cho trước trận ở sàn Vết Nứt và Keeper của P1 còn 5,000 máu
      Khi P1 bắt đầu lượt thứ 8 của mình
      Thì Keeper của P1 còn 4,500 máu

  Quy tắc: BR-ARN-04 — Chọn sàn và bộ bài ở xếp hạng

    @R3 @bien
    Kịch bản: SC-ARN-05 — Không chọn bộ trong 15 giây
      Cho trước trận xếp hạng của P1 vừa được ghép, sàn công bố là Biển Hoài Niệm
      Và P1 có bộ "Thủy triều" được đặt làm bộ mặc định
      Khi 15 giây trôi qua mà P1 không chọn bộ
      Thì P1 vào trận với bộ "Thủy triều"

Tính năng: Cộng minh và Hợp thể (BR-FUS)
  Bối cảnh chung:
    Cho trước công thức Hợp thể "Nocturne — Tiếng khóc vỡ òa" = "Nocturne, the Silent Tear" + "Quietshade" (cùng hệ Umbryx, cùng mạch "Người khóc thầm"), giá 3 Cộng hưởng
    Và chỉ số công thức: ATK = 70% tổng ATK, HP = 70% tổng HP, DEF = DEF cao hơn

  Quy tắc: BR-FUS-02 — Cộng minh

    @R2 @tinhtoan
    Sơ đồ kịch bản: SC-FUS-01 — Bonus theo số thẻ cùng mạch
      Cho trước sân của P1 có <số thẻ> Anima cùng mạch "Người khóc thầm", mỗi thẻ ATK 1,000 và HP 1,500 trước bonus
      Khi trạng thái sân được tính lại
      Thì mỗi thẻ cùng mạch có ATK <atk> và HP <hp>

      Ví dụ:
        | số thẻ | atk  | hp   |
        | 1      | 1000 | 1500 |
        | 2      | 1100 | 1700 |
        | 3      | 1200 | 1900 |

  Quy tắc: BR-FUS-03 / BR-FUS-04 — Hợp thể

    @R2 @happy @tinhtoan
    Kịch bản: SC-FUS-02 — Hợp thể hợp lệ
      Cho trước sân của P1 có "Nocturne" (ATK 1,200, DEF 600, HP 1,800) và "Quietshade" (ATK 1,000, DEF 700, HP 1,400)
      Và P1 còn 3 Cộng hưởng trong lượt
      Khi P1 Hợp thể hai thẻ đó
      Thì "Nocturne — Tiếng khóc vỡ òa" xuất hiện với ATK 1,540, DEF 700, HP 2,240
      Và "Nocturne" và "Quietshade" vào mộ
      Và P1 còn 0 Cộng hưởng
      Và dạng Hợp thể không tấn công được trong lượt này

    @R2 @negative
    Sơ đồ kịch bản: SC-FUS-03 — Hợp thể không hợp lệ
      Cho trước <điều kiện>
      Khi P1 yêu cầu Hợp thể
      Thì server từ chối với mã lỗi <mã lỗi>

      Ví dụ:
        | điều kiện                                                    | mã lỗi                    |
        | hai thẻ khác mạch truyện                                     | FUSION_RECIPE_NOT_FOUND   |
        | hai thẻ cùng mạch nhưng hệ không giống và không liền nhau    | FUSION_RECIPE_NOT_FOUND   |
        | P1 chỉ còn 2 Cộng hưởng                                      | INSUFFICIENT_RESONANCE    |
        | P1 đã Hợp thể một lần trong lượt này                         | FUSION_LIMIT_PER_TURN     |

    @R2 @happy
    Kịch bản: SC-FUS-04 — Hợp thể khác hệ theo nhân quả
      Cho trước công thức "Emberfang — Cơn giận sinh từ nỗi sợ" = một Anima Umbryx + "Emberfang" cùng mạch "Đứa trẻ bị bỏ rơi"
      Khi P1 Hợp thể hai thẻ đó với đủ Cộng hưởng
      Thì dạng Hợp thể xuất hiện (Sợ hãi sinh Giận dữ, hai hệ liền nhau trong vòng sinh)

  Quy tắc: BR-FUS-05 — Thẻ Hợp thể bản sưu tầm

    @R2 @happy
    Kịch bản: SC-FUS-05 — Chỉ đổi hình hiển thị
      Cho trước P1 sở hữu thẻ sưu tầm "Nocturne — Tiếng khóc vỡ òa" bản art đặc biệt
      Khi P1 Hợp thể "Nocturne" và "Quietshade"
      Thì dạng Hợp thể hiển thị art đặc biệt
      Và chỉ số giống hệt SC-FUS-02

Tính năng: Chế độ chơi và Arena Point (BR-PVP)

  Quy tắc: BR-PVP-03 — AP không có giá trị quy đổi

    @R3 @negative
    Sơ đồ kịch bản: SC-PVP-01 — Thao tác bị cấm với AP
      Khi P1 gửi yêu cầu <hành động>
      Thì server trả mã lỗi NOT_SUPPORTED

      Ví dụ:
        | hành động                        |
        | mua 1,000 AP bằng Gem            |
        | đổi 500 AP sang Coin             |
        | chuyển 100 AP cho P2             |
        | dùng AP mua pack                 |

  Quy tắc: BR-PVP-04 — Thách đấu cược

    @R3 @happy @tinhtoan
    Kịch bản: SC-PVP-02 — Người thắng nhận cả hai phần cược
      Cho trước P1 và P2 mỗi người có 500 AP và đồng ý cược 200 AP
      Khi trận bắt đầu
      Thì mỗi người còn 300 AP khả dụng, 200 AP bị giữ
      Và khi P1 thắng, P1 có 700 AP và P2 có 300 AP

    @R3 @bien
    Sơ đồ kịch bản: SC-PVP-03 — Mức cược
      Cho trước P1 có 600 AP
      Khi P1 đề nghị cược <mức> AP
      Thì kết quả là <kết quả>

      Ví dụ:
        | mức  | kết quả               |
        | 9    | WAGER_OUT_OF_RANGE    |
        | 10   | thành công            |
        | 600  | thành công            |
        | 601  | INSUFFICIENT_AP       |

    @R3 @happy
    Kịch bản: SC-PVP-04 — Hủy trước khi bắt đầu
      Cho trước P1 và P2 đã đồng ý cược 200 AP nhưng trận chưa bắt đầu
      Khi P2 hủy thách đấu
      Thì không có AP nào bị giữ hoặc chuyển

    @R3 @negative
    Kịch bản: SC-PVP-05 — Mất kết nối trong trận cược
      Cho trước trận cược 200 AP giữa P1 và P2 đang diễn ra
      Khi P2 mất kết nối quá 60 giây
      Thì P1 thắng và nhận 400 AP tạm giữ

  Quy tắc: BR-PVP-06 — Ghép trận

    @R3 @negative
    Kịch bản: SC-PVP-06 — Hai tài khoản cùng thiết bị
      Cho trước P1 và "P1b" từng đăng nhập trên cùng một thiết bị
      Khi P1 thách đấu cược P1b
      Thì server từ chối với mã lỗi MATCH_NOT_ALLOWED

    @R3 @bien
    Sơ đồ kịch bản: SC-PVP-07 — Giới hạn trận giữa một cặp mỗi ngày
      Cho trước P1 và P2 đã đấu <số trận> trận xếp hạng hoặc cược với nhau hôm nay
      Khi P1 thách đấu cược P2
      Thì kết quả là <kết quả>

      Ví dụ:
        | số trận | kết quả             |
        | 2       | thành công          |
        | 3       | PAIR_DAILY_LIMIT    |

  Quy tắc: BR-PVP-07 — Dàn xếp trận

    @R3 @negative
    Kịch bản: SC-PVP-08 — Đầu hàng sớm lặp lại
      Cho trước P2 đã đầu hàng P1 trong 2 lượt đầu ở 2 trận cược trong 7 ngày
      Khi P2 đầu hàng P1 trong 2 lượt đầu lần thứ 3
      Thì cặp P1–P2 được gắn cờ cho Fraud Analyst

  Quy tắc: BR-PVP-08 — Thể thức

    @R3 @negative
    Sơ đồ kịch bản: SC-PVP-09 — Thẻ không hợp lệ trong thể thức
      Cho trước mùa hiện tại là S4
      Khi P1 vào trận <thể thức> với bộ có <thẻ>
      Thì kết quả là <kết quả>

      Ví dụ:
        | thể thức | thẻ                                  | kết quả                     |
        | Standard | thẻ của mùa S3                       | thành công                  |
        | Standard | thẻ của mùa S2                       | CARD_NOT_LEGAL_IN_FORMAT    |
        | Eternal  | thẻ của mùa S1                       | thành công                  |
        | Eternal  | thẻ trong danh sách cấm              | CARD_BANNED                 |

  Quy tắc: BR-PVP-09 — Tắt theo quốc gia

    @R3 @negative
    Kịch bản: SC-PVP-10 — Thách đấu cược bị tắt ở thị trường
      Cho trước ma trận quốc gia tắt "thách đấu cược" cho quốc gia pháp lý của P1
      Khi P1 tạo thách đấu cược
      Thì server từ chối với mã lỗi FEATURE_NOT_AVAILABLE_IN_REGION

Tính năng: Người chơi mới (BR-NEW)

  Quy tắc: BR-NEW-01 — Gói chào mừng

    @R1 @happy
    Kịch bản: SC-NEW-01 — Nội dung gói chào mừng
      Khi người dùng "N1" tạo tài khoản thành công
      Thì N1 nhận 5 Card Instance Anima rarity Common
      Và 5 thẻ thuộc 5 hệ khác nhau trong {Umbryx, Pyraxis, Aqualis, Terrakin, Ventara, Voltaris, Luminara}
      Và không thẻ nào thuộc hệ Nihilum hoặc là bài hỗ trợ
      Và cả 5 thẻ được đánh dấu gắn chặt tài khoản

  Quy tắc: BR-NEW-04 — Thẻ gắn chặt tài khoản

    @R1 @negative
    Sơ đồ kịch bản: SC-NEW-02 — Thao tác bị cấm với thẻ tặng
      Cho trước N1 có thẻ chào mừng "Driftkoi #30012"
      Khi N1 <hành động> "Driftkoi #30012"
      Thì server từ chối với mã lỗi <mã lỗi>

      Ví dụ:
        | hành động         | mã lỗi              |
        | niêm yết          | CARD_NOT_TRADABLE   |
        | rút về ví NFT     | CARD_NOT_TRADABLE   |
        | đưa vào Lò rèn    | CARD_NOT_FORGEABLE  |

  Quy tắc: BR-NEW-03 — Nhiệm vụ Tân thủ

    @R1 @happy
    Kịch bản: SC-NEW-03 — Hoàn thành nhiệm vụ ngày 3
      Cho trước N1 tạo tài khoản ngày 2026-10-06 và đang ở ngày Tân thủ thứ 3
      Khi N1 hoàn thành mọi nhiệm vụ của ngày 3
      Thì N1 nhận 1 pack cơ bản 5 Anima Common gắn chặt tài khoản

    @R1 @bien
    Sơ đồ kịch bản: SC-NEW-04 — Làm bù nhiệm vụ
      Cho trước N1 tạo tài khoản ngày 2026-10-06 và chưa hoàn thành nhiệm vụ ngày 2
      Khi N1 hoàn thành nhiệm vụ ngày 2 vào ngày <ngày>
      Thì kết quả là <kết quả>

      Ví dụ:
        | ngày       | kết quả                             |
        | 2026-10-09 | nhận pack cơ bản của ngày 2         |
        | 2026-10-12 | nhận pack cơ bản của ngày 2 (ngày 7)|
        | 2026-10-13 | QUEST_EXPIRED                       |

    @R1 @happy @cho-Q53
    Kịch bản: SC-NEW-05 — Thưởng ngày 6
      Cho trước N1 đang ở ngày Tân thủ thứ 6
      Khi N1 hoàn thành mọi nhiệm vụ của ngày 6
      Thì N1 nhận 200 Coin

  Quy tắc: BR-NEW-05 — Không quá 2 bản trong thẻ Tân thủ

    @R1 @bien
    Kịch bản: SC-NEW-06 — Pack cơ bản tránh bản thứ 3
      Cho trước N1 đã có 2 bản "Driftkoi" gắn chặt tài khoản
      Khi N1 mở một pack cơ bản
      Thì không thẻ nào trong pack là "Driftkoi"

    @R1 @happy
    Kịch bản: SC-NEW-07 — Đủ 30 lá sau ngày 5
      Cho trước N1 có gói chào mừng và đã nhận 5 pack cơ bản
      Khi N1 tạo bộ bài từ 30 thẻ gắn chặt tài khoản
      Thì bộ bài hợp lệ theo BR-DECK-01 → 03

  Quy tắc: BR-NEW-06 — Trận hướng dẫn

    @R2 @happy
    Kịch bản: SC-NEW-08 — Trận hướng dẫn không đổi bộ sưu tập
      Cho trước N1 vừa nhận gói chào mừng và có 5 thẻ
      Khi N1 chơi xong trận hướng dẫn bằng bộ bài mượn
      Thì N1 vẫn có đúng 5 thẻ
      Và tài khoản được đánh dấu đã hoàn thành hướng dẫn
      Và N1 không nhận Coin, Gem hay thẻ từ trận này

  Quy tắc: BR-PVP-02 — Chưa đủ 30 lá

    @R2 @negative
    Kịch bản: SC-NEW-09 — Người mới vào trận giao hữu khi mới có 5 thẻ
      Cho trước N1 chỉ có 5 thẻ
      Khi N1 gửi yêu cầu vào trận giao hữu
      Thì server từ chối với mã lỗi DECK_INVALID
```

---

## 13. Rà soát edge case

| Nhóm | Kịch bản phủ | Ghi chú |
|---|---|---|
| Đồng thời | SC-PACK-13, SC-MKT-02, SC-PACK-05, SC-MKT-14 | |
| Ranh giới thời gian | SC-ACC-10/11/13/20/21, SC-CHK-02/07, SC-ADS-03/09/10, SC-ECO-03, SC-PACK-17, SC-MKT-05/06/15/22, SC-FRD-03 | |
| Snapshot & bất biến | SC-PACK-08/11/17/18, SC-WAL-04, SC-ADM-05/09 | |
| Đảo ngược | SC-WAL-02/05/11–15, SC-PACK-25, SC-MKT-28–30 | |
| Dữ liệu thiếu | SC-ADS-05, SC-WAL-08, SC-ADM-11, SC-MKT-04 | |
| Danh tính | SC-ACC-06/07/08, SC-REF-04/06, SC-MKT-13/23/24 | |
| Tích hợp lỗi/trùng | SC-WAL-01/07/08/15, SC-ADS-04/05/12 | OTP provider lỗi: SC-ACC-15 phủ giới hạn; lỗi gửi SMS viết ở FRD |
| Cộng đồng (EP-08) | Chưa viết | Chưa có Business Rule trong BRD; viết khi chốt phạm vi R2 |
| Battle Pass, thành tựu | Chưa viết | Chưa có thiết kế (Q-28) |

---

## 14. Traceability: Business Rule → Kịch bản

| BR | Happy | Biên | Negative / Unauthorized |
|---|---|---|---|
| BR-ACC-01 | SC-ACC-01 | SC-ACC-02 | SC-ACC-03, SC-ACC-04 |
| BR-ACC-02 | SC-ACC-05 | SC-ACC-06, SC-ACC-07 | SC-ACC-06 |
| BR-ACC-03 | SC-ACC-09 | SC-ACC-10, SC-ACC-11 | SC-ACC-08 |
| BR-ACC-04 | SC-PERM-01 | SC-PERM-01 | SC-PERM-01 |
| BR-ACC-05 | SC-ACC-12 | SC-ACC-13, SC-ACC-14 | SC-ACC-15, SC-ACC-16 |
| BR-WAL-01 | SC-WAL-03, SC-WAL-05 | — (không có ngưỡng) | SC-WAL-04 |
| BR-WAL-02 | SC-WAL-06, SC-WAL-09 | SC-WAL-08 | SC-WAL-01, SC-WAL-07 |
| BR-WAL-03 | SC-PACK-07 | SC-PACK-07 | SC-PACK-06, SC-WAL-10 |
| BR-WAL-04 | SC-WAL-11, SC-WAL-13 | SC-WAL-12, SC-WAL-14 | SC-WAL-02, SC-WAL-15 |
| BR-WAL-05 | SC-WAL-16, SC-WAL-19, SC-WAL-20 | SC-WAL-17 | SC-WAL-18, SC-WAL-21 |
| BR-WAL-06 | SC-WAL-22 | SC-WAL-22 | SC-WAL-22, SC-WAL-23 |
| BR-ECO-01 | — (chỉ có hành vi chặn) | — | SC-ECO-01 |
| BR-ECO-02 | — (chỉ có hành vi chặn) | — | SC-ECO-02 |
| BR-ECO-03 | SC-ECO-03 | SC-ECO-03 | SC-ECO-04 |
| BR-ECO-04 | SC-ECO-05 | SC-ECO-05 | SC-ECO-05 |
| BR-PACK-01 | SC-PACK-10 | SC-PACK-11 | SC-PACK-11 |
| BR-PACK-02 | SC-PACK-01 | SC-PACK-02, SC-PACK-03 | SC-PACK-04, SC-PACK-12, SC-PACK-13 |
| BR-PACK-03 | SC-PACK-16 | SC-PACK-16 | — (kiểm định thống kê) |
| BR-PACK-04 | SC-PACK-08, SC-PACK-18 | SC-PACK-17 | SC-PACK-15 |
| BR-PACK-05 | SC-PACK-09, SC-PACK-20 | SC-PACK-09 | SC-PACK-19 |
| BR-PACK-06 | SC-PACK-21 | — (không có ngưỡng) | — |
| BR-PACK-07 | SC-PACK-22 | SC-PACK-22 | SC-PACK-22 |
| BR-PACK-08 | SC-PACK-01 | SC-PACK-23 | — |
| BR-PACK-09 | SC-PACK-24 | — | — |
| BR-CHK-01 | SC-CHK-02 | SC-CHK-02 | SC-CHK-03, SC-CHK-07 |
| BR-CHK-02 | SC-CHK-01, SC-CHK-08 | SC-CHK-08 | — |
| BR-CHK-03 | SC-CHK-08, SC-CHK-09 | SC-CHK-10 | — |
| BR-CHK-04 | — | — | SC-CHK-04 |
| BR-CHK-05 | SC-CHK-05, SC-CHK-11 | SC-CHK-12, SC-CHK-14 | SC-CHK-13 |
| BR-CHK-06 | — | SC-CHK-06 | SC-CHK-06 |
| BR-ADS-01 | SC-ADS-11 | SC-ADS-07, SC-ADS-08 | SC-ADS-02 |
| BR-ADS-02 | SC-ADS-09 | SC-ADS-03, SC-ADS-09 | SC-ADS-03 |
| BR-ADS-03 | SC-ADS-10 | SC-ADS-10 | SC-ADS-10 |
| BR-ADS-04 | SC-ADS-11 | SC-ADS-05 | SC-ADS-04, SC-ADS-12 |
| BR-ADS-05 | SC-ADS-01, SC-ADS-13 | SC-ADS-07, SC-ADS-14 | — |
| BR-ADS-06 | SC-ADS-15 | SC-ADS-15 | SC-ADS-15 |
| BR-REF-01 | SC-REF-01 | SC-REF-02 | SC-REF-03, SC-REF-04 |
| BR-REF-02 | SC-REF-05 | SC-REF-05 | SC-REF-05 |
| BR-REF-03 | — | — | SC-REF-06 |
| BR-MKT-01 | SC-PERM-01 | — | SC-MKT-04, SC-MKT-07 |
| BR-MKT-02 | — | — | SC-MKT-08, SC-MKT-09 |
| BR-MKT-03 | — | — | SC-MKT-03 |
| BR-MKT-04 | SC-MKT-10 | SC-MKT-10 | SC-MKT-10 |
| BR-MKT-05 | SC-MKT-01, SC-MKT-12 | SC-MKT-01 | — |
| BR-MKT-06 | SC-MKT-12 | — | SC-MKT-11 |
| BR-MKT-07 | SC-MKT-17 | SC-MKT-17 | SC-MKT-16 |
| BR-MKT-08 | SC-MKT-18, SC-MKT-20 | SC-MKT-18, SC-MKT-19 | SC-MKT-21 |
| BR-MKT-09 | SC-MKT-05 | SC-MKT-06 | SC-MKT-22 |
| BR-MKT-10 | — | — | SC-MKT-23, SC-MKT-24 |
| BR-MKT-11 | SC-MKT-28 | SC-MKT-29 | SC-MKT-30 |
| BR-FRD-01 | SC-FRD-01 | SC-FRD-01 | SC-ADS-06, SC-CHK-15 |
| BR-FRD-02 | SC-ADS-15 | SC-ADS-15 | SC-ADS-15 |
| BR-FRD-03 | — | — | SC-FRD-02 |
| BR-FRD-04 | SC-FRD-03 | SC-FRD-03 | SC-FRD-03 |
| BR-ADM-01 | SC-ADM-04, SC-ADM-06 | — | SC-ADM-05, SC-ADM-07 |
| BR-ADM-02 | SC-ADM-08 | SC-ADM-02 | SC-ADM-01, SC-ADM-03 |
| BR-ADM-03 | — | — | SC-ADM-09 |
| BR-ADM-04 | SC-ADM-12 | — | SC-ADM-10, SC-ADM-11 |
| BR-WEB-01 | SC-WEB-01 | SC-WEB-02, SC-WEB-03 | SC-WEB-03 |
| BR-WEB-02 | SC-WEB-06 | SC-WEB-04 | SC-WEB-05 |
| BR-WEB-03 | — (rule cấm) | — | SC-WEB-07 |
| BR-WEB-04 | SC-WEB-08 | SC-WEB-09 | SC-WEB-10, SC-WEB-11 |
| BR-WEB-05 | Viết khi chốt Q-33, Q-34 | — | — |
| BR-WEB-06 | SC-WEB-12 | SC-WEB-12 | — |
| BR-WEB-07 | Dùng chung SC-ACC-04 trên kênh web | — | SC-ACC-04 |
| BR-ECO-01 (sửa CR-002) | — (rule cấm) | — | SC-ECO-01, SC-ECO-06 |
| BR-SUP-01/02 | SC-SUP-01 | SC-SUP-01 | — |
| BR-SUP-03 | SC-SUP-02 | SC-SUP-01 | — |
| BR-SUP-04 | — | — | SC-SUP-03 |
| BR-SUP-05 | SC-SUP-04 | — | SC-SUP-04 |
| BR-SUP-06 | SC-FRG-01 | — | — |
| BR-PF-01/02 | SC-PF-01 | — | — |
| BR-PF-03 | SC-PF-02 | — | SC-PF-04 |
| BR-PF-04 | SC-PF-03 | SC-PF-05 | SC-PF-04 |
| BR-PF-05 | SC-FRG-05 | — | — |
| BR-PF-06 | Viết khi chốt chuỗi (T-09) | — | — |
| BR-FRG-01 | SC-FRG-01 | SC-FRG-09 | SC-FRG-04, SC-FRG-09 |
| BR-FRG-02 | SC-FRG-01, SC-FRG-02 | — | SC-FRG-03 |
| BR-FRG-03 | SC-FRG-06 | SC-FRG-05 | SC-FRG-05 |
| BR-FRG-04 | SC-SUP-04 | — | — |
| BR-FRG-05 | — | — | SC-FRG-04 |
| BR-FRG-06 | — | — | SC-FRG-07 |
| BR-FRG-07 | SC-FRG-08 | SC-FRG-08 | SC-FRG-08 |
| BR-NFT-01 | — | — | SC-NFT-03 (nền tảng, tạm dừng) |
| BR-NFT-02 | SC-NFT-01 | SC-NFT-02 | SC-NFT-03 |
| BR-NFT-03 | SC-NFT-01 | — | SC-NFT-06, SC-NFT-07 |
| BR-NFT-04 | SC-NFT-01 | SC-NFT-04 | — |
| BR-NFT-05 | SC-NFT-08, SC-NFT-09 | SC-NFT-11 | SC-NFT-10 |
| BR-NFT-06, 07 | Kiểm tra trong audit smart contract (không phải hành vi API) | — | — |
| BR-NFT-08 | — | — | SC-NFT-12 |
| BR-NFT-09 | SC-NFT-05 | — | — |
| BR-NFT-10 | — | — | SC-NFT-03 |
| BR-GEO-01 | SC-GEO-01 | SC-GEO-01 | SC-GEO-02 |
| BR-GEO-02 | SC-GEO-03 | — | SC-GEO-03, SC-GEO-04 |
| BR-GEO-03 | — (rule cấm) | — | SC-GEO-05 |
| BR-GEO-04 | SC-GEO-06 | SC-GEO-06 | SC-GEO-06 |
| BR-GEO-05 | SC-GEO-07 | SC-GEO-07 | — |
| BR-GEO-06 | Kiểm tra cấu hình khi thêm tính năng thưởng theo bộ (hiện chưa có tính năng nào) | — | — |
| BR-GEO-07 | Kiểm tra trên store và cổng thanh toán từng nước | — | — |
| BR-GEO-08 | SC-GEO-08 | — | — |
| BR-I18N-01 | SC-I18N-01, SC-I18N-02 | SC-I18N-01 | — |
| BR-I18N-02, 03 | — | SC-I18N-04 | SC-I18N-03 |
| BR-I18N-04 | SC-I18N-05 | — | — |
| BR-I18N-05, 06, 07 | Kiểm thử bản địa hóa (QA) | — | — |
| BR-CARD-01 → 07 | Kiểm tra dữ liệu Card Definition khi phát hành (validator Catalog) | — | — |
| BR-DECK-01 → 03 | SC-DECK-01 | SC-DECK-01 | SC-DECK-01 |
| BR-DECK-04 | SC-DECK-07 | — | SC-DECK-01 |
| BR-DECK-05 | SC-DECK-02 | SC-DECK-02 | SC-DECK-02 |
| BR-DECK-06, 07 | SC-DECK-03 | — | SC-DECK-04 |
| BR-DECK-08 | SC-DECK-06 | — | SC-DECK-05 |
| BR-BTL-02 | — | — | SC-BTL-11 |
| BR-BTL-03 | SC-BTL-01 | SC-BTL-01 | — |
| BR-BTL-04 | — | — | SC-BTL-02 |
| BR-BTL-05 | SC-BTL-03, SC-BTL-05 | SC-BTL-03 | SC-BTL-04 |
| BR-BTL-06 | — | SC-BTL-06 | — |
| BR-BTL-07 | — | SC-BTL-10 | SC-BTL-09 |
| BR-BTL-08 | SC-BTL-08 | SC-BTL-08 | SC-BTL-08 |
| BR-BTL-09 | SC-BTL-07 | SC-BTL-07 | — |
| BR-BTL-10 | SC-BTL-12 | — | — |
| BR-BTL-11 | Đo bằng mô phỏng và analytics | — | — |
| BR-ELM-01, 02, 04 | SC-ELM-01 | — | SC-ELM-01 |
| BR-ELM-03 | SC-ELM-01 | — | SC-ELM-04 |
| BR-ELM-05 | SC-ELM-02 | — | SC-ELM-03 |
| BR-ELM-06 | SC-ELM-05 | SC-ELM-05 | SC-ELM-05 |
| BR-ARN-02 | SC-ARN-01, SC-ARN-02 | SC-ARN-01 | — |
| BR-ARN-03 | SC-ARN-04 | SC-ARN-03 | — |
| BR-ARN-04 | — | SC-ARN-05 | — |
| BR-FUS-02 | SC-FUS-01 | SC-FUS-01 | — |
| BR-FUS-03, 04 | SC-FUS-02, SC-FUS-04 | — | SC-FUS-03 |
| BR-FUS-05 | SC-FUS-05 | — | — |
| BR-PVP-03 | — (rule cấm) | — | SC-PVP-01 |
| BR-PVP-04 | SC-PVP-02, SC-PVP-04 | SC-PVP-03 | SC-PVP-05 |
| BR-PVP-06 | — | SC-PVP-07 | SC-PVP-06 |
| BR-PVP-07 | — | — | SC-PVP-08 |
| BR-PVP-08 | SC-PVP-09 | — | SC-PVP-09 |
| BR-PVP-09 | — | — | SC-PVP-10 |
| BR-NEW-01 | SC-NEW-01 | — | — |
| BR-NEW-03 | SC-NEW-03, SC-NEW-05 | SC-NEW-04 | SC-NEW-04 |
| BR-NEW-04 | — | — | SC-NEW-02 |
| BR-NEW-05 | SC-NEW-07 | SC-NEW-06 | — |
| BR-NEW-06 | SC-NEW-08 | — | — |
| BR-PVP-02 | — | — | SC-NEW-09, SC-DECK-04 |

Ô "—" ở cột Happy/Biên nghĩa là rule đó chỉ mô tả hành vi chặn hoặc không có giá trị ngưỡng; lý do được ghi trong ô. Các rule chỉ có cột Negative (BR-CHK-04, BR-REF-03, BR-MKT-02/03/10, BR-FRD-03, BR-ADM-03) là rule cấm; hành vi hợp lệ tương ứng đã nằm trong kịch bản happy của rule khác.

## 15. Số liệu

- 27 tính năng, 223 kịch bản đơn và 50 sơ đồ kịch bản (mỗi dòng trong bảng `Ví dụ` là một trường hợp kiểm thử).
- 23 kịch bản/sơ đồ có tag `@cho-*` và 2 dòng ví dụ được đánh dấu `@cho-*`, sẽ cập nhật khi PO trả lời câu hỏi mở trong BRD.

## 16. Bước tiếp theo

1. PO rà các kịch bản có tag `@cho-*` cùng lúc với việc trả lời câu hỏi mở trong BRD.
2. QA dùng tài liệu này làm nền cho test case.
3. Khi bắt đầu code backend, chuyển các khối Gherkin thành file `.feature` chạy bằng Reqnroll. Cần kiểm tra bộ từ khóa tiếng Việt Reqnroll hỗ trợ và thống nhất cách viết "Cho trước" với từ khóa chuẩn.

---

*End of Document*
