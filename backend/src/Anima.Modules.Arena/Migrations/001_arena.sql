CREATE SCHEMA IF NOT EXISTS arena;

-- Điểm kỹ năng Glicko-2 (BR-PVP-06).
CREATE TABLE arena.rating (
  account_id uuid PRIMARY KEY,
  rating     double precision NOT NULL DEFAULT 1500,
  rd         double precision NOT NULL DEFAULT 350,
  vol        double precision NOT NULL DEFAULT 0.06,
  games      int  NOT NULL DEFAULT 0,
  wins       int  NOT NULL DEFAULT 0,
  updated_at timestamptz NOT NULL
);
CREATE INDEX ix_rating_rank ON arena.rating (rating DESC) WHERE games > 0;

-- Arena Point (BR-PVP-03): chỉ kiếm được qua thi đấu, không mua, không chuyển, không quy đổi.
CREATE TABLE arena.ap (
  account_id uuid PRIMARY KEY,
  balance    bigint NOT NULL DEFAULT 0 CHECK (balance >= 0)
);

-- Hàng đợi ghép trận xếp hạng. Mỗi tài khoản một dòng.
CREATE TABLE arena.queue (
  account_id uuid PRIMARY KEY,
  deck_id    uuid NOT NULL,
  rating     double precision NOT NULL,
  joined_at  timestamptz NOT NULL
);

-- Trận PvP: lưu đủ để phát lại (BR-BTL-10): seed + bộ bài hai bên + toàn bộ hành động, kể cả hành động "timeout" do máy chủ sinh.
CREATE TABLE arena.match (
  id              uuid PRIMARY KEY,
  p0              uuid NOT NULL,
  p1              uuid NOT NULL,
  mode            text NOT NULL CHECK (mode IN ('ranked')),
  status          text NOT NULL CHECK (status IN ('InProgress','Finished')),
  seed            bytea NOT NULL,
  cards0          jsonb NOT NULL,
  cards1          jsonb NOT NULL,
  actions         jsonb NOT NULL DEFAULT '[]',
  winner          int NULL CHECK (winner IN (0,1)),
  reason          text NULL,
  created_at      timestamptz NOT NULL,
  finished_at     timestamptz NULL,
  turn_started_at timestamptz NOT NULL,
  bank0_ms        int NOT NULL DEFAULT 30000,
  bank1_ms        int NOT NULL DEFAULT 30000,
  active_seen     int NOT NULL DEFAULT -1,
  turn_seen       int NOT NULL DEFAULT 0,
  rating0 double precision NOT NULL, rating1 double precision NOT NULL,
  delta0  double precision NULL,     delta1  double precision NULL,
  ap0 int NOT NULL DEFAULT 0, ap1 int NOT NULL DEFAULT 0
);
CREATE INDEX ix_arena_match_p0 ON arena.match (p0, created_at DESC);
CREATE INDEX ix_arena_match_p1 ON arena.match (p1, created_at DESC);
CREATE INDEX ix_arena_match_active ON arena.match (status) WHERE status = 'InProgress';
