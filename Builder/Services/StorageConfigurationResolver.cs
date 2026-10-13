using System.Globalization;
using Builder.Models;

namespace Builder.Services;

internal static class StorageConfigurationResolver
{
    internal static StorageSettings Resolve(
        StorageSettings draft, StorageSettings? saved, IReadOnlyDictionary<string, string> environment, bool repair)
    {
        draft.Validate();
        saved?.Validate();
        long? recordedGb = null;
        var hasCapacity = environment.TryGetValue("GARAGE_CAPACITY", out var capacity) && !string.IsNullOrWhiteSpace(capacity);

        if (hasCapacity)
        {
            if (!TryParseCapacity(capacity!, out var parsed))
            {
                throw new InvalidOperationException("The established GARAGE_CAPACITY value is invalid. Preserve the existing layout and recover its actual capacity before continuing.");
            }

            recordedGb = parsed;
        }

        var actual = saved?.ActualGarageCapacityBytes ?? draft.ActualGarageCapacityBytes;
        long? establishedBytes = actual;

        if (establishedBytes is null && recordedGb.HasValue)
        {
            establishedBytes = checked(recordedGb.Value * StorageSettings.BytesPerGb);
        }

        if (repair && draft.CapacityReviewed && establishedBytes.HasValue
            && checked(draft.GarageCapacityGb * StorageSettings.BytesPerGb) != establishedBytes.Value)
        {
            throw new InvalidOperationException($"The existing Garage capacity is {establishedBytes.Value.ToString(CultureInfo.InvariantCulture)} bytes. "
                + "Changing an established storage layout is a separate operation; the layout and installation files were preserved.");
        }

        var selectedGb = draft.GarageCapacityGb;

        if (repair && !draft.CapacityReviewed)
        {
            selectedGb = recordedGb ?? saved?.GarageCapacityGb ?? draft.GarageCapacityGb;

            if (actual.HasValue && actual.Value % StorageSettings.BytesPerGb == 0)
            {
                selectedGb = actual.Value / StorageSettings.BytesPerGb;
            }
        }

        return new StorageSettings
        {
            GarageCapacityGb = selectedGb,
            ActualGarageCapacityBytes = actual,
            CapacityReviewed = draft.CapacityReviewed,
            PreserveExistingCapacity = repair,
            HasRecordedCapacity = hasCapacity,
        };
    }

    internal static void ApplyEnvironment(StorageSettings settings, Dictionary<string, string> values)
    {
        values["GARAGE_CAPACITY"] = settings.PreserveExistingCapacity && settings.ActualGarageCapacityBytes is null
            && !settings.CapacityReviewed && !settings.HasRecordedCapacity
            ? string.Empty
            : settings.CapacityArgument;
        values["GARAGE_CAPACITY_CHANGE_REQUESTED"] = settings.PreserveExistingCapacity && settings.CapacityReviewed ? "true" : "false";
    }

    internal static bool TryParseCapacity(string value, out long gb)
    {
        gb = 0;
        return value.EndsWith('G') && long.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None,
            CultureInfo.InvariantCulture, out gb) && gb >= 1 && gb <= long.MaxValue / StorageSettings.BytesPerGb;
    }
}
