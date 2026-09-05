using Alexandria.Common.Mapping;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Common;

public class ServiceTypeMappingTests
{
    [Theory]
    [InlineData(ServiceType.MediaPreviews, JobType.MediaPreview)]
    [InlineData(ServiceType.DocumentPreviews, JobType.DocumentPreview)]
    [InlineData(ServiceType.Transpilation, JobType.Transpilation)]
    [InlineData(ServiceType.Lyrics, JobType.LyricsFetch)]
    [InlineData(ServiceType.MediaMetadata, JobType.MetadataEnrichment)]
    public void to_job_type_maps_worker_service_to_backing_job_type(ServiceType service, JobType expected)
    {
        service.ToJobType().Should().Be(expected);
    }

    [Fact]
    public void to_job_type_api_without_job_table_returns_null()
    {
        ServiceType.Api.ToJobType().Should().BeNull();
    }

    [Fact]
    public void to_job_type_unknown_service_throws()
    {
        var unknown = (ServiceType)999;

        var act = () => unknown.ToJobType();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}