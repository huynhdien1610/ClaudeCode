-- Ngừng phát hành thẻ mà không xóa (US-10.2, SC-ADM-14). Thẻ đã phát hành vẫn nguyên; chỉ không còn được cấp mới.
ALTER TABLE catalog.card_definition ADD COLUMN discontinued boolean NOT NULL DEFAULT false;

-- Version tỷ lệ chỉ sửa được khi còn là bản nháp; đã duyệt/đang hiệu lực/đã thay thế thì khóa (BR-ADM-03, SC-ADM-09).
CREATE OR REPLACE FUNCTION catalog.guard_active_odds() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD.status <> 'draft' AND NEW.entries IS DISTINCT FROM OLD.entries THEN
    RAISE EXCEPTION 'Odds version is locked once approved (BR-ADM-03)' USING ERRCODE = '55000';
  END IF;
  RETURN NEW;
END $$;
