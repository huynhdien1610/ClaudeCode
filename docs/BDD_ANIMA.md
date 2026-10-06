# BDD — ANIMA: Echoes of the Heart
## Đặc tả hành vi (Behavior-Driven Development)

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | BDD-ANIMA-001 |
| Phiên bản | 0.1 (Draft) |
| Ngày | 2026-10-06 |
| Nguồn | [BRD](BRD_ANIMA.md) v0.2, [PRD](PRD_ANIMA.md) v0.1 |
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
| Đổi Gem → Coin | 1 Gem = 9 Coin (`@cho-Q05`) |
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

    @R2 @negative @cho-Q05
    Kịch bản: SC-WAL-19 — Đổi Coin sang Gem
      Cho trước P1 có 9,000 Coin
      Khi P1 gửi yêu cầu đổi Coin sang Gem
      Thì server từ chối với mã lỗi CONVERSION_NOT_SUPPORTED
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
| BR-WAL-05 | SC-WAL-16 | SC-WAL-17 | SC-WAL-18, SC-WAL-19 |
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

Ô "—" ở cột Happy/Biên nghĩa là rule đó chỉ mô tả hành vi chặn hoặc không có giá trị ngưỡng; lý do được ghi trong ô. Các rule chỉ có cột Negative (BR-CHK-04, BR-REF-03, BR-MKT-02/03/10, BR-FRD-03, BR-ADM-03) là rule cấm; hành vi hợp lệ tương ứng đã nằm trong kịch bản happy của rule khác.

## 15. Số liệu

- 12 tính năng, 139 kịch bản đơn và 21 sơ đồ kịch bản (mỗi dòng trong bảng `Ví dụ` là một trường hợp kiểm thử).
- 19 kịch bản/sơ đồ có tag `@cho-*` và 2 dòng ví dụ được đánh dấu `@cho-*`, sẽ cập nhật khi PO trả lời câu hỏi mở trong BRD.

## 16. Bước tiếp theo

1. PO rà các kịch bản có tag `@cho-*` cùng lúc với việc trả lời câu hỏi mở trong BRD.
2. QA dùng tài liệu này làm nền cho test case.
3. Khi bắt đầu code backend, chuyển các khối Gherkin thành file `.feature` chạy bằng Reqnroll. Cần kiểm tra bộ từ khóa tiếng Việt Reqnroll hỗ trợ và thống nhất cách viết "Cho trước" với từ khóa chuẩn.

---

*End of Document*
