CREATE SCHEMA IF NOT EXISTS economy;

-- Tham số kinh tế có version và thời điểm hiệu lực (BR-ECO-03). Không sửa dòng cũ; thêm version mới.
CREATE TABLE economy.parameter (
  key            text        NOT NULL,
  version        int         NOT NULL,
  value          bigint      NOT NULL,
  effective_from timestamptz NOT NULL,
  created_by     text        NOT NULL,
  approved_by    text        NOT NULL,
  note           text        NULL,
  PRIMARY KEY (key, version),
  CONSTRAINT maker_checker CHECK (created_by <> approved_by)   -- BR-ADM-02
);

INSERT INTO economy.parameter(key, version, value, effective_from, created_by, approved_by, note) VALUES
  ('gem_to_coin_rate',              1,   9, '2026-01-01', 'system', 'product-owner', '1 Gem -> 9 Coin (BR-WAL-05, đề xuất)'),
  ('coin_to_gem_rate',              1,  11, '2026-01-01', 'system', 'product-owner', '11 Coin -> 1 Gem (BR-WAL-05, đề xuất)'),
  ('coin_to_gem_daily_cap_gem',     1, 1000,'2026-01-01', 'system', 'product-owner', 'BR-WAL-06, Q-38'),
  ('forge_fee_coin',                1,  50, '2026-01-01', 'system', 'product-owner', 'BR-FRG-02'),
  ('forge_fee_gem',                 1,   5, '2026-01-01', 'system', 'product-owner', 'BR-FRG-02'),
  ('forge_daily_limit_unverified',  1,   5, '2026-01-01', 'system', 'product-owner', 'BR-FRG-07'),
  ('forge_daily_limit_verified',    1, 100, '2026-01-01', 'system', 'product-owner', 'BR-FRG-07'),
  ('first_login_coin',              1, 100, '2026-01-01', 'system', 'product-owner', 'Thưởng đăng nhập lần đầu (BRD 6.3)'),
  ('pity_guarantee_after',          1,  49, '2026-01-01', 'system', 'product-owner', 'BR-PACK-05, Q-09 chờ xác nhận');
