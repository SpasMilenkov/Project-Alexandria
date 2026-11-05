/**
 * Fires `onDwell` once a key has stayed unchanged for `delay` milliseconds.
 * Tracking a different key (or null) restarts or stops the wait; tracking the
 * same key again keeps the running wait untouched.
 */
export const createDwellTimer = <T>(delay: number, onDwell: (payload: T) => void) => {
  let currentKey: string | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;

  const clearTimer = () => {
    if (timer !== null) clearTimeout(timer);
    timer = null;
  };

  const track = (key: string | null, payload?: T) => {
    if (key === currentKey) return;

    clearTimer();
    currentKey = key;

    if (key === null || payload === undefined) return;

    timer = setTimeout(() => {
      timer = null;
      onDwell(payload);
    }, delay);
  };

  const reset = () => {
    clearTimer();
    currentKey = null;
  };

  return { reset, track };
};
