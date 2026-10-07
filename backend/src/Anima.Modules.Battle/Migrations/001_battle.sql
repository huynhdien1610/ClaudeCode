CREATE SCHEMA IF NOT EXISTS battle;

-- Trận đấu lưu đủ để phát lại (BR-BTL-10): seed + bộ bài hai bên (chỉ số đã chốt) + toàn bộ hành động theo thứ tự.
CREATE TABLE battle.match (
  id           uuid        PRIMARY KEY,
  account_id   uuid        NOT NULL,
  mode         text        NOT NULL CHECK (mode IN ('practice')),
  deck_id      uuid        NULL,
  status       text        NOT NULL CHECK (status IN ('InProgress','Finished')),
  seed         bytea       NOT NULL,
  player_cards jsonb       NOT NULL,
  bot_cards    jsonb       NOT NULL,
  actions      jsonb       NOT NULL DEFAULT '[]',
  winner       int         NULL CHECK (winner IN (0,1)),
  reason       text        NULL,
  created_at   timestamptz NOT NULL,
  finished_at  timestamptz NULL
);
CREATE INDEX ix_match_account ON battle.match (account_id, created_at DESC);
-- Mỗi tài khoản chỉ có một trận luyện tập đang diễn ra.
CREATE UNIQUE INDEX ux_match_one_active ON battle.match (account_id) WHERE status = 'InProgress';
