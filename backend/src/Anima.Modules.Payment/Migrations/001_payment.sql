CREATE SCHEMA IF NOT EXISTS payment;

-- Bảng giá gói Gem trên web (BR-WEB-05, công khai). Giá dưới đây là giá tạm chờ PO chốt (Q-33).
CREATE TABLE payment.gem_package (
  code         text    PRIMARY KEY,
  gem          bigint  NOT NULL CHECK (gem > 0),
  price_minor  bigint  NOT NULL CHECK (price_minor > 0),   -- đơn vị nhỏ nhất của tiền tệ (VND không có phần lẻ)
  currency     text    NOT NULL,
  sort         int     NOT NULL,
  active       boolean NOT NULL DEFAULT true
);
INSERT INTO payment.gem_package(code,gem,price_minor,currency,sort) VALUES
  ('gem_100',  100,   25000, 'VND', 1),
  ('gem_550',  550,  125000, 'VND', 2),
  ('gem_1200', 1200, 259000, 'VND', 3),
  ('gem_6500', 6500, 1290000, 'VND', 4);

CREATE TABLE payment.payment_order (
  id                uuid        PRIMARY KEY,
  account_id        uuid        NOT NULL,
  package_code      text        NOT NULL REFERENCES payment.gem_package(code),
  gem               bigint      NOT NULL,                       -- chốt tại lúc tạo đơn; hoàn tiền thu hồi đúng số này
  amount_minor      bigint      NOT NULL,
  currency          text        NOT NULL,
  provider          text        NOT NULL,
  status            text        NOT NULL CHECK (status IN ('Created','Paid','Failed','Refunded')),
  gateway_txn_id    text        NULL,
  credit_entry_id   bigint      NULL,
  clawback_entry_id bigint      NULL,
  created_at        timestamptz NOT NULL,
  paid_at           timestamptz NULL,
  refunded_at       timestamptz NULL
);
-- BR-WEB-04: mỗi gateway transaction ID chỉ được ghi nhận một lần.
CREATE UNIQUE INDEX ux_order_gateway_txn ON payment.payment_order (provider, gateway_txn_id) WHERE gateway_txn_id IS NOT NULL;
CREATE INDEX ix_order_account ON payment.payment_order (account_id, created_at DESC);

-- Nhật ký mọi webhook nhận được, kể cả chữ ký sai (log gian lận, SC-WAL-07). Chỉ thêm.
CREATE TABLE payment.webhook_log (
  id              bigserial   PRIMARY KEY,
  received_at     timestamptz NOT NULL,
  provider        text        NOT NULL,
  signature_valid boolean     NOT NULL,
  event_type      text        NULL,
  order_id        uuid        NULL,
  gateway_txn_id  text        NULL,
  outcome         text        NOT NULL,
  body            text        NOT NULL
);
