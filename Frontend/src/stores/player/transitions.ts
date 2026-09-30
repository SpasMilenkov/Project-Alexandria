export class Stale extends Error {}

export type Guard = <T>(promise: Promise<T>, discard?: (value: T) => void) => Promise<T>;

export interface RunOptions {
  message?: string;
  retry?: () => Promise<void>;
}

export interface TransitionHooks {
  onError?: (err: unknown, options: RunOptions) => void;
  onSettled?: () => void;
}

const DEFAULT_MESSAGE = "Something went wrong. Retry when ready.";

/**
 * Serializes every navigation and context change, and is the single error
 * boundary: anything a task throws (other than Stale) goes to onError once,
 * with the message and retry the intent supplied. replace supersedes
 * in-flight work first, so only the latest context-replacing intent runs.
 */
export const createTransitions = (hooks: TransitionHooks = {}) => {
  let generation = 0;
  let chain: Promise<void> = Promise.resolve();

  const supersede = () => {
    generation++;
  };

  const run = (task: (guard: Guard) => Promise<void>, options: RunOptions = {}): Promise<void> => {
    const start = generation;
    const guard: Guard = async (promise, discard) => {
      const value = await promise;
      if (start !== generation) {
        discard?.(value);
        throw new Stale();
      }
      return value;
    };
    const op = chain.then(() => {
      if (start !== generation) return undefined;
      return task(guard).catch((err: unknown) => {
        if (err instanceof Stale) return;
        if (hooks.onError) {
          hooks.onError(err, { message: options.message ?? DEFAULT_MESSAGE, retry: options.retry });
          return;
        }
        throw err;
      });
    });
    chain = op.catch(() => undefined);
    const done = op.catch(() => undefined);
    if (hooks.onSettled) void done.finally(() => hooks.onSettled?.());
    return done;
  };

  const replace = (
    task: (guard: Guard) => Promise<void>,
    options: RunOptions = {},
  ): Promise<void> => {
    supersede();
    return run(task, options);
  };

  return { run, replace, supersede, current: () => generation };
};
