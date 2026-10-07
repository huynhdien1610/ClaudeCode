CREATE SCHEMA IF NOT EXISTS admin;

CREATE TABLE admin.admin_user (
  id            uuid        PRIMARY KEY,
  email         text        NOT NULL,
  email_norm    text        NOT NULL UNIQUE,
  password_hash text        NOT NULL,
  role          text        NOT NULL CHECK (role IN ('cs_agent','content_manager','economy_manager','fraud_analyst','finance_viewer','super_admin')),
  active        boolean     NOT NULL DEFAULT true,
  failed_logins int         NOT NULL DEFAULT 0,
  locked_until  timestamptz NULL,
  created_by    uuid        NULL,
  created_at    timestamptz NOT NULL DEFAULT now()
);

-- BR-ADM-01: audit log chỉ thêm, không ai sửa hay xóa được (SC-ADM-05).
CREATE TABLE admin.audit_log (
  id          bigserial   PRIMARY KEY,
  at          timestamptz NOT NULL DEFAULT now(),
  actor_id    uuid        NULL,
  actor_email text        NOT NULL,
  actor_role  text        NOT NULL,
  action      text        NOT NULL,
  target_type text        NULL,
  target_id   text        NULL,
  before      jsonb       NULL,
  after       jsonb       NULL,
  reason      text        NULL,
  outcome     text        NOT NULL DEFAULT 'OK' CHECK (outcome IN ('OK','DENIED'))
);
CREATE INDEX ix_audit_at ON admin.audit_log (at DESC, id DESC);
CREATE INDEX ix_audit_target ON admin.audit_log (target_type, target_id);
CREATE FUNCTION admin.forbid_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'admin.audit_log is append-only' USING ERRCODE = '55000'; END $$;
CREATE TRIGGER audit_no_update BEFORE UPDATE OR DELETE ON admin.audit_log FOR EACH ROW EXECUTE FUNCTION admin.forbid_audit_mutation();
CREATE TRIGGER audit_no_truncate BEFORE TRUNCATE ON admin.audit_log FOR EACH STATEMENT EXECUTE FUNCTION admin.forbid_audit_mutation();

-- Bồi thường Coin (BR-ADM-04): luôn có ticket, người tạo khác người duyệt.
CREATE TABLE admin.compensation (
  id              uuid        PRIMARY KEY,
  account_id      uuid        NOT NULL,
  coin            bigint      NOT NULL CHECK (coin > 0),
  ticket          text        NOT NULL CHECK (length(trim(ticket)) > 0),
  reason          text        NULL,
  status          text        NOT NULL CHECK (status IN ('pending','approved','rejected')),
  created_by      text        NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  decided_by      text        NULL,
  decided_at      timestamptz NULL,
  ledger_entry_id bigint      NULL,
  CONSTRAINT decided_by_other CHECK (status = 'pending' OR (decided_by IS NOT NULL AND decided_by <> created_by))
);
CREATE INDEX ix_compensation_status ON admin.compensation (status, created_at DESC);
