using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Library;
using Alexandria.Services.Storage.Library;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Unit.Library;

public class LibrarySetupServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IPolicyRuleRepository _rules = Substitute.For<IPolicyRuleRepository>();

    private readonly List<Directory> _addedDirectories = [];
    private readonly List<DirectoryPolicy> _addedPolicies = [];
    private readonly List<PolicyRule> _addedRules = [];

    public LibrarySetupServiceTests()
    {
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.Directories.Returns(_directories);
        _unitOfWork.DirectoryPolicies.Returns(_policies);
        _unitOfWork.PolicyRules.Returns(_rules);

        _users.AdvanceOnboardingAsync(_userId, OnboardingStep.Tour, OnboardingStep.Done, Arg.Any<CancellationToken>())
            .Returns(true);

        _directories.AddAsync(Arg.Do<Directory>(_addedDirectories.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Directory>());
        _policies.AddAsync(Arg.Do<DirectoryPolicy>(_addedPolicies.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<DirectoryPolicy>());
        _rules.AddAsync(Arg.Do<PolicyRule>(_addedRules.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<PolicyRule>());
    }

    private LibrarySetupService CreateSut()
    {
        return new LibrarySetupService(_unitOfWork, Substitute.For<ILogger<LibrarySetupService>>(),
            Options.Create(new ProcessingCapabilities
            {
                AdaptiveStreaming = true,
                Autotagging = true,
                AudioAnalysis = true,
            }));
    }

    private static LibraryFolderChoice Music(string name = "Music") => new()
    {
        Kind = LibraryFolderKind.Music,
        Name = name,
        AutomationEnabled = true,
        AudioRungs = [AudioRung.Kbps192, AudioRung.Kbps320],
        AutoTag = true,
    };

    private static LibraryFolderChoice Plain(LibraryFolderKind kind, string name) => new()
    {
        Kind = kind,
        Name = name,
        AutomationEnabled = true,
    };

    private void GivenExistingRootFolder(Directory directory)
    {
        _directories.FirstOrDefaultAsync(Arg.Any<Expression<Func<Directory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Expression<Func<Directory, bool>>>().Compile()(directory) ? directory : null);
    }

    [Fact]
    public async Task FinishTour_notOnTourStep_throwsAndCreatesNothing()
    {
        _users.AdvanceOnboardingAsync(_userId, OnboardingStep.Tour, OnboardingStep.Done, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => CreateSut().FinishTourAsync(_userId, [Music()], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OnboardingStepMismatchException>();

        _addedDirectories.Should().BeEmpty();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishTour_musicAndPlainFolders_createsFoldersAndOneInheritedPolicy()
    {
        await CreateSut().FinishTourAsync(_userId,
            [Music("  My Music  "), Plain(LibraryFolderKind.Pictures, "Pictures")],
            TestContext.Current.CancellationToken);

        _addedDirectories.Select(d => d.Name).Should().Equal("My Music", "Pictures");
        _addedDirectories.Should().AllSatisfy(d =>
        {
            d.OwnerId.Should().Be(_userId);
            d.ParentId.Should().BeNull();
        });

        var policy = _addedPolicies.Should().ContainSingle().Subject;
        policy.DirectoryId.Should().Be(_addedDirectories[0].Id);
        policy.InheritedByChildren.Should().BeTrue();

        _addedRules.Should().HaveCount(2).And.AllSatisfy(r => r.PolicyId.Should().Be(policy.Id));

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishTour_noFolders_stillFinishesTheTour()
    {
        await CreateSut().FinishTourAsync(_userId, [], TestContext.Current.CancellationToken);

        await _users.Received(1).AdvanceOnboardingAsync(
            _userId, OnboardingStep.Tour, OnboardingStep.Done, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishTour_existingFolderWithPolicy_reusesFolderAndKeepsItsPolicy()
    {
        var existing = new Directory { Id = Guid.NewGuid(), Name = "Music", OwnerId = _userId };
        GivenExistingRootFolder(existing);
        _policies.ExistsForDirectoryAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().FinishTourAsync(_userId, [Music()], TestContext.Current.CancellationToken);

        _addedDirectories.Should().BeEmpty();
        _addedPolicies.Should().BeEmpty();
        _addedRules.Should().BeEmpty();
    }

    [Fact]
    public async Task FinishTour_existingFolderWithoutPolicy_addsAutomationToIt()
    {
        var existing = new Directory { Id = Guid.NewGuid(), Name = "Music", OwnerId = _userId };
        GivenExistingRootFolder(existing);

        await CreateSut().FinishTourAsync(_userId, [Music()], TestContext.Current.CancellationToken);

        _addedDirectories.Should().BeEmpty();
        _addedPolicies.Should().ContainSingle().Which.DirectoryId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task FinishTour_notesChoice_createsTheNotesSystemFolderUnderItsDefaultName()
    {
        await CreateSut().FinishTourAsync(_userId, [Plain(LibraryFolderKind.Notes, "Journal")],
            TestContext.Current.CancellationToken);

        var folder = _addedDirectories.Should().ContainSingle().Subject;
        folder.Name.Should().Be("Notes");
        folder.SystemKind.Should().Be(DirectorySystemKind.Notes);
        _addedPolicies.Should().BeEmpty();
    }

    [Fact]
    public async Task FinishTour_notesChoiceMatchingAnExistingFolder_marksThatFolder()
    {
        var existing = new Directory { Id = Guid.NewGuid(), Name = "Notes", OwnerId = _userId };
        GivenExistingRootFolder(existing);

        await CreateSut().FinishTourAsync(_userId, [Plain(LibraryFolderKind.Notes, "Notes")],
            TestContext.Current.CancellationToken);

        _addedDirectories.Should().BeEmpty();
        existing.SystemKind.Should().Be(DirectorySystemKind.Notes);
        _directories.Received(1).Update(existing);
    }

    [Fact]
    public async Task FinishTour_ownerAlreadyHasANotesFolder_addsNoSecondOne()
    {
        _directories.ExistsAsync(Arg.Any<Expression<Func<Directory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateSut().FinishTourAsync(_userId, [Plain(LibraryFolderKind.Notes, "Notes")],
            TestContext.Current.CancellationToken);

        _addedDirectories.Should().BeEmpty();
        _directories.DidNotReceive().Update(Arg.Any<Directory>());
    }

    [Fact]
    public async Task FinishTour_saveFails_rollsBackAndRethrows()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database down")));

        var act = () => CreateSut().FinishTourAsync(_userId, [Music()], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database down");
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }
}
