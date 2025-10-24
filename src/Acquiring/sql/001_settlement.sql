CREATE TABLE IF NOT EXISTS settlements (
  entry_id uuid PRIMARY KEY,
  merchant text NOT NULL,
  settled_at timestamptz NOT NULL
);
