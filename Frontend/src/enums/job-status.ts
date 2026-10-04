// Mirrors backend JobStatus ordinals (Alexandria.Data.Models.Enumerators.JobStatus).
// Single source for job lifecycle state on the frontend.
export enum JobStatus {
  Queued = 0,
  Processing = 1,
  Partial = 2,
  Ready = 3,
  Failed = 4,
  Cancelled = 5,
  CancellationRequested = 6,
}
