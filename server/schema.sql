CREATE TABLE IF NOT EXISTS challenges (
  id TEXT PRIMARY KEY, email TEXT NOT NULL, code_hash TEXT NOT NULL,
  expires INTEGER NOT NULL, attempts INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS challenges_expiry ON challenges(expires);
CREATE TABLE IF NOT EXISTS accounts (
  id TEXT PRIMARY KEY, email TEXT NOT NULL UNIQUE, enabled INTEGER NOT NULL DEFAULT 1,
  unsubscribe_hash TEXT NOT NULL UNIQUE, unsubscribe_token TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS sessions (
  token_hash TEXT PRIMARY KEY, account_id TEXT NOT NULL, expires INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS sessions_expiry ON sessions(expires);
CREATE TABLE IF NOT EXISTS limits (
  id TEXT PRIMARY KEY, count INTEGER NOT NULL, expires INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS deliveries (
  id TEXT PRIMARY KEY, account_id TEXT NOT NULL, status TEXT NOT NULL, created INTEGER NOT NULL
);
