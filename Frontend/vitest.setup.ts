import { vi } from "vitest";

vi.mock("@/utils/logger", () => ({
  logger: { log: vi.fn(), error: vi.fn(), warn: vi.fn() },
}));

HTMLAnchorElement.prototype.click = vi.fn();
