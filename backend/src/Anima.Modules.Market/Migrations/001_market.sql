CREATE SCHEMA IF NOT EXISTS market;

-- Listing giá cố định của chợ P2P (BR-MKT). Thẻ đang niêm yết có collection.card_instance.state = 'Listed' (BR-MKT-02).
CREATE TABLE market.listing (
  id                 uuid        PRIMARY KEY,
  seller_id          uuid        NOT NULL,
  card_instance_id   uuid        NOT NULL,
  card_definition_id int         NOT NULL,
  rarity             text        NOT NULL,
  currency           text        NOT NULL CHECK (currency IN ('GEM','COIN')),    -- một loại tiền mỗi listing (BR-MKT-06)
  price              bigint      NOT NULL CHECK (price > 0),
  fee                bigint      NULL,                                           -- chốt lúc bán: 5% làm tròn lên (BR-MKT-05)
  status             text        NOT NULL CHECK (status IN ('Active','Sold','Cancelled','Expired','Removed')),
  buyer_id           uuid        NULL,
  created_at         timestamptz NOT NULL,
  expires_at         timestamptz NOT NULL,
  closed_at          timestamptz NULL
);
-- Một thẻ chỉ có tối đa một listing đang mở.
CREATE UNIQUE INDEX ux_listing_one_active_per_card ON market.listing (card_instance_id) WHERE status = 'Active';
CREATE INDEX ix_listing_browse ON market.listing (status, rarity, price);
CREATE INDEX ix_listing_seller ON market.listing (seller_id, created_at DESC);
CREATE INDEX ix_listing_expiry ON market.listing (expires_at) WHERE status = 'Active';
