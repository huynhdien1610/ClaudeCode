CREATE SCHEMA IF NOT EXISTS forge;

CREATE TABLE forge.sealed_card (
  id                      uuid        PRIMARY KEY,
  account_id              uuid        NOT NULL,
  forge_odds_version_id   int         NOT NULL,                 -- snapshot tỷ lệ rèn lúc rèn (BR-FRG-03)
  status                  text        NOT NULL CHECK (status IN ('Sealed','Revealed')),
  result_card_instance_id uuid        NULL,
  seed_id                 uuid        NULL,
  nonce                   int         NULL,
  created_at              timestamptz NOT NULL DEFAULT now(),
  revealed_at             timestamptz NULL
);
CREATE INDEX ix_sealed_account ON forge.sealed_card (account_id, status);

CREATE TABLE forge.forge_record (
  id              uuid        PRIMARY KEY,
  account_id      uuid        NOT NULL,
  input_card_ids  uuid[]      NOT NULL CHECK (cardinality(input_card_ids) = 2),
  fee_currency    text        NOT NULL CHECK (fee_currency IN ('GEM','COIN')),
  fee_amount      bigint      NOT NULL CHECK (fee_amount > 0),
  sealed_card_id  uuid        NOT NULL REFERENCES forge.sealed_card(id),
  created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_forge_record_account_day ON forge.forge_record (account_id, created_at);
