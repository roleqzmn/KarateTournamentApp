using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Data;

public sealed partial class TournamentDbContext
{
    internal DbSet<ScheduleState> ScheduleStates => Set<ScheduleState>();
    internal DbSet<ScheduleMat> ScheduleMats => Set<ScheduleMat>();
    internal DbSet<ScheduleSlot> ScheduleSlots => Set<ScheduleSlot>();

    internal Task EnsureScheduleSchemaAsync() => Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ScheduleStates" (
            "Id" integer PRIMARY KEY,
            "Version" bigint NOT NULL
        );
        INSERT INTO "ScheduleStates" ("Id", "Version") VALUES (1, 0) ON CONFLICT ("Id") DO NOTHING;
        CREATE TABLE IF NOT EXISTS "ScheduleMats" (
            "Number" integer PRIMARY KEY,
            "Revision" uuid NOT NULL,
            "ClaimToken" uuid UNIQUE,
            "ClaimedBy" character varying(100)
        );
        CREATE TABLE IF NOT EXISTS "ScheduleSlots" (
            "CategoryId" integer PRIMARY KEY REFERENCES "Categories" ("Id"),
            "MatNumber" integer NOT NULL REFERENCES "ScheduleMats" ("Number"),
            "Position" integer NOT NULL,
            "StartsAt" timestamp without time zone,
            "EndsAt" timestamp without time zone,
            UNIQUE ("MatNumber", "Position")
        );
        """);

    // Schedule writes and category claims acquire this lock before reading the plan or locking a category.
    internal async Task<ScheduleState> LockScheduleAsync(CancellationToken cancellationToken)
    {
        var rows = await ScheduleStates.FromSqlRaw("""SELECT * FROM "ScheduleStates" WHERE "Id" = 1 FOR UPDATE""")
            .ToListAsync(cancellationToken);
        return rows.Single();
    }

    private static void ConfigureSchedule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduleState>().Property(state => state.Id).ValueGeneratedNever();
        modelBuilder.Entity<ScheduleMat>(entity =>
        {
            entity.HasKey(mat => mat.Number);
            entity.Property(mat => mat.Number).ValueGeneratedNever();
            entity.Property(mat => mat.ClaimedBy).HasMaxLength(100);
            entity.HasIndex(mat => mat.ClaimToken).IsUnique();
        });
        modelBuilder.Entity<ScheduleSlot>(entity =>
        {
            entity.HasKey(slot => slot.CategoryId);
            entity.HasOne(slot => slot.Category).WithOne().HasForeignKey<ScheduleSlot>(slot => slot.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(slot => slot.Mat).WithMany(mat => mat.Slots).HasForeignKey(slot => slot.MatNumber)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(slot => new { slot.MatNumber, slot.Position }).IsUnique();
            entity.Property(slot => slot.StartsAt).HasColumnType("timestamp without time zone");
            entity.Property(slot => slot.EndsAt).HasColumnType("timestamp without time zone");
        });
    }
}
