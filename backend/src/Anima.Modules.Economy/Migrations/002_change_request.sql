-- Đề xuất đổi tham số kinh tế chờ người khác duyệt (BR-ADM-02). Duyệt xong mới sinh version mới trong economy.parameter.
CREATE TABLE economy.change_request (
  id             uuid        PRIMARY KEY,
  key            text        NOT NULL,
  value          bigint      NOT NULL CHECK (value >= 0),
  effective_from timestamptz NOT NULL,
  proposed_by    text        NOT NULL,
  proposed_at    timestamptz NOT NULL DEFAULT now(),
  note           text        NULL,
  status         text        NOT NULL CHECK (status IN ('pending','approved','rejected')),
  decided_by     text        NULL,
  decided_at     timestamptz NULL,
  CONSTRAINT decided_by_other CHECK (status = 'pending' OR (decided_by IS NOT NULL AND decided_by <> proposed_by))
);
CREATE INDEX ix_change_request_status ON economy.change_request (status, proposed_at DESC);
