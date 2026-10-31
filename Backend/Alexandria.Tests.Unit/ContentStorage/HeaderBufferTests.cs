using Alexandria.Services.Storage.Content;
using Xunit;

namespace Alexandria.Tests.Unit.ContentStorage;

public class HeaderBufferTests
{
    private static byte[] Sequence(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++) bytes[i] = (byte)(i % 251);
        return bytes;
    }

    [Fact]
    public void H01_one_chunk_larger_than_capacity_keeps_the_first_capacity_bytes()
    {
        var input = Sequence(100);
        var buffer = new HeaderBuffer(16);

        buffer.Append(input);

        Assert.Equal(input[..16], buffer.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(7)]
    public void H02_small_chunks_accumulate_to_the_same_result_as_one_big_chunk(int chunkSize)
    {
        var input = Sequence(100);
        var chunked = new HeaderBuffer(16);
        var whole = new HeaderBuffer(16);

        for (var offset = 0; offset < input.Length; offset += chunkSize)
            chunked.Append(input.AsSpan(offset, Math.Min(chunkSize, input.Length - offset)));
        whole.Append(input);

        Assert.Equal(whole.ToArray(), chunked.ToArray());
    }

    [Fact]
    public void H03_input_exactly_equal_to_capacity()
    {
        var input = Sequence(16);
        var buffer = new HeaderBuffer(16);

        buffer.Append(input);

        Assert.Equal(input, buffer.ToArray());
    }

    [Fact]
    public void H04_input_shorter_than_capacity_returns_only_the_input()
    {
        var input = Sequence(5);
        var buffer = new HeaderBuffer(16);

        buffer.Append(input);

        Assert.Equal(input, buffer.ToArray());
    }

    [Fact]
    public void H05_zero_length_chunks_are_ignored()
    {
        var buffer = new HeaderBuffer(16);

        buffer.Append([]);
        buffer.Append(Sequence(3));
        buffer.Append([]);

        Assert.Equal(Sequence(3), buffer.ToArray());
    }

    [Fact]
    public void H06_appends_after_the_buffer_is_full_change_nothing()
    {
        var buffer = new HeaderBuffer(4);
        buffer.Append(new byte[] { 1, 2, 3, 4 });

        buffer.Append(new byte[] { 9, 9, 9 });

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.ToArray());
    }
}
