import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { ContentMatchesResponse } from "@/api/file";

import SearchRowExcerpt from "@/components/dashboard/file-system/Modals/search/SearchRowExcerpt.vue";
import { DocumentTextStatus } from "@/enums";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

let reportVisibility: (entries: { isIntersecting: boolean }[]) => void = () => {};
const stopObserving = vi.fn();
vi.mock("@vueuse/core", () => ({
  useIntersectionObserver: (_target: unknown, callback: typeof reportVisibility) => {
    reportVisibility = callback;
    return { stop: stopObserving };
  },
}));

const response: ContentMatchesResponse = {
  fragments: [
    {
      segments: [
        {
          isMatch: false,
          text: "Algorithm Name Standardized Name Full Expansion CRYSTALS-Kyber renamed to ML-",
        },
        { isMatch: true, text: "KEM" },
        { isMatch: false, text: " Module-Lattice" },
      ],
    },
  ],
  isTruncated: false,
  status: DocumentTextStatus.Extracted,
};

type QueryOptions = () => { enabled: boolean; key: unknown[] };

const mountRow = (data?: ContentMatchesResponse) => {
  mockUseQuery.mockReturnValue({ data: ref(data) });

  return mount(SearchRowExcerpt, {
    props: { fileId: "f1", query: "ML-KEM", versionId: "v1" },
  });
};

const options = () => {
  const getter = mockUseQuery.mock.calls[0]?.[0] as QueryOptions | undefined;
  if (!getter) {
    throw new Error("useQuery was not called");
  }
  return getter();
};

describe("SearchRowExcerpt", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
    stopObserving.mockReset();
  });

  it("waits until the row is seen before asking for excerpts", async () => {
    const row = mountRow();

    expect(options().enabled).toBe(false);
    expect(row.text()).toContain("Found in the text");

    reportVisibility([{ isIntersecting: true }]);
    await row.vm.$nextTick();

    expect(options().enabled).toBe(true);
    // Rows ask only for the first match; the preview pane keeps its own full set.
    expect(options().key).toEqual(["files", "first-content-match", "f1", "v1", "ML-KEM"]);
    expect(stopObserving).toHaveBeenCalled();
  });

  it("shows the first excerpt starting a few words before the match", () => {
    const row = mountRow(response);

    expect(row.findAll("mark").map((mark) => mark.text())).toEqual(["ML", "KEM"]);
    expect(row.text()).toContain("…renamed to ML-KEM Module-Lattice");
    expect(row.text()).not.toContain("Algorithm Name");
  });
});
