using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Converters;

public class BigIntegerJsonConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return new BigInteger(reader.GetInt64());
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (BigInteger.TryParse(stringValue, out var result))
            {
                return result;
            }
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to BigInteger");
    }

    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}