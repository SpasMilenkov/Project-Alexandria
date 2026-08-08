using Alexandria.Common.Helpers;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.AutoTagging;

public class EnrichmentPayloadTests
{
    [Fact]
    public void IsFailure_true_for_failure_row()
        => EnrichmentPayload.IsFailure("""{"success":false,"error":"boom"}""").Should().BeTrue();

    [Fact]
    public void IsFailure_false_for_successful_genre_payload()
        => EnrichmentPayload.IsFailure("""{"backbone":"maest","predictions":[{"label":"Rock","confidence":0.9}]}""")
            .Should().BeFalse();

    [Fact]
    public void IsFailure_false_for_successful_mood_payload()
        => EnrichmentPayload.IsFailure("""{"axis":"valence","pole":"happy","confidence":0.8}""")
            .Should().BeFalse();

    [Fact]
    public void IsFailure_false_when_success_key_is_true()
        => EnrichmentPayload.IsFailure("""{"success":true,"predictions":[]}""").Should().BeFalse();

    [Fact]
    public void IsFailure_true_for_unparseable_json()
        => EnrichmentPayload.IsFailure("{not json").Should().BeTrue();

    [Fact]
    public void IsFailure_true_for_non_object_json()
        => EnrichmentPayload.IsFailure("""["a","b"]""").Should().BeTrue();
}