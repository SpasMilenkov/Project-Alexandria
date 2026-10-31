<script lang="ts">
import { type PropType, defineComponent, h } from "vue";

import type { RenderedLine } from "@/types/preview/code-preview";

/**
 * The code block, as a leaf component with no reactive state of its own.
 *
 * - It only re-renders when `lines` or `gutterDigits` change identity, which means a new preview.
 *   Toggling the wrap or copy buttons in the parent does not touch it, because wrap is applied
 *   with CSS variables on an ancestor and is never passed down as a prop.
 * - It is a render function, not a template. A template emits a fragment (plus two empty anchor
 *   text nodes) for every `<template v-for>` and `v-else` segment, and its whitespace inside <pre>
 *   depends on how a formatter lays out the source. This renders exactly one element per line,
 *   one element per token and one text node per plain run, and nothing else.
 * - Children are unkeyed, so updates patch in place instead of building a key map.
 *
 * Do not use v-once here: this component instance is reused when the drawer switches files, so a
 * once-rendered tree would show the previous file's text.
 */
export default defineComponent({
  name: "TextPreviewCode",
  props: {
    lines: { type: Array as PropType<RenderedLine[]>, required: true },
    gutterDigits: { type: Number, required: true },
  },
  setup(props) {
    return () =>
      h(
        "pre",
        { class: "code", style: { "--gw": `${props.gutterDigits}ch` } },
        props.lines.map((line) =>
          h(
            "span",
            { class: "l", "data-line": line.number },
            line.segments.map((segment) =>
              segment.kind ? h("span", { class: segment.kind }, segment.text) : segment.text,
            ),
          ),
        ),
      );
  },
});
</script>

<style scoped>
/* Wrapping is driven by variables set on an ancestor (.code-scroll.wrap), so toggling it never
   needs a prop and never re-renders this component. */
.code {
  margin: 0;
  padding: 0.625rem 0;
  min-width: 100%;
  width: var(--code-width, max-content);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
  font-size: 0.78rem;
  line-height: 1.6;
  color: var(--tok-fg);
  tab-size: 4;
  white-space: var(--code-white-space, pre);
  overflow-wrap: var(--code-overflow-wrap, normal);
}

/* One block per line. The newline lives inside the line's text, so copy and paste keep line
   breaks. The negative indent keeps wrapped lines aligned with the code, not the line number.
   Offscreen lines skip layout and paint; the 1.6em estimate matches one line height, and the
   browser refines it once a line has rendered. In nowrap mode that makes the horizontal
   scrollbar approximate until long lines have been seen. */
.l {
  display: block;
  padding: 0 1rem 0 calc(var(--gw, 3ch) + 2.5ch);
  text-indent: calc(-1 * (var(--gw, 3ch) + 1.5ch));
  content-visibility: auto;
  contain-intrinsic-size: auto 1.6em;
}

.l:hover {
  background: var(--tok-hover);
}

.l::before {
  content: attr(data-line);
  display: inline-block;
  width: var(--gw, 3ch);
  margin-right: 1.5ch;
  text-align: right;
  text-indent: 0;
  color: var(--tok-ln);
  user-select: none;
}

.kw {
  color: var(--tok-kw);
}
.st {
  color: var(--tok-st);
}
.cm {
  color: var(--tok-cm);
  font-style: italic;
}
.nu {
  color: var(--tok-nu);
}
.li {
  color: var(--tok-li);
}
.fn {
  color: var(--tok-fn);
}
.ty {
  color: var(--tok-ty);
}
.ky {
  color: var(--tok-ky);
}
.tg {
  color: var(--tok-tg);
}
.at {
  color: var(--tok-at);
}
.mt {
  color: var(--tok-mt);
}
.hd {
  color: var(--tok-hd);
  font-weight: 600;
}
.bd {
  font-weight: 700;
}
</style>
