CREATE SCHEMA IF NOT EXISTS gacha;

CREATE TABLE gacha.pack_instance (
  id                       uuid        PRIMARY KEY,
  account_id               uuid        NOT NULL,
  pack_code                text        NOT NULL,
  kind                     text        NOT NULL CHECK (kind IN ('standard','welcome')),
  odds_version_id          int         NULL,                  -- snapshot tỷ lệ tại thời điểm mua (BR-PACK-04); welcome không có
  status                   text        NOT NULL CHECK (status IN ('Unopened','Opened','Revoked')),
  soulbound                boolean     NOT NULL DEFAULT false,
  purchase_ledger_entry_id bigint      NULL,
  created_at               timestamptz NOT NULL DEFAULT now(),
  opened_at                timestamptz NULL
);
CREATE INDEX ix_pack_instance_account ON gacha.pack_instance (account_id, status, created_at DESC);

-- PK(pack_instance_id) chống mở hai lần (SC-PACK-13). Bản ghi này là dữ liệu cho công cụ kiểm chứng công khai.
CREATE TABLE gacha.pack_opening (
  pack_instance_id uuid        PRIMARY KEY REFERENCES gacha.pack_instance(id),
  seed_id          uuid        NOT NULL,
  seed_hash        text        NOT NULL,
  client_seed      text        NOT NULL,
  nonce            int         NOT NULL,
  results          jsonb       NOT NULL,
  pity_before      int         NOT NULL,
  pity_after       int         NOT NULL,
  pity_triggered   boolean     NOT NULL,
  opened_at        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE gacha.pity_counter (
  account_id uuid NOT NULL,
  pack_code  text NOT NULL,
  count      int  NOT NULL DEFAULT 0 CHECK (count >= 0),
  PRIMARY KEY (account_id, pack_code)
);
