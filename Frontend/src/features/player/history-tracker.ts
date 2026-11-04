export interface HistoryClosePayload {
  endPositionSeconds: number;
  listenedSeconds: number;
  playbackFinished?: boolean;
}

export interface HistorySessionCallbacks {
  startSession: (fileId: string, startPositionSeconds: number) => Promise<{ id: string } | null>;
  closeSession: (sessionId: string, payload: HistoryClosePayload) => Promise<unknown>;
  verifyClosed: (sessionId: string, fileId: string) => Promise<boolean>;
  currentFileId: () => string | null;
}

interface FailedClose extends HistoryClosePayload {
  sessionId: string;
  fileId: string;
}

/**
 * Owns listening-history session bookkeeping for one player engine: closing
 * the previous session when playback moves on, guarding against duplicate
 * closes from pause/ended/load races, closing late-arriving starts for
 * abandoned playback, and flushing outstanding writes before a weighted
 * shuffle cycle reads history. The engine keeps the tickers and element
 * access; this unit keeps the async bookkeeping, so it can be tested without
 * a media element.
 */
export class ListeningHistoryTracker {
  private readonly callbacks: HistorySessionCallbacks;
  private active: { sessionId: string; fileId: string } | null = null;
  private playbackGeneration = 0;

  private pendingStart: {
    generation: number;
    fileId: string;
    close: HistoryClosePayload | null;
  } | null = null;

  private readonly closeSubmitted = new Set<string>();
  private readonly pendingCloses = new Map<string, Promise<void>>();
  private readonly failedCloses: FailedClose[] = [];
  private readonly pendingStarts: Promise<void>[] = [];
  private flushError: string | null = null;

  constructor(callbacks: HistorySessionCallbacks) {
    this.callbacks = callbacks;
  }

  get hasFailed(): boolean {
    return this.failedCloses.length > 0 || this.flushError !== null;
  }

  get failureMessage(): string | null {
    return this.flushError;
  }

  closeActive(payload: HistoryClosePayload): void {
    this.playbackGeneration++;

    if (!this.active) {
      if (this.pendingStart) this.pendingStart.close = { ...payload };

      return;
    }

    const { sessionId, fileId } = this.active;

    this.active = null;
    this.submitClose(sessionId, fileId, payload);
  }

  openNew(fileId: string, startPositionSeconds: number): void {
    const generation = ++this.playbackGeneration;
    const pending = { generation, fileId, close: null as HistoryClosePayload | null };

    this.pendingStart = pending;

    const run = (async (): Promise<void> => {
      let session: { id: string } | null = null;

      try {
        session = await this.callbacks.startSession(fileId, startPositionSeconds);
      } catch {
        return;
      }

      if (!session) return;

      const close = pending.close;

      if (
        generation !== this.playbackGeneration ||
        this.callbacks.currentFileId() !== fileId ||
        close
      ) {
        await this.submitClose(
          session.id,
          fileId,
          close ?? { endPositionSeconds: startPositionSeconds, listenedSeconds: 0 },
        ).catch(() => undefined);

        return;
      }

      this.active = { sessionId: session.id, fileId };
    })();

    this.pendingStarts.push(run);

    void run
      .catch(() => undefined)
      .finally(() => {
        if (this.pendingStart === pending) this.pendingStart = null;

        const index = this.pendingStarts.indexOf(run);

        if (index !== -1) this.pendingStarts.splice(index, 1);
      });
  }

  async flush(): Promise<void> {
    this.flushError = null;

    const retry = this.failedCloses.splice(0);

    for (const failed of retry) {
      this.closeSubmitted.delete(failed.sessionId);

      await this.submitClose(failed.sessionId, failed.fileId, {
        endPositionSeconds: failed.endPositionSeconds,
        listenedSeconds: failed.listenedSeconds,
        playbackFinished: failed.playbackFinished,
      }).catch(() => undefined);
    }

    await Promise.allSettled(this.pendingStarts);
    await Promise.allSettled(this.pendingCloses.values());

    if (this.failedCloses.length > 0) {
      this.flushError = "Listening history could not be saved.";
      throw new Error("history-flush-failed");
    }
  }

  private submitClose(
    sessionId: string,
    fileId: string,
    payload: HistoryClosePayload,
  ): Promise<void> {
    const existing = this.pendingCloses.get(sessionId);

    if (existing) return existing;
    if (this.closeSubmitted.has(sessionId)) return Promise.resolve();

    this.closeSubmitted.add(sessionId);

    const task = this.callbacks
      .closeSession(sessionId, payload)
      .then(
        () => undefined,
        async (err: unknown) => {
          const verified = await this.callbacks.verifyClosed(sessionId, fileId).catch(() => false);

          if (verified) return;

          this.failedCloses.push({ sessionId, fileId, ...payload });
          this.flushError = "Listening history could not be saved.";
          throw err;
        },
      )
      .finally(() => {
        this.pendingCloses.delete(sessionId);
      });

    // A handler from birth: the rejection is also observed through
    // pendingCloses, but without this a failure recorded before any flush
    // attaches would surface as an unhandled rejection.
    task.catch(() => undefined);
    this.pendingCloses.set(sessionId, task);

    return task;
  }
}
