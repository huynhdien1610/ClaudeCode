CREATE SCHEMA IF NOT EXISTS quest;

-- Bộ đếm hành động của người chơi (mở pack, xác thực SĐT...). Nhiệm vụ đọc từ đây.
CREATE TABLE quest.progress (
  account_id uuid   NOT NULL,
  kind       text   NOT NULL,
  count      int    NOT NULL DEFAULT 0 CHECK (count >= 0),
  PRIMARY KEY (account_id, kind)
);

-- Mỗi ngày Tân thủ nhận thưởng đúng một lần (PK chặn nhận trùng, kể cả khi hai yêu cầu đồng thời).
CREATE TABLE quest.claim (
  account_id   uuid        NOT NULL,
  day          int         NOT NULL CHECK (day BETWEEN 1 AND 7),
  reward_type  text        NOT NULL CHECK (reward_type IN ('BASIC_PACK','COIN')),
  reward_ref   text        NULL,
  reward_coin  bigint      NULL,
  claimed_at   timestamptz NOT NULL,
  PRIMARY KEY (account_id, day)
);
