CREATE SCHEMA IF NOT EXISTS catalog;

CREATE TABLE catalog.season (
  id        text        PRIMARY KEY,
  name      text        NOT NULL,
  status    text        NOT NULL CHECK (status IN ('draft','open','closed')),
  opened_at timestamptz NULL,
  closed_at timestamptz NULL
);

CREATE TABLE catalog.card_definition (
  id              int         PRIMARY KEY,
  code            text        NOT NULL UNIQUE,
  name            text        NOT NULL,                 -- tên riêng, giữ chữ Latin ở mọi ngôn ngữ (BR-I18N-03)
  season_id       text        NOT NULL REFERENCES catalog.season(id),
  element         text        NOT NULL CHECK (element IN ('Umbryx','Pyraxis','Aqualis','Terrakin','Ventara','Voltaris','Luminara','Nihilum')),
  rarity          text        NOT NULL CHECK (rarity IN ('common','uncommon','rare','epic','legendary','secret')),
  card_type       text        NOT NULL CHECK (card_type IN ('anima','echo','seal')),
  resonance_cost  int         NOT NULL CHECK (resonance_cost BETWEEN 1 AND 6),
  atk             int         NULL CHECK (atk BETWEEN 0 AND 3000),
  def             int         NULL CHECK (def BETWEEN 0 AND 3000),
  hp              int         NULL CHECK (hp  BETWEEN 100 AND 4000),
  skill_id        text        NULL,
  arc_id          text        NULL,
  published       boolean     NOT NULL DEFAULT false,
  CONSTRAINT anima_has_stats CHECK (card_type <> 'anima' OR (atk IS NOT NULL AND def IS NOT NULL AND hp IS NOT NULL))
);

-- BR-CARD-06: chỉ số, kỹ năng, mạch truyện bất biến sau khi phát hành (gắn với NFT).
CREATE FUNCTION catalog.guard_published_card() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD.published THEN
    IF (NEW.element, NEW.rarity, NEW.card_type, NEW.resonance_cost, NEW.atk, NEW.def, NEW.hp, NEW.skill_id, NEW.arc_id, NEW.season_id, NEW.code, NEW.name)
       IS DISTINCT FROM
       (OLD.element, OLD.rarity, OLD.card_type, OLD.resonance_cost, OLD.atk, OLD.def, OLD.hp, OLD.skill_id, OLD.arc_id, OLD.season_id, OLD.code, OLD.name)
    THEN RAISE EXCEPTION 'Published card definition is immutable (BR-CARD-06)' USING ERRCODE = '55000'; END IF;
    IF NEW.published = false THEN RAISE EXCEPTION 'Published card cannot be unpublished' USING ERRCODE = '55000'; END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER card_definition_immutable BEFORE UPDATE ON catalog.card_definition FOR EACH ROW EXECUTE FUNCTION catalog.guard_published_card();
CREATE FUNCTION catalog.forbid_delete_published() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN IF OLD.published THEN RAISE EXCEPTION 'Published card definition cannot be deleted' USING ERRCODE = '55000'; END IF; RETURN OLD; END $$;
CREATE TRIGGER card_definition_no_delete BEFORE DELETE ON catalog.card_definition FOR EACH ROW EXECUTE FUNCTION catalog.forbid_delete_published();

CREATE TABLE catalog.card_translation (
  card_definition_id int  NOT NULL REFERENCES catalog.card_definition(id),
  locale             text NOT NULL CHECK (locale IN ('vi','en','zh-Hans','zh-Hant')),
  epithet            text NULL,
  story              text NOT NULL,
  PRIMARY KEY (card_definition_id, locale)
);

-- Số lượng phát hành (BR-SUP): issued tăng bằng UPDATE ... WHERE issued < max_supply nên không bao giờ vượt.
CREATE TABLE catalog.edition (
  card_definition_id int PRIMARY KEY REFERENCES catalog.card_definition(id),
  season_id          text NOT NULL REFERENCES catalog.season(id),
  max_supply         int  NOT NULL CHECK (max_supply > 0),
  issued             int  NOT NULL DEFAULT 0,
  burned             int  NOT NULL DEFAULT 0,
  CONSTRAINT issued_within_supply CHECK (issued <= max_supply),
  CONSTRAINT burned_within_issued CHECK (burned <= issued)
);

CREATE TABLE catalog.pack_definition (
  code            text    PRIMARY KEY,
  name            text    NOT NULL,
  kind            text    NOT NULL CHECK (kind IN ('standard','welcome','forge')),
  price_coin      bigint  NULL CHECK (price_coin > 0),
  price_gem       bigint  NULL CHECK (price_gem > 0),
  cards_per_pack  int     NOT NULL CHECK (cards_per_pack BETWEEN 1 AND 10),
  on_sale         boolean NOT NULL DEFAULT false,
  off_sale_reason text    NULL,
  season_id       text    NOT NULL REFERENCES catalog.season(id)
);

-- Tỷ lệ rơi có version (BR-ADM-03): không sửa version đang active; maker-checker (BR-ADM-02).
CREATE TABLE catalog.odds_version (
  id             serial      PRIMARY KEY,
  pack_code      text        NOT NULL REFERENCES catalog.pack_definition(code),
  version        int         NOT NULL,
  status         text        NOT NULL CHECK (status IN ('draft','approved','active','retired')),
  effective_from timestamptz NOT NULL,
  created_by     text        NOT NULL,
  approved_by    text        NULL,
  entries        jsonb       NOT NULL,                  -- [{"rarity":"common","ppm":450000}, ...] tổng 1,000,000
  UNIQUE (pack_code, version),
  CONSTRAINT maker_checker CHECK (status IN ('draft') OR (approved_by IS NOT NULL AND approved_by <> created_by))
);
CREATE FUNCTION catalog.guard_active_odds() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD.status = 'active' AND NEW.entries IS DISTINCT FROM OLD.entries THEN RAISE EXCEPTION 'Active odds version is immutable (BR-ADM-03)' USING ERRCODE = '55000'; END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER odds_version_immutable BEFORE UPDATE ON catalog.odds_version FOR EACH ROW EXECUTE FUNCTION catalog.guard_active_odds();
