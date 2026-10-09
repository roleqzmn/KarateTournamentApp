using KarateTournament.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarateTournament.Api.Data;

public sealed class TournamentDbContext : DbContext
{
    public TournamentDbContext(DbContextOptions<TournamentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<ParticipantResult> ParticipantResults => Set<ParticipantResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).IsRequired();
            entity.HasMany(category => category.ParticipantLinks)
                .WithOne(link => link.Category)
                .HasForeignKey(link => link.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(category => category.AllowedBeltLinks)
                .WithOne(link => link.Category)
                .HasForeignKey(link => link.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(category => category.BracketMatches)
                .WithOne(match => match.Category)
                .HasForeignKey(match => match.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(category => category.Teams)
                .WithOne(team => team.Category)
                .HasForeignKey(team => team.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(category => category.FinalResults)
                .WithOne(result => result.Category)
                .HasForeignKey(result => result.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(participant => participant.Id);
            entity.Property(participant => participant.FirstName).IsRequired();
            entity.Property(participant => participant.LastName).IsRequired();
        });

        modelBuilder.Entity<CategoryParticipant>(entity =>
        {
            entity.HasKey(link => new { link.CategoryId, link.ParticipantId });
            entity.HasIndex(link => new { link.CategoryId, link.SortOrder }).IsUnique();
            entity.HasOne(link => link.Participant)
                .WithMany(participant => participant.CategoryMemberships)
                .HasForeignKey(link => link.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryAllowedBelt>(entity =>
        {
            entity.HasKey(link => new { link.CategoryId, link.Belt });
            entity.HasIndex(link => new { link.CategoryId, link.SortOrder }).IsUnique();
        });

        modelBuilder.Entity<ParticipantCategoryType>(entity =>
        {
            entity.HasKey(link => new { link.ParticipantId, link.CategoryType });
            entity.HasOne(link => link.Participant)
                .WithMany(participant => participant.CategoryTypes)
                .HasForeignKey(link => link.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(match => match.Id);
            entity.HasIndex(match => new { match.CategoryId, match.BracketPosition }).IsUnique();
            entity.HasOne(match => match.AkaParticipant)
                .WithMany()
                .HasForeignKey(match => match.AkaParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(match => match.ShiroParticipant)
                .WithMany()
                .HasForeignKey(match => match.ShiroParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(match => match.WinnerParticipant)
                .WithMany()
                .HasForeignKey(match => match.WinnerParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(team => team.Id);
            entity.Property(team => team.Name).IsRequired();
            entity.HasIndex(team => new { team.CategoryId, team.SortOrder }).IsUnique();
            entity.HasMany(team => team.MemberLinks)
                .WithOne(link => link.Team)
                .HasForeignKey(link => link.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(link => new { link.TeamId, link.ParticipantId });
            entity.HasIndex(link => new { link.TeamId, link.SortOrder }).IsUnique();
            entity.HasOne(link => link.Participant)
                .WithMany(participant => participant.TeamMemberships)
                .HasForeignKey(link => link.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParticipantResult>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.HasIndex(result => new { result.CategoryId, result.ParticipantId }).IsUnique();
            entity.HasIndex(result => new { result.CategoryId, result.Rank }).IsUnique();
            entity.Property(result => result.Score).HasPrecision(7, 2);
            entity.HasOne(result => result.Participant)
                .WithMany()
                .HasForeignKey(result => result.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(result => result.JudgeScores)
                .WithOne(score => score.ParticipantResult)
                .HasForeignKey(score => score.ParticipantResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantJudgeScore>(entity =>
        {
            entity.HasKey(score => score.Id);
            entity.HasIndex(score => new { score.ParticipantResultId, score.Ordinal }).IsUnique();
            entity.Property(score => score.Score).HasPrecision(4, 2);
        });
    }
}
