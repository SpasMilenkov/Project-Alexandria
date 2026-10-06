using System.Globalization;
using System.Text.Json.Serialization;

namespace Builder.Models;

public sealed class StorageSettings
{
    public const long BytesPerGb = 1_000_000_000;
    public const long DefaultCapacityGb = 10;
    public long GarageCapacityGb { get; set; } = DefaultCapacityGb;
    public long? ActualGarageCapacityBytes { get; set; }

    [JsonIgnore]
    public bool CapacityReviewed { get; set; }

    [JsonIgnore]
    public bool PreserveExistingCapacity { get; set; }

    [JsonIgnore]
    public bool HasRecordedCapacity { get; set; }

    public void Validate()
    {
        if (GarageCapacityGb < 1 || GarageCapacityGb > long.MaxValue / BytesPerGb)
        {
            throw new InvalidOperationException("Garage capacity must be a whole number of GB, at least 1 GB.");
        }

        if (ActualGarageCapacityBytes is <= 0)
        {
            throw new InvalidOperationException("The recorded Garage layout capacity must be a positive byte count.");
        }
    }

    [JsonIgnore]
    public string CapacityArgument => GarageCapacityGb.ToString(CultureInfo.InvariantCulture) + "G";
}
