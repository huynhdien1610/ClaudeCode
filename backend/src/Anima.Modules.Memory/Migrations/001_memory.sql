CREATE SCHEMA IF NOT EXISTS memory;

-- Kho Ký ức phong ấn (BR-CARD-05): vật phẩm tiêu hao, không phải Card Instance. Khóa theo id thẻ Anima gốc.
CREATE TABLE memory.stock (
  account_id    uuid NOT NULL,
  source_def_id int  NOT NULL,
  qty           int  NOT NULL CHECK (qty >= 0),
  PRIMARY KEY (account_id, source_def_id)
);

-- Mua bằng Gem tối đa 1 lần mỗi tuần mỗi tài khoản (BR-CARD-08); week_start là thứ Hai của tuần (UTC).
CREATE TABLE memory.weekly_purchase (
  account_id  uuid NOT NULL,
  week_start  date NOT NULL,
  source_def_id int NOT NULL,
  created_at  timestamptz NOT NULL,
  PRIMARY KEY (account_id, week_start)
);
