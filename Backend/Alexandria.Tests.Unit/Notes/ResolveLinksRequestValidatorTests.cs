using Alexandria.Api.Features.Links.ResolveLinks;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using AwesomeAssertions;
using FluentValidation.Results;

namespace Alexandria.Tests.Unit.Notes;

public class ResolveLinksRequestValidatorTests
{
    private static ValidationResult Validate(params LinkTargetRef[] targets) =>
        new ResolveLinksRequestValidator().Validate(new ResolveLinksRequest { Targets = [.. targets] });

    private static LinkTargetRef File() => new(LinkTargetKind.File, Guid.NewGuid());

    [Fact]
    public void Validate_ordinaryTargets_isValid()
    {
        Validate(File(), new LinkTargetRef(LinkTargetKind.Playlist, Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_noTargets_isRejected()
    {
        Validate().IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_moreTargetsThanOneBatch_isRejected()
    {
        var targets = Enumerable.Range(0, ResolveLinksRequestValidator.MaxTargets + 1).Select(_ => File()).ToArray();

        Validate(targets).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_exactlyOneBatch_isValid()
    {
        var targets = Enumerable.Range(0, ResolveLinksRequestValidator.MaxTargets).Select(_ => File()).ToArray();

        Validate(targets).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_unknownKind_isRejected()
    {
        Validate(new LinkTargetRef((LinkTargetKind)99, Guid.NewGuid())).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_emptyId_isRejected()
    {
        Validate(new LinkTargetRef(LinkTargetKind.Tag, Guid.Empty)).IsValid.Should().BeFalse();
    }
}
