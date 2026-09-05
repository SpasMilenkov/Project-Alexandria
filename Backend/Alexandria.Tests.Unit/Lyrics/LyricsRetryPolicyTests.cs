using Alexandria.Workers.Lyrics;
using AwesomeAssertions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Lyrics;

public class LyricsRetryPolicyTests
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
        LyricsRetryPolicy.ReadAttempts(null).Should().Be(0);
    }

    [Fact]
    public void read_attempts_missing_headers_returns_zero()
    {
        LyricsRetryPolicy.ReadAttempts(PropsWithHeaders(null)).Should().Be(0);
        LyricsRetryPolicy.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>())).Should().Be(0);
    }

    [Fact]
    public void read_attempts_parses_int_and_long_headers()
    {
        LyricsRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-lyrics-attempts"] = 2 }))
            .Should().Be(2);
        LyricsRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-lyrics-attempts"] = 4L }))
            .Should().Be(4);
    }

    [Fact]
    public void read_attempts_clamps_garbage_to_zero()
    {
        LyricsRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-lyrics-attempts"] = -3 }))
            .Should().Be(0);
        LyricsRetryPolicy.ReadAttempts(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-lyrics-attempts"] = "nope" }))
            .Should().Be(0);
    }

    [Fact]
    public void next_attempt_number_increments_stored_count()
    {
        LyricsRetryPolicy.NextAttemptNumber(null).Should().Be(1);
        LyricsRetryPolicy.NextAttemptNumber(
                PropsWithHeaders(new Dictionary<string, object?> { ["x-lyrics-attempts"] = 2 }))
            .Should().Be(3);
    }

    [Fact]
    public void should_park_parks_at_and_beyond_max()
    {
        LyricsRetryPolicy.ShouldPark(4, 5).Should().BeFalse();
        LyricsRetryPolicy.ShouldPark(5, 5).Should().BeTrue();
        LyricsRetryPolicy.ShouldPark(9, 5).Should().BeTrue();
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
        var body = Guid.NewGuid().ToByteArray();

        await LyricsRetryPolicy.PublishWithAttemptsAsync(
            channel, "content-exchange", "lyrics-parking",
            body, 5, TestContext.Current.CancellationToken);

        await channel.Received(1).BasicPublishAsync(
            "content-exchange", "lyrics-parking", false,
            Arg.Is<BasicProperties>(p =>
                p.Persistent
                && p.Headers != null
                && p.Headers.ContainsKey("x-lyrics-attempts")
                && (int)p.Headers["x-lyrics-attempts"]! == 5),
            Arg.Is<ReadOnlyMemory<byte>>(b => b.ToArray().SequenceEqual(body)),
            Arg.Any<CancellationToken>());
    }
}