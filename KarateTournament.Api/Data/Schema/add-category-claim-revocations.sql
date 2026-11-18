-- Apply to an existing database before running an API version with claim recovery.
BEGIN;
CREATE TABLE IF NOT EXISTS "RevokedCategoryClaims" (
    "CategoryId" integer NOT NULL,
    "ClaimReference" character varying(64) NOT NULL,
    CONSTRAINT "PK_RevokedCategoryClaims" PRIMARY KEY ("CategoryId", "ClaimReference"),
    CONSTRAINT "FK_RevokedCategoryClaims_Categories_CategoryId"
        FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE
);
COMMIT;
