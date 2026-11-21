import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { DocumentTextOverviewResponse } from "@/api/documentTextStats";

import DocumentTextCoverage from "@/components/dashboard/integrations/DocumentTextCoverage.vue";
import { DocumentTextStatus } from "@/enums";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

const retryFailed = vi.fn();
vi.mock("@/mutations/documentTextStats", () => ({
  useRetryFailedDocumentText: () => ({ isLoading: ref(false), mutateAsync: retryFailed }),
}));

const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
vi.mock("@/composables/useAppToast", () => ({ useAppToast: () => toast }));
vi.mock("@/composables/useLiveRefresh", () => ({ useLiveRefresh: vi.fn() }));

const overview = (overrides: Partial<DocumentTextOverviewResponse> = {}) => ({
  maxAttempts: 3,
  recentFailures: [
    {
      attemptCount: 1,
      contentObjectId: "c1",
      errorDetail: "pdftotext exited with 1",
      fileName: "scan.pdf",
      lastAttemptAt: "2026-11-20T10:00:00Z",
      mimeType: "application/pdf",
      willRetry: true,
    },
    {
      attemptCount: 3,
      contentObjectId: "c2",
      errorDetail: null,
      fileName: null,
      lastAttemptAt: "2026-11-20T09:00:00Z",
      mimeType: null,
      willRetry: false,
    },
  ],
  statusCounts: [
    { count: 12, status: DocumentTextStatus.Extracted },
    { count: 3, status: DocumentTextStatus.NoTextLayer },
    { count: 1, status: DocumentTextStatus.PasswordProtected },
    { count: 2, status: DocumentTextStatus.Failed },
  ],
  waitingCount: 4,
  ...overrides,
});

const mountCoverage = (data: DocumentTextOverviewResponse | undefined, isPending = false) => {
  mockUseQuery.mockReturnValue({
    data: ref(data),
    error: ref<Error | null>(null),
    isPending: ref(isPending),
    refetch: vi.fn(),
  });

  return mount(DocumentTextCoverage);
};

describe("DocumentTextCoverage", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
    retryFailed.mockReset();
    toast.success.mockReset();
    toast.info.mockReset();
  });

  it("shows the counts, with scans and locked files as unsearchable", () => {
    const text = mountCoverage(overview()).text();

    expect(text).toContain("Searchable12");
    expect(text).toContain("Waiting4");
    expect(text).toContain("Failed2");
    expect(text).toContain("Can't be searched4");
  });

  it("lists failures, naming only the admin's own files", () => {
    const rows = mountCoverage(overview()).findAll("[data-document-text-failure]");

    expect(rows).toHaveLength(2);
    expect(rows[0]?.text()).toContain("scan.pdf");
    expect(rows[0]?.text()).toContain("Try 1 of 3, will retry");
    expect(rows[0]?.text()).toContain("pdftotext exited with 1");
    expect(rows[1]?.text()).toContain("A file in another account");
    expect(rows[1]?.text()).toContain("Details hidden for another account");
    expect(rows[1]?.text()).toContain("Gave up after 3 tries");
  });

  it("explains each reason a document can't be searched", () => {
    const reasons = mountCoverage(overview()).findAll("[data-unsearchable-reason]");

    expect(reasons.map((reason) => reason.text())).toEqual([
      expect.stringContaining("Scanned without text"),
      expect.stringContaining("Password protected"),
    ]);
  });

  it("queues the failures again and says how many", async () => {
    retryFailed.mockResolvedValue({ reset: 2 });
    const wrapper = mountCoverage(overview());

    const button = wrapper.findAll("button").find((b) => b.text().includes("Retry all"));
    expect(button).toBeDefined();
    await button?.trigger("click");
    await flushPromises();

    expect(retryFailed).toHaveBeenCalledOnce();
    expect(toast.success).toHaveBeenCalledWith("Queued 2 documents for another try");
  });

  it("offers no retry without failures", () => {
    const wrapper = mountCoverage(
      overview({
        recentFailures: [],
        statusCounts: [{ count: 12, status: DocumentTextStatus.Extracted }],
      }),
    );

    expect(wrapper.text()).not.toContain("Retry all");
    expect(wrapper.text()).toContain("No failures");
    expect(wrapper.text()).toContain("Every document read so far has text");
  });

  it("shows spinners until the overview arrives", () => {
    const wrapper = mountCoverage(undefined, true);

    expect(wrapper.findAll("[role='status']").length).toBeGreaterThan(0);
    expect(wrapper.find("[data-document-text-failure]").exists()).toBe(false);
  });
});
