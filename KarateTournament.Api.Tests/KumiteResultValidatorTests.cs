using KarateTournament.Api.Contracts;
using Xunit;

namespace KarateTournament.Api.Tests;

public class KumiteResultValidatorTests
{
    [Fact]
    public void AcceptsByesAndEmptySubtrees()
    {
        KumiteMatchResultRequest[] matches =
        [new(0, 1, null, 1), new(1, 1, null, 1), new(2, null, null, null)];
        Assert.Null(KumiteResultValidator.Validate(new(Guid.NewGuid(), matches, [1])));
    }

    [Fact]
    public void RejectsInvalidTimeSelfMatchAndByeScores()
    {
        foreach (var match in new[]
        {
            new KumiteMatchResultRequest(0, 1, 2, 1, TimeRemaining: double.NaN),
            new KumiteMatchResultRequest(0, 1, 2, 1, TimeRemaining: double.PositiveInfinity),
            new KumiteMatchResultRequest(0, 1, 1, 1),
            new KumiteMatchResultRequest(0, 1, null, 1, AkaScore: 1)
        })
            Assert.NotNull(KumiteResultValidator.Validate(new(Guid.NewGuid(), [match],
                match.ShiroParticipantId is null || match.AkaParticipantId == match.ShiroParticipantId ? [1] : [1, 2])));
    }
}
