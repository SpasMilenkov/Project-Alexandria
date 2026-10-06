using System.Globalization;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public static class StorageCapacityReceipt
{
    public const string FileName = "garage-capacity.bytes";

    public static long Read(string targetDirectory)
    {
        var path = Path.Combine(targetDirectory, "init-output", FileName);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Garage initialization did not publish its actual layout capacity. Rerun storage initialization and inspect its output before starting application services.");
        }

        if (new FileInfo(path).LinkTarget is not null)
        {
            throw new InvalidOperationException("The Garage capacity receipt must be a regular file.");
        }

        var content = File.ReadAllText(path).Trim();

        if (!long.TryParse(content, NumberStyles.None, CultureInfo.InvariantCulture, out var capacity) || capacity <= 0)
        {
            throw new InvalidOperationException("Garage did not report a valid actual layout capacity. Preserve the layout and inspect storage initialization output.");
        }

        return capacity;
    }

    public static long Commit(InstallationContext context)
    {
        var capacity = Read(context.InstallPath);
        var storage = context.Config.Storage;
        storage.ActualGarageCapacityBytes = capacity;

        if (capacity % StorageSettings.BytesPerGb == 0)
        {
            storage.GarageCapacityGb = capacity / StorageSettings.BytesPerGb;
            context.Config.EnvironmentValues["GARAGE_CAPACITY"] = storage.CapacityArgument;
        }

        storage.CapacityReviewed = false;
        storage.PreserveExistingCapacity = true;
        context.Config.EnvironmentValues["GARAGE_CAPACITY_CHANGE_REQUESTED"] = "false";

        var environmentPath = Path.Combine(context.InstallPath, ".env");

        if (File.Exists(environmentPath))
        {
            var content = File.ReadAllText(environmentPath);
            var values = EnvGenerator.Parse(content);
            values["GARAGE_CAPACITY"] = context.Config.EnvironmentValues.GetValueOrDefault("GARAGE_CAPACITY", string.Empty);
            values["GARAGE_CAPACITY_CHANGE_REQUESTED"] = "false";
            EnvGenerator.WriteFile(environmentPath, values, content);
            context.Config.EnvironmentValues = values;
            context.Config.Credentials = new Dictionary<string, string>(values);
        }

        return capacity;
    }
}
