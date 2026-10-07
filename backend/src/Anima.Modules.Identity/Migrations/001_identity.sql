CREATE SCHEMA IF NOT EXISTS identity;

-- PII (email, SĐT, ngày sinh) mã hóa AES-GCM ở tầng ứng dụng; *_hash (HMAC) dùng để tra cứu và kiểm tra trùng.
CREATE TABLE identity.account (
  id                      uuid        PRIMARY KEY,
  email_enc               bytea       NOT NULL,
  email_hash              bytea       NOT NULL UNIQUE,
  password_hash           text        NOT NULL,
  phone_enc               bytea       NULL,
  phone_hash              bytea       NULL,
  phone_verified_at       timestamptz NULL,
  birth_date_enc          bytea       NOT NULL,
  legal_country           char(2)     NOT NULL,
  locale                  text        NOT NULL CHECK (locale IN ('vi','en','zh-Hans','zh-Hant')),
  timezone                text        NOT NULL,
  status                  text        NOT NULL CHECK (status IN ('Unverified','Verified','Restricted','Banned','PendingDeletion','Deleted')),
  status_before_restriction text      NULL,
  restriction_reason      text        NULL CHECK (restriction_reason IN ('FRAUD','NEGATIVE_GEM')),
  failed_logins           int         NOT NULL DEFAULT 0,
  locked_until            timestamptz NULL,
  created_at              timestamptz NOT NULL DEFAULT now()
);
-- Một SĐT chỉ xác thực cho một tài khoản (BR-ACC-02).
CREATE UNIQUE INDEX ux_account_phone_verified ON identity.account (phone_hash) WHERE phone_verified_at IS NOT NULL;

CREATE TABLE identity.otp (
  account_id   uuid        PRIMARY KEY REFERENCES identity.account(id),
  phone_hash   bytea       NOT NULL,
  phone_enc    bytea       NOT NULL,
  code_hash    bytea       NOT NULL,
  expires_at   timestamptz NOT NULL,
  attempts     int         NOT NULL DEFAULT 0,
  locked_until timestamptz NULL,
  sent_on      date        NOT NULL,
  sent_count   int         NOT NULL
);
