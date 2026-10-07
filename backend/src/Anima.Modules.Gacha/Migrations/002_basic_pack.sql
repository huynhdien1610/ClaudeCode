-- Pack cơ bản của nhiệm vụ Tân thủ (BR-NEW-03): cấp miễn phí, gắn chặt tài khoản.
ALTER TABLE gacha.pack_instance DROP CONSTRAINT pack_instance_kind_check;
ALTER TABLE gacha.pack_instance ADD CONSTRAINT pack_instance_kind_check CHECK (kind IN ('standard','welcome','basic'));
