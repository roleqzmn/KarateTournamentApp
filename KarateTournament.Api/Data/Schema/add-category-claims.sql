-- Apply explicitly to an existing database before starting this API version.
-- EnsureCreated creates these columns only when creating a fresh schema.
BEGIN;
ALTER TABLE "Categories"
    ADD COLUMN IF NOT EXISTS "ClaimToken" uuid NULL,
    ADD COLUMN IF NOT EXISTS "ClaimedBy" character varying(100) NULL;
COMMIT;
