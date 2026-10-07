CREATE SCHEMA IF NOT EXISTS fairness;

CREATE TABLE fairness.seed (
  id               uuid        PRIMARY KEY,
  account_id       uuid        NOT NULL,
  server_seed_enc  bytea       NOT NULL,          -- AES-GCM; chỉ giải mã khi quay hoặc công bố
  server_seed_hash text        NOT NULL,          -- SHA-256 hex, công bố trước lần quay đầu (BR-PF-03)
  client_seed      text        NOT NULL,
  next_nonce       int         NOT NULL DEFAULT 1 CHECK (next_nonce >= 1),
  status           text        NOT NULL CHECK (status IN ('active','revealed')),
  created_at       timestamptz NOT NULL DEFAULT now(),
  revealed_at      timestamptz NULL
);
-- Một seed active mỗi tài khoản; mở pack và đổi seed khóa cùng dòng này (SC-PF-05).
CREATE UNIQUE INDEX ux_seed_active ON fairness.seed (account_id) WHERE status = 'active';
CREATE INDEX ix_seed_account ON fairness.seed (account_id, created_at);
