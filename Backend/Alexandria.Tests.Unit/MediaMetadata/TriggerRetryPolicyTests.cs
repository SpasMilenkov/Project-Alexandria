using Alexandria.Workers.MediaMetadata.Workers;
using AwesomeAssertions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class TriggerRetryPolicyTests
{
    private static IReadOnlyBasicProperties PropsWithHeaders(IDictionary<string, object?>? headers)
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();
        props.Headers.Returns(headers);
        return props;
    }

    [Fact]
    public void read_attempts_missing_properties_returns_zero()
    {
        TriggerRetryPolicy.ReadAttempts(null).Should().Be(0);
    }

    [Fact]
    public void read_attempts_missing_headers_returns_zero()
    {
        TriggerRetryPolicy.ReadAttempts(PropsWithHeaders(null)).Should().Be(0);
        TriggerRetryPolicy.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>())).Should().Be(0);
    }

    [Fact]
    public void read_attempts_parses_int_and_long_headers()
    {
        TriggerRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-trigger-attempts"] = 2 }))
            .Should().Be(2);
        TriggerRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-trigger-attempts"] = 4L }))
            .Should().Be(4);
    }

    [Fact]
    public void read_attempts_clamps_garbage_to_zero()
    {
        TriggerRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-trigger-attempts"] = -3 }))
            .Should().Be(0);
        TriggerRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-trigger-attempts"] = "nope" }))
            .Should().Be(0);
    }

    [Fact]
    public void next_attempt_number_increments_stored_count()
    {
        TriggerRetryPolicy.NextAttemptNumber(null).Should().Be(1);
        TriggerRetryPolicy.NextAttemptNumber(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-trigger-attempts"] = 2 }))
            .Should().Be(3);
    }

    [Fact]
    public void should_park_parks_at_and_beyond_max()
    {
        TriggerRetryPolicy.ShouldPark(4, 5).Should().BeFalse();
        TriggerRetryPolicy.ShouldPark(5, 5).Should().BeTrue();
        TriggerRetryPolicy.ShouldPark(9, 5).Should().BeTrue();
    }

    [Fact]
    public async Task publish_writes_attempt_header_body_and_routing()
    {
        var channel = Substitute.For<IChannel>();
        channel.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        var body = "698e35c3-00b2-42d1-9f02-c9f808a3d977"u8.ToArray();

        await TriggerRetryPolicy.PublishWithAttemptsAsync(
            channel, "content-exchange", "media-metadata.trigger.parking",
            body, 5, TestContext.Current.CancellationToken);

        await channel.Received(1).BasicPublishAsync(
            "content-exchange", "media-metadata.trigger.parking", false,
            Arg.Is<BasicProperties>(p =>
                p.Persistent
                && p.Headers != null
                && p.Headers.ContainsKey("x-trigger-attempts")
                && (int)p.Headers["x-trigger-attempts"]! == 5),
            Arg.Is<ReadOnlyMemory<byte>>(b => b.ToArray().SequenceEqual(body)),
            Arg.Any<CancellationToken>());
    }
}