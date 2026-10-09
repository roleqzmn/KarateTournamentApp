using KarateTournament.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("TournamentDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:TournamentDatabase using user secrets or the ConnectionStrings__TournamentDatabase environment variable.");
}

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health/database", async (
    TournamentDbContext dbContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!await dbContext.Database.CanConnectAsync(cancellationToken))
    {
        logger.LogWarning("PostgreSQL connectivity check failed.");
        return Results.Problem(
            title: "Database unavailable",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "healthy" });
})
.WithName("GetDatabaseHealth");

app.Run();
