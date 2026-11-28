using Alexandria.Common.Policies;
using Alexandria.Data.Models;
using AwesomeAssertions;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Unit.Policies;

public class PolicyScopeTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Child = Guid.NewGuid();
    private static readonly Guid Grandchild = Guid.NewGuid();
    private static readonly Guid Sibling = Guid.NewGuid();

    private static Directory Folder(Guid id, Guid parentId) => new()
    {
        Id = id,
        Name = id.ToString(),
        ParentId = parentId,
    };

    private static readonly Directory[] Tree =
    [
        Folder(Child, Root),
        Folder(Grandchild, Child),
        Folder(Sibling, Root),
    ];

    private static DirectoryPolicy PolicyOn(Guid directoryId, bool inherited, bool withRule = true) => new()
    {
        Id = Guid.NewGuid(),
        DirectoryId = directoryId,
        InheritedByChildren = inherited,
        Rules = withRule ? [new PolicyRule { Id = Guid.NewGuid() }] : [],
    };

    [Fact]
    public void GovernedDirectoryIds_noSubfolderPolicies_coversTheWholeTree()
    {
        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, []);

        governed.Should().BeEquivalentTo([Root, Child, Grandchild, Sibling]);
    }

    [Fact]
    public void GovernedDirectoryIds_noSubfolders_coversOnlyTheRoot()
    {
        PolicyScope.GovernedDirectoryIds(Root, [], []).Should().Equal(Root);
    }

    [Fact]
    public void GovernedDirectoryIds_subfolderWithInheritedPolicy_leavesItsWholeSubtreeToThatPolicy()
    {
        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, [PolicyOn(Child, inherited: true)]);

        governed.Should().BeEquivalentTo([Root, Sibling]);
    }

    [Fact]
    public void GovernedDirectoryIds_subfolderWithItsOwnOnlyPolicy_stillCoversItsChildren()
    {
        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, [PolicyOn(Child, inherited: false)]);

        governed.Should().BeEquivalentTo([Root, Grandchild, Sibling]);
    }

    [Fact]
    public void GovernedDirectoryIds_deletedSubfolderPolicy_isIgnored()
    {
        var deleted = PolicyOn(Child, inherited: true);
        deleted.DeletedAt = DateTime.UtcNow;

        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, [deleted]);

        governed.Should().BeEquivalentTo([Root, Child, Grandchild, Sibling]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GovernedDirectoryIds_subfolderPolicyWithoutRules_fallsBackToUs(bool inherited)
    {
        var empty = PolicyOn(Child, inherited, withRule: false);

        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, [empty]);

        governed.Should().BeEquivalentTo([Root, Child, Grandchild, Sibling]);
    }

    [Fact]
    public void GovernedDirectoryIds_subfolderPolicyWithOnlyDeletedRules_fallsBackToUs()
    {
        var emptied = PolicyOn(Child, inherited: true);
        emptied.Rules.Single().DeletedAt = DateTime.UtcNow;

        var governed = PolicyScope.GovernedDirectoryIds(Root, Tree, [emptied]);

        governed.Should().BeEquivalentTo([Root, Child, Grandchild, Sibling]);
    }
}
