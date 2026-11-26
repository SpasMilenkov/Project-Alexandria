namespace Builder.Deployment;

// One device's outcome from the transcoding worker's GPU probe: Driver names the VAAPI driver
// that encoded, Detail says why a device could not.
public sealed record VideoGpuTestResult(string Device, bool Usable, string? Driver, string? Detail);
