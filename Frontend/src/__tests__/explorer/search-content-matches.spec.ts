import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { ContentMatchesResponse } from "@/api/file";

import SearchContentMatches from "@/components/dashboard/file-system/Modals/search/SearchContentMatches.vue";
import { DocumentTextStatus } from "@/enums";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

const response = (overrides: Partial<ContentMatchesResponse> = {}): ContentMatchesResponse => ({
  fragments: [
    {
      segments: [
        { isMatch: false, text: "the hotel in " },
        { isMatch: true, text: "Paris" },
        { isMatch: false, text: " charged extra" },
      ],
    },
  ],
  isTruncated: false,
  status: DocumentTextStatus.Extracted,
  ...overrides,
});

const mountMatches = (state: {
  data?: ContentMatchesResponse;
  error?: Error | null;
  isPending?: boolean;
}) => {
  mockUseQuery.mockReturnValue({
    data: ref(state.data),
    error: ref(state.error ?? null),
    isPending: ref(state.isPending ?? false),
  });

  return mount(SearchContentMatches, {
    props: { fileId: "f1", query: "paris hotel", versionId: "v2" },
  });
};

describe("SearchContentMatches", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
  });

  it("asks for the excerpts of this version and query", () => {
    mountMatches({ data: response() });

    const getter = mockUseQuery.mock.calls[0]?.[0] as () => { key: unknown[] };
    expect(getter().key).toEqual(["files", "content-matches", "f1", "v2", "paris hotel"]);
  });

  it("marks every typed word and keeps the context as plain text", () => {
    const wrapper = mountMatches({ data: response() });

    // The server marked "Paris"; "hotel" was typed too, so it is marked as well.
    expect(wrapper.findAll("mark").map((mark) => mark.text())).toEqual(["hotel", "Paris"]);
    expect(wrapper.find("[data-content-excerpt]").text()).toBe(
      "…the hotel in Paris charged extra…",
    );
  });

  it("never renders markup that came from a document", () => {
    const wrapper = mountMatches({
      data: response({
        fragments: [{ segments: [{ isMatch: true, text: "<img src=x onerror=alert(1)>" }] }],
      }),
    });

    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.find("mark").text()).toBe("<img src=x onerror=alert(1)>");
  });

  it("shows a spinner while the excerpts load", () => {
    const wrapper = mountMatches({ isPending: true });

    expect(wrapper.find("[role='status']").exists()).toBe(true);
    expect(wrapper.find("[data-content-excerpt]").exists()).toBe(false);
  });

  it("explains a match with no excerpt", () => {
    const wrapper = mountMatches({ data: response({ fragments: [] }) });

    expect(wrapper.text()).toContain("no excerpt could be cut");
  });

  it("says so when the excerpts fail to load", () => {
    const wrapper = mountMatches({ error: new Error("boom") });

    expect(wrapper.text()).toContain("Couldn't load the excerpts.");
  });

  it("notes when only the start of a long document is searched", () => {
    expect(mountMatches({ data: response({ isTruncated: true }) }).text()).toContain(
      "only its start is searched",
    );
    expect(mountMatches({ data: response() }).text()).not.toContain("only its start is searched");
  });
});
