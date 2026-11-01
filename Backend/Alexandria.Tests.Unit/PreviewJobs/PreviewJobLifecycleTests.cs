using Alexandria.Common;
using Alexandria.Common.Exceptions.Preview.Images;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class PreviewJobLifecycleTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();

    public PreviewJobLifecycleTests()
    {
        _unitOfWork.Jobs.Returns(_jobs);
    }

    [Fact]
    public async Task fail_with_permanent_error_exhausts_retries()
    {
        var jobId = Guid.NewGuid();

        await PreviewJobLifecycle.FailAsync(_unitOfWork, jobId, new UnsupportedImageFormatException("image/x-foo"),
            TestContext.Current.CancellationToken);

        await _jobs.Received(1).UpdateStatusAsync(jobId, JobStatus.Failed, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _jobs.Received(1).MarkNonRetryableAsync(jobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task fail_with_transient_error_keeps_retry_budget()
    {
        var jobId = Guid.NewGuid();

        await PreviewJobLifecycle.FailAsync(_unitOfWork, jobId, new IOException("disk hiccup"),
            TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().MarkNonRetryableAsync(jobId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task release_hands_the_claim_back()
    {
        var jobId = Guid.NewGuid();

        await PreviewJobLifecycle.ReleaseAsync(_unitOfWork, jobId);

        await _jobs.Received(1).ReleaseClaimAsync(jobId, CancellationToken.None);
    }
}
