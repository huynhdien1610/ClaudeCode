CREATE SCHEMA IF NOT EXISTS wallet;

CREATE TABLE wallet.balance (
  account_id uuid   NOT NULL,
  currency   text   NOT NULL CHECK (currency IN ('GEM','COIN')),
  amount     bigint NOT NULL DEFAULT 0,
  version    bigint NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id, currency),
  -- BR-WAL-03: Coin không bao giờ âm. Gem chỉ âm khi thu hồi hoàn tiền (chưa có trong phạm vi này).
  CONSTRAINT coin_non_negative CHECK (currency <> 'COIN' OR amount >= 0)
);

CREATE TABLE wallet.ledger_entry (
  id                 bigserial   PRIMARY KEY,
  account_id         uuid        NOT NULL,
  currency           text        NOT NULL CHECK (currency IN ('GEM','COIN')),
  amount             bigint      NOT NULL CHECK (amount <> 0),
  balance_after      bigint      NOT NULL,
  reason             text        NOT NULL,
  ref_type           text        NULL,
  ref_id             text        NULL,
  idempotency_key    text        NULL,
  reverses_entry_id  bigint      NULL REFERENCES wallet.ledger_entry(id),
  created_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_ledger_idem ON wallet.ledger_entry (account_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
CREATE INDEX ix_ledger_account ON wallet.ledger_entry (account_id, id DESC);

-- BR-WAL-01: append-only. Điều chỉnh bằng bút toán đảo, không sửa/xóa.
CREATE FUNCTION wallet.forbid_ledger_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'wallet.ledger_entry is append-only' USING ERRCODE = '55000'; END $$;
CREATE TRIGGER ledger_no_update BEFORE UPDATE OR DELETE ON wallet.ledger_entry FOR EACH ROW EXECUTE FUNCTION wallet.forbid_ledger_mutation();
CREATE TRIGGER ledger_no_truncate BEFORE TRUNCATE ON wallet.ledger_entry FOR EACH STATEMENT EXECUTE FUNCTION wallet.forbid_ledger_mutation();

-- NFR-11: số dư phải bằng tổng bút toán. Dùng cho job đối soát hằng ngày và test.
CREATE VIEW wallet.balance_mismatch AS
SELECT b.account_id, b.currency, b.amount AS balance, COALESCE(SUM(l.amount), 0) AS ledger_sum
FROM wallet.balance b LEFT JOIN wallet.ledger_entry l ON l.account_id = b.account_id AND l.currency = b.currency
GROUP BY b.account_id, b.currency, b.amount
HAVING b.amount <> COALESCE(SUM(l.amount), 0);
