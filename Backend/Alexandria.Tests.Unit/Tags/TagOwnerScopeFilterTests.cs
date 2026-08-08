using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Tags;
using Alexandria.Repositories;
using Alexandria.Repositories.Projections;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Tags;

public class TagOwnerScopeFilterTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void User_scope_matches_only_owning_user()
    {
        var filter = TagRepository.BuildOwnerScopeFilter(UserId, OwnerScope.User, SystemConfig.SystemId).Compile();

        filter(Tag(UserId)).Should().BeTrue();
        filter(Tag(SystemConfig.SystemId)).Should().BeFalse();
        filter(Tag(OtherUserId)).Should().BeFalse();
    }

    [Fact]
    public void System_scope_matches_only_system_account()
    {
        var filter = TagRepository.BuildOwnerScopeFilter(UserId, OwnerScope.System, SystemConfig.SystemId).Compile();

        filter(Tag(SystemConfig.SystemId)).Should().BeTrue();
        filter(Tag(UserId)).Should().BeFalse();
        filter(Tag(OtherUserId)).Should().BeFalse();
    }

    [Fact]
    public void All_scope_matches_user_and_system_but_never_other_users()
    {
        var filter = TagRepository.BuildOwnerScopeFilter(UserId, OwnerScope.All, SystemConfig.SystemId).Compile();

        filter(Tag(UserId)).Should().BeTrue();
        filter(Tag(SystemConfig.SystemId)).Should().BeTrue();
        filter(Tag(OtherUserId)).Should().BeFalse();
    }

    [Fact]
    public void Default_scope_is_user_only()
    {
        var filter = TagRepository.BuildOwnerScopeFilter(UserId, default, SystemConfig.SystemId).Compile();

        filter(Tag(UserId)).Should().BeTrue();
        filter(Tag(SystemConfig.SystemId)).Should().BeFalse();
    }

    [Fact]
    public void Null_user_id_disables_owner_filtering()
    {
        var filter = TagRepository.BuildOwnerScopeFilter(null, OwnerScope.All, SystemConfig.SystemId).Compile();

        filter(Tag(OtherUserId)).Should().BeTrue();
        filter(Tag(SystemConfig.SystemId)).Should().BeTrue();
    }

    private static Tag Tag(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        Name = "tag",
        Icon = "tag",
        Color = "#000000",
        OwnerId = ownerId
    };
}

public class TagProjectionTests
{
    private static readonly Guid ParentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SystemTagId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid UserTagId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public void System_tag_maps_is_system_and_parent()
    {
        var parent = Tag(ParentId, "Rock", SystemConfig.SystemId, TagFacet.Genre, null);
        var child = Tag(SystemTagId, "Nu Metal", SystemConfig.SystemId, TagFacet.Genre, parent);

        var dto = TagProjections.ToTagDto(SystemConfig.SystemId).Compile()(child);

        dto.IsSystem.Should().BeTrue();
        dto.Facet.Should().Be(TagFacet.Genre);
        dto.ParentId.Should().Be(ParentId);
        dto.ParentName.Should().Be("Rock");
        dto.UserId.Should().Be(SystemConfig.SystemId);
    }

    [Fact]
    public void User_tag_maps_no_system_flags()
    {
        var tag = Tag(UserTagId, "work", ownerId: Guid.NewGuid(), facet: null, parent: null);

        var dto = TagProjections.ToTagDto(SystemConfig.SystemId).Compile()(tag);

        dto.IsSystem.Should().BeFalse();
        dto.Facet.Should().BeNull();
        dto.ParentId.Should().BeNull();
        dto.ParentName.Should().BeNull();
        dto.UserId.Should().Be(tag.OwnerId);
    }

    [Fact]
    public void Mood_tag_maps_facet_without_parent()
    {
        var tag = Tag(UserTagId, "aggressive", SystemConfig.SystemId, TagFacet.Mood, null);

        var dto = TagProjections.ToTagDto(SystemConfig.SystemId).Compile()(tag);

        dto.IsSystem.Should().BeTrue();
        dto.Facet.Should().Be(TagFacet.Mood);
        dto.ParentId.Should().BeNull();
        dto.ParentName.Should().BeNull();
    }

    [Fact]
    public void System_id_is_compared_by_value_not_magic_string()
    {
        var userTag = Tag(UserTagId, "mine", Guid.NewGuid(), null, null);

        var dto = TagProjections.ToTagDto(SystemConfig.SystemId).Compile()(userTag);

        dto.IsSystem.Should().BeFalse();
    }

    private static Tag Tag(Guid id, string name, Guid ownerId, TagFacet? facet, Tag? parent) => new()
    {
        Id = id,
        Name = name,
        Icon = "tag",
        Color = "#000000",
        OwnerId = ownerId,
        Facet = facet,
        ParentId = parent?.Id,
        Parent = parent
    };
}