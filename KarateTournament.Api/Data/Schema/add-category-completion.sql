-- Apply after add-category-claims.sql to an existing schema; preserves existing data.
BEGIN;
ALTER TABLE "Categories"
    ADD COLUMN IF NOT EXISTS "CompletionToken" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CompletionHash" character varying(64) NULL;
COMMIT;
