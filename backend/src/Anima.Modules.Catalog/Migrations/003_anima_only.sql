-- BR-CARD-01 (v1.1): thẻ sưu tầm chỉ còn Anima (Ký ức phong ấn là vật phẩm kho, Echo là đơn vị trong trận).
-- Dữ liệu cũ của môi trường dev: ngừng phát hành các định nghĩa Echo/Seal; bản đã phát (nếu có) vẫn nằm trong bộ sưu tập.
UPDATE catalog.card_definition SET discontinued = true WHERE card_type <> 'anima';
