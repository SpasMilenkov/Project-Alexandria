import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";

import type { TextPreviewDto, TextPreviewLine } from "@/api/file";

import TextPreviewPane from "@/components/dashboard/file-system/TextPreviewPane.vue";
import { TextPreviewTruncation } from "@/enums";

const preview = (
  lines: TextPreviewLine[],
  overrides: Partial<TextPreviewDto> = {},
): TextPreviewDto => ({
  bytesShown: lines.reduce((total, line) => total + line.text.length, 0),
  domElementCount: lines.reduce((total, line) => total + line.tokens.length + 1, 0),
  encoding: "utf-8",
  highlightingLimited: false,
  language: "javascript",
  lineCount: lines.length,
  lines,
  tokenCount: lines.reduce((total, line) => total + line.tokens.length, 0),
  totalBytes: null,
  truncated: false,
  truncation: TextPreviewTruncation.None,
  ...overrides,
});

const mountPane = (data: TextPreviewDto) =>
  mount(TextPreviewPane, {
    global: {
      stubs: {
        Button: {
          props: ["label"],
          template: "<button><slot />{{ label }}</button>",
        },
        UButton: {
          props: ["label"],
          template: "<button><slot />{{ label }}</button>",
        },
      },
    },
    props: { preview: data },
  });

describe("TextPreviewPane", () => {
  it("renders token spans without injecting markup", () => {
    const wrapper = mountPane(
      preview([
        {
          text: "<b>const</b>\n",
          tokens: [{ kind: "kw", length: 5, start: 3 }],
        },
      ]),
    );

    expect(wrapper.find(".kw").text()).toBe("const");
    expect(wrapper.find("b").exists()).toBe(false);
    expect(wrapper.findAll(".l")).toHaveLength(1);
  });

  it("renders payloads above the old element budget without a fallback", () => {
    const line = `${"a".repeat(10_001)}\n`;
    const tokens = Array.from({ length: 10_001 }, (_, index) => ({
      kind: "kw",
      length: 1,
      start: index,
    }));

    const wrapper = mountPane(preview([{ text: line, tokens }]));

    expect(wrapper.findAll(".l")).toHaveLength(1);
    expect(wrapper.findAll(".kw")).toHaveLength(10_001);
    expect(wrapper.text()).not.toContain("too large to display safely");
  });

  it("offers a download when the preview is truncated", async () => {
    const wrapper = mountPane(
      preview([{ text: "a\n", tokens: [] }], {
        lineCount: 1,
        truncated: true,
        truncation: TextPreviewTruncation.Lines,
      }),
    );

    const downloadButton = wrapper
      .findAll("button")
      .find((button) => button.text().includes("Download full file"));

    expect(downloadButton).toBeTruthy();
    await downloadButton!.trigger("click");
    expect(wrapper.emitted("download")).toHaveLength(1);
  });
});
