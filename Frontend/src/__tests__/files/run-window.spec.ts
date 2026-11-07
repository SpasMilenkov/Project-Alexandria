import { describe, expect, it, vi } from "vitest";

import { type WindowOptions, runWindow } from "@/features/upload/run-window";

interface Item {
  id: string;
  size: number;
}

const item = (id: string, size = 1): Item => ({ id, size });

const options = (overrides: Partial<WindowOptions<Item>> = {}): WindowOptions<Item> => ({
  alwaysAdmit: 2,
  maxBytes: 100,
  maxItems: 4,
  sizeOf: (entry) => entry.size,
  ...overrides,
});

const flush = async () => {
  await new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
};

// Tasks stay open until the test finishes them, so it can see exactly what was admitted.
const controlledRun = () => {
  const started: string[] = [];
  const finishers = new Map<string, () => void>();

  const run = (entry: Item) =>
    new Promise<void>((resolve) => {
      started.push(entry.id);
      finishers.set(entry.id, resolve);
    });

  return { finish: (id: string) => finishers.get(id)?.(), run, started };
};

describe("runWindow", () => {
  it("runs everything and resolves once all tasks finish", async () => {
    const seen: string[] = [];

    await runWindow(
      [item("a"), item("b"), item("c")],
      (entry) => {
        seen.push(entry.id);

        return Promise.resolve();
      },
      options(),
    );

    expect(seen).toEqual(["a", "b", "c"]);
  });

  it("resolves immediately with nothing to do", async () => {
    await expect(runWindow([], vi.fn(), options())).resolves.toBeUndefined();
  });

  it("stops admitting at the file limit and continues as tasks finish", async () => {
    const { finish, run, started } = controlledRun();
    const items = ["a", "b", "c", "d", "e"].map((id) => item(id));

    const done = runWindow(items, run, options({ maxItems: 3 }));

    await flush();
    expect(started).toEqual(["a", "b", "c"]);

    finish("a");
    await flush();
    expect(started).toEqual(["a", "b", "c", "d"]);

    for (const id of ["b", "c", "d"]) finish(id);
    await flush();
    finish("e");

    await done;
    expect(started).toEqual(["a", "b", "c", "d", "e"]);
  });

  it("stops admitting at the byte limit, keeping files in order", async () => {
    const { finish, run, started } = controlledRun();
    const items = [item("a", 40), item("b", 40), item("c", 40), item("d", 1)];

    const done = runWindow(items, run, options());

    await flush();

    // d would fit, but starting it would jump ahead of c.
    expect(started).toEqual(["a", "b"]);

    finish("a");
    await flush();
    expect(started).toEqual(["a", "b", "c", "d"]);

    for (const id of ["b", "c", "d"]) finish(id);
    await done;
  });

  it("always admits a few files however large they are", async () => {
    const { finish, run, started } = controlledRun();
    const items = [item("a", 5000), item("b", 5000), item("c", 5000)];

    const done = runWindow(items, run, options());

    await flush();
    expect(started).toEqual(["a", "b"]);

    finish("a");
    await flush();
    expect(started).toEqual(["a", "b", "c"]);

    finish("b");
    finish("c");
    await done;
  });

  it("stops starting new files once told to stop", async () => {
    const { finish, run, started } = controlledRun();
    let stop = false;

    const done = runWindow(
      [item("a"), item("b"), item("c")],
      run,
      options({ maxItems: 1, alwaysAdmit: 1, shouldStop: () => stop }),
    );

    await flush();
    stop = true;
    finish("a");

    await done;
    expect(started).toEqual(["a"]);
  });

  it("carries on when a task rejects", async () => {
    const seen: string[] = [];

    await runWindow(
      [item("a"), item("b")],
      (entry) => {
        seen.push(entry.id);

        return entry.id === "a" ? Promise.reject(new Error("task bug")) : Promise.resolve();
      },
      options({ alwaysAdmit: 1, maxItems: 1 }),
    );

    expect(seen).toEqual(["a", "b"]);
  });
});
