CREATE SCHEMA IF NOT EXISTS deck;

CREATE TABLE deck.deck (
  id          uuid        PRIMARY KEY,
  account_id  uuid        NOT NULL,
  name        text        NOT NULL CHECK (char_length(name) BETWEEN 1 AND 40),
  is_default  boolean     NOT NULL DEFAULT false,
  created_at  timestamptz NOT NULL,
  updated_at  timestamptz NOT NULL
);
CREATE INDEX ix_deck_account ON deck.deck (account_id, created_at);
-- Mỗi tài khoản có tối đa một bộ mặc định (BR-ARN-04).
CREATE UNIQUE INDEX ux_deck_default ON deck.deck (account_id) WHERE is_default;

-- Bộ bài lưu id Card Instance (không lưu bản sao thông tin thẻ). Một Instance chỉ xuất hiện một lần trong một bộ.
CREATE TABLE deck.deck_card (
  deck_id     uuid NOT NULL REFERENCES deck.deck(id) ON DELETE CASCADE,
  instance_id uuid NOT NULL,
  PRIMARY KEY (deck_id, instance_id)
);
CREATE INDEX ix_deck_card_instance ON deck.deck_card (instance_id);
