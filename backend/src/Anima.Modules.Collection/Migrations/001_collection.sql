CREATE SCHEMA IF NOT EXISTS collection;
CREATE SEQUENCE collection.card_serial_seq START 1;

CREATE TABLE collection.card_instance (
  id                 uuid        PRIMARY KEY,
  serial             bigint      NOT NULL UNIQUE DEFAULT nextval('collection.card_serial_seq'),   -- duy nhất toàn hệ thống (BR-SUP-02)
  card_definition_id int         NOT NULL,
  edition_no         int         NOT NULL CHECK (edition_no >= 1),                                -- "#37/100"
  owner_id           uuid        NOT NULL,
  state              text        NOT NULL CHECK (state IN ('Owned','Listed','InAuction','Locked','Withdrawing','InWallet','InMatch','Burned')),
  soulbound          boolean     NOT NULL DEFAULT false,
  origin_type        text        NOT NULL,        -- PACK | FORGE | WELCOME | ...
  origin_id          text        NULL,
  created_at         timestamptz NOT NULL DEFAULT now(),
  UNIQUE (card_definition_id, edition_no)
);
CREATE INDEX ix_card_owner ON collection.card_instance (owner_id, state);

-- Burned là trạng thái cuối, không đảo ngược (BR-FRG-01, SAD 8.2).
CREATE FUNCTION collection.guard_burned() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD.state = 'Burned' AND NEW.state <> 'Burned' THEN RAISE EXCEPTION 'Burned card cannot change state' USING ERRCODE = '55000'; END IF;
  IF NEW.serial <> OLD.serial OR NEW.card_definition_id <> OLD.card_definition_id OR NEW.edition_no <> OLD.edition_no THEN RAISE EXCEPTION 'Card identity is immutable' USING ERRCODE = '55000'; END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER card_instance_guard BEFORE UPDATE ON collection.card_instance FOR EACH ROW EXECUTE FUNCTION collection.guard_burned();

-- Thẻ từng sở hữu → được đọc Story Fragment (US-05.4, Q-17).
CREATE TABLE collection.discovery (
  account_id         uuid        NOT NULL,
  card_definition_id int         NOT NULL,
  first_at           timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (account_id, card_definition_id)
);
