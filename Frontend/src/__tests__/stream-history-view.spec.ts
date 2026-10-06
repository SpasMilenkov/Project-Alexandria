import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { ref, watchEffect } from "vue";

import StreamHistoryView from "@/views/dashboard/streaming/StreamHistoryView.vue";

const mocks = vi.hoisted(() => ({ history: vi.fn() }));

vi.mock("@/queries/streaming", () => ({ getHistory: mocks.history }));

vi.mock("@pinia/colada", () => ({
  useQuery: (options: () => unknown) => {
    watchEffect(options);

    return {
      data: ref({
        items: [
          {
            id: "history",
            fileId: "song",
            title: "Song",

            positionSeconds: 30,
            maxPositionReachedSeconds: 30,
            totalListenedSeconds: 30,
            qualifiedPlayCount: 1,
            hasFinished: false,

            lastPlayedAt: "2026-01-01T12:00:00Z",
            lastAccessedAt: "2026-01-01T12:00:00Z",
            createdAt: "2026-01-01T12:00:00Z",
          },
        ],
        totalCount: 1,
        totalPages: 1,
      }),
      error: ref<unknown>(null),
      isLoading: ref(false),
      refetch: vi.fn(),
    };
  },
}));

const mountHistory = () => mount(StreamHistoryView, {
  global: {
    stubs: {
      Button: { template: "<button><slot /></button>" },
      Badge: { template: "<span><slot /></span>" },
      Collapsible: { template: "<section><slot /><slot name='content' /></section>" },
      Icon: true,
    },
  },
});

describe("stream history qualified plays", () => {
  it("labels a qualified play without claiming the song was completed", () => {
    const wrapper = mountHistory();

    try {
      expect(wrapper.text()).toContain("Played");
      expect(wrapper.text()).toContain("Plays");
      expect(wrapper.text()).toContain("Last played");
      expect(wrapper.text()).not.toContain("Completed");
      expect(wrapper.text()).not.toContain("Times completed");
    } finally {
      wrapper.unmount();
    }
  });

  it("filters history by qualification instead of completion", async () => {
    const wrapper = mountHistory();

    try {
      const played = wrapper.findAll("button").find((button) => button.text() === "Played")!;

      await played.trigger("click");

      expect(mocks.history).toHaveBeenLastCalledWith({ qualified: true, currentPage: 1, pageSize: 20 });
    } finally {
      wrapper.unmount();
    }
  });
});
