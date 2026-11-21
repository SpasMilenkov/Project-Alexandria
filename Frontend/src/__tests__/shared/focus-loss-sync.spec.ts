import { afterEach, describe, expect, it, vi } from "vitest";
import { effectScope } from "vue";

import { useFocusLossSync } from "@/composables/useFocusLossSync";

// MutationObserver callbacks run as microtasks.
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("useFocusLossSync", () => {
  const scope = effectScope();
  const captured = vi.fn();
  const plain = vi.fn();

  const setup = () => {
    scope.run(() => useFocusLossSync());
    window.addEventListener("blur", captured, true);
    window.addEventListener("blur", plain);

    const input = document.createElement("input");
    document.body.append(input);
    input.focus();
    captured.mockClear();
    plain.mockClear();
    return input;
  };

  afterEach(() => {
    scope.stop();
    window.removeEventListener("blur", captured, true);
    window.removeEventListener("blur", plain);
    document.body.replaceChildren();
    captured.mockReset();
    plain.mockReset();
  });

  it("sends a blur to capture listeners when the focused input is removed", async () => {
    const input = setup();

    input.remove();
    await settle();

    expect(captured).toHaveBeenCalledOnce();
    expect((captured.mock.calls[0]?.[0] as FocusEvent).relatedTarget).toBeNull();
  });

  it("leaves plain window blur listeners alone, so drags are not cancelled", async () => {
    const input = setup();

    input.remove();
    await settle();

    expect(plain).not.toHaveBeenCalled();
  });

  it("stays quiet when something other than the focused element is removed", async () => {
    setup();
    const other = document.createElement("div");
    document.body.append(other);

    other.remove();
    await settle();

    expect(captured).not.toHaveBeenCalled();
  });
});
