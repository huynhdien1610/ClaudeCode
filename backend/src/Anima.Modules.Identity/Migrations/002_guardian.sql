-- Đồng ý của người giám hộ cho tài khoản dưới 18 tuổi (BR-ACC-01, BR-WEB-07). Cơ chế tối thiểu; phạm vi cuối cùng chờ Legal (Q-21).
ALTER TABLE identity.account ADD COLUMN guardian_consent_at timestamptz NULL;

CREATE TABLE identity.guardian_request (
  account_id  uuid        PRIMARY KEY,
  email_enc   bytea       NOT NULL,        -- email người giám hộ, mã hóa như PII khác
  token_hash  bytea       NOT NULL,        -- chỉ lưu băm của mã xác nhận gửi qua email
  expires_at  timestamptz NOT NULL,
  created_at  timestamptz NOT NULL
);
CREATE UNIQUE INDEX ux_guardian_token ON identity.guardian_request (token_hash);
