using Alexandria.Data.Models;
using Alexandria.Repositories;
using Alexandria.Repositories.Projections;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Repositories;

public class EffectivePolicyPickTests
{
    private static DirectoryPolicy PolicyOn(Guid directoryId) => new()
    {
        Id = Guid.NewGuid(),
        DirectoryId = directoryId,
        InheritedByChildren = true,
    };

    [Fact]
    public void PickNearest_severalInheritableAncestors_returnsTheClosest()
    {
        var album = Guid.NewGuid();
        var artist = Guid.NewGuid();
        var music = Guid.NewGuid();
        var ancestors = new List<DirectoryAncestorRow> { new(album, 1), new(artist, 2), new(music, 3) };

        var musicPolicy = PolicyOn(music);
        var artistPolicy = PolicyOn(artist);

        var picked = DirectoryPolicyRepository.PickNearest(ancestors, [musicPolicy, artistPolicy]);

        picked!.Id.Should().Be(artistPolicy.Id);
    }

    [Fact]
    public void PickNearest_onlyDistantAncestorHasPolicy_returnsIt()
    {
        var album = Guid.NewGuid();
        var music = Guid.NewGuid();
        var ancestors = new List<DirectoryAncestorRow> { new(album, 1), new(Guid.NewGuid(), 2), new(music, 3) };

        var musicPolicy = PolicyOn(music);

        DirectoryPolicyRepository.PickNearest(ancestors, [musicPolicy])!.Id.Should().Be(musicPolicy.Id);
    }

    [Fact]
    public void PickNearest_noPolicies_returnsNull()
    {
        var ancestors = new List<DirectoryAncestorRow> { new(Guid.NewGuid(), 1) };

        DirectoryPolicyRepository.PickNearest(ancestors, []).Should().BeNull();
    }
}
