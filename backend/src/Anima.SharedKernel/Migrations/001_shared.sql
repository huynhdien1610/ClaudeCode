CREATE SCHEMA IF NOT EXISTS shared;

-- Idempotency (SC-PACK-05): cùng (tài khoản, key) trả lại response cũ; khác nội dung thì từ chối.
CREATE TABLE shared.idempotency (
  account_id   uuid        NOT NULL,
  key          text        NOT NULL,
  endpoint     text        NOT NULL,
  request_hash text        NOT NULL,
  response     jsonb       NULL,
  created_at   timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (account_id, key)
);
