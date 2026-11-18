using KarateTournamentApp.Models;
using Xunit;

namespace KarateTournamentApp.Tests;

public class LocalIdGeneratorTests
{
    [Fact]
    public void ParallelGenerationProducesPositiveUniqueIds()
    {
        var ids = new int[1000];

        Parallel.For(0, ids.Length, index => ids[index] = LocalIdGenerator.Next());

        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void ReservingImportedIdAdvancesGeneratorAndOlderReservationsCannotRewindIt()
    {
        var importedId = checked(LocalIdGenerator.Next() + 1000);

        LocalIdGenerator.Reserve(importedId);
        var firstNewId = LocalIdGenerator.Next();
        LocalIdGenerator.Reserve(importedId - 1);
        var secondNewId = LocalIdGenerator.Next();

        Assert.True(firstNewId > importedId);
        Assert.True(secondNewId > firstNewId);
    }
}
