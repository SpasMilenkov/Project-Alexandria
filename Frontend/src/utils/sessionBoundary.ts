export const SESSION_EPOCH_STORAGE_KEY = "alexandria:auth-epoch";
export const SESSION_BOUNDARY_CHANNEL = "alexandria:auth";

// Peers reload instead of running the auth teardown because those stores persist
// to the same localStorage and could overwrite the account that just signed in.
const INITIAL_SESSION_EPOCH = "initial";

type SessionBoundaryInvalidation = () => void;

interface SessionBoundaryMessage {
  type: "session-boundary";
}

let tabEpoch: string | null = null;
let channel: BroadcastChannel | null = null;
let initialized = false;
let invalidate: SessionBoundaryInvalidation = () => {
  window.location.reload();
};

const readStoredEpoch = (): string | null => {
  try {
    return window.localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);
  } catch {
    return null;
  }
};

const writeStoredEpoch = (epoch: string): boolean => {
  try {
    window.localStorage.setItem(SESSION_EPOCH_STORAGE_KEY, epoch);
    return true;
  } catch {
    return false;
  }
};

const handleBoundaryCheck = (): void => {
  const storedEpoch = readStoredEpoch();

  if (!storedEpoch || storedEpoch === tabEpoch) return;

  tabEpoch = storedEpoch;
  invalidate();
};

const onStorage = (event: StorageEvent): void => {
  if (event.key === SESSION_EPOCH_STORAGE_KEY || event.key === null) {
    handleBoundaryCheck();
  }
};

const onChannelMessage = (event: MessageEvent<SessionBoundaryMessage>): void => {
  if (event.data?.type === "session-boundary") {
    handleBoundaryCheck();
  }
};

const onVisibilityChange = (): void => {
  if (!document.hidden) {
    handleBoundaryCheck();
  }
};

export const ensureSessionEpoch = (): string | null => {
  const storedEpoch = readStoredEpoch();

  if (storedEpoch) {
    tabEpoch = storedEpoch;
    return storedEpoch;
  }

  if (!writeStoredEpoch(INITIAL_SESSION_EPOCH)) return null;

  tabEpoch = INITIAL_SESSION_EPOCH;
  return tabEpoch;
};

const createEpoch = (): string => {
  try {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      return crypto.randomUUID();
    }
  } catch {
    // randomUUID is unavailable outside secure contexts; fall through.
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
};

export const rotateSessionEpoch = (): string | null => {
  const epoch = createEpoch();

  if (!writeStoredEpoch(epoch)) return null;

  tabEpoch = epoch;

  const message: SessionBoundaryMessage = { type: "session-boundary" };

  try {
    channel?.postMessage(message);
  } catch {
    channel = null;
  }

  return epoch;
};

export const isSessionEpochCurrent = (): boolean => {
  const storedEpoch = readStoredEpoch();

  if (!storedEpoch || !tabEpoch) return true;

  return storedEpoch === tabEpoch;
};

export const stopSessionBoundaryWatcher = (): void => {
  if (initialized) {
    if (channel) {
      channel.removeEventListener("message", onChannelMessage);
      channel.close();
    }

    window.removeEventListener("storage", onStorage);
    window.removeEventListener("focus", handleBoundaryCheck);
    window.removeEventListener("pageshow", handleBoundaryCheck);
    document.removeEventListener("visibilitychange", onVisibilityChange);
  }

  channel = null;
  initialized = false;
  tabEpoch = null;
};

export const startSessionBoundaryWatcher = (
  onInvalidate: SessionBoundaryInvalidation = () => window.location.reload(),
): (() => void) => {
  if (initialized) return stopSessionBoundaryWatcher;

  initialized = true;
  invalidate = onInvalidate;
  ensureSessionEpoch();

  if (typeof BroadcastChannel !== "undefined") {
    let nextChannel: BroadcastChannel | null = null;

    try {
      nextChannel = new BroadcastChannel(SESSION_BOUNDARY_CHANNEL);
      nextChannel.addEventListener("message", onChannelMessage);
      channel = nextChannel;
    } catch {
      nextChannel?.close();
      channel = null;
    }
  }

  window.addEventListener("storage", onStorage);
  window.addEventListener("focus", handleBoundaryCheck);
  window.addEventListener("pageshow", handleBoundaryCheck);
  document.addEventListener("visibilitychange", onVisibilityChange);

  return stopSessionBoundaryWatcher;
};
