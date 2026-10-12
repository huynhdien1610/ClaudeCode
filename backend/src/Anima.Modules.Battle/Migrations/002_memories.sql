-- Ký ức phong ấn mang vào trận (BR-CARD-05), chốt lúc bắt đầu để phát lại đúng.
ALTER TABLE battle.match ADD COLUMN memories jsonb NOT NULL DEFAULT '[]';
