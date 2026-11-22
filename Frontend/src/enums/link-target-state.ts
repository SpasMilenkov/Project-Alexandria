// Mirrors the backend's LinkTargetState. Unavailable covers missing, deleted for good and someone
// else's, which the server never tells apart.
export enum LinkTargetState {
  Available = 0,
  Trashed = 1,
  Unavailable = 2,
}
