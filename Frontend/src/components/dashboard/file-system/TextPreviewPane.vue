<script setup lang="ts">
import { computed, ref, toRaw } from "vue";

import type { TextPreviewDto, TextPreviewLine } from "@/api/file";
import type { RenderedLine, RenderedSegment } from "@/types/preview/code-preview";

import { TextPreviewTruncation } from "@/enums";
import { formatBytes } from "@/utils/size.utils";

import TextPreviewCode from "./TextPreviewCode.vue";

const { preview } = defineProps<{ preview: TextPreviewDto | null }>();

const emit = defineEmits<{ download: [] }>();

const wrap = ref(false);
const copied = ref(false);

// The payload is immutable and can hold thousands of line and token objects. If a caller keeps it
// in a deep ref, reactive store or reactive(), reading it through the proxy allocates a proxy per
// line and per token. Reading the raw object avoids that. Tracking is unchanged: this computed
// still depends on the `preview` prop, so a new payload recomputes everything below.
const data = computed(() => (preview ? toRaw(preview) : null));

const LANGUAGE_LABELS: Record<string, string> = {
  ass: "ASS",
  bash: "Shell",
  batch: "Batch",
  bazel: "Bazel / Starlark",
  c: "C",
  cmake: "CMake",
  cpp: "C++",
  csharp: "C#",
  css: "CSS",
  csv: "CSV",
  cue: "CUE Sheet",
  dart: "Dart",
  diff: "Diff",
  dockerfile: "Dockerfile",
  fsharp: "F#",
  go: "Go",
  gradle: "Gradle",
  groovy: "Groovy",
  hcl: "Terraform / HCL",
  htaccess: "Apache config",
  ini: "INI",
  ignore: "Ignore file",
  java: "Java",
  javascript: "JavaScript",
  json: "JSON",
  json5: "JSON5",
  julia: "Julia",
  kotlin: "Kotlin",
  less: "Less",
  log: "Log",
  lrc: "LRC",
  lua: "Lua",
  m3u: "M3U / M3U8",
  makefile: "Makefile",
  markdown: "Markdown",
  nginx: "Nginx config",
  nfo: "NFO",
  nix: "Nix",
  php: "PHP",
  plaintext: "Plain text",
  powershell: "PowerShell",
  python: "Python",
  r: "R",
  ruby: "Ruby",
  rust: "Rust",
  sass: "Sass",
  scala: "Scala",
  scss: "SCSS",
  sln: "Visual Studio Solution",
  sql: "SQL",
  srt: "SubRip",
  swift: "Swift",
  tex: "LaTeX",
  toml: "TOML",
  typescript: "TypeScript",
  vb: "Visual Basic",
  vtt: "WebVTT",
  xml: "XML / HTML",
  yaml: "YAML",
};

const languageLabel = computed(() => {
  const id = data.value?.language;
  if (!id) return "Text";

  return LANGUAGE_LABELS[id] ?? id.charAt(0).toUpperCase() + id.slice(1);
});

// Width of the line-number gutter, from the widest number
const gutterDigits = computed(() => Math.max(2, String(data.value?.lineCount ?? 0).length));

const toSegments = (line: TextPreviewLine): RenderedSegment[] => {
  const segments: RenderedSegment[] = [];
  let cursor = 0;

  for (const token of line.tokens) {
    const end = token.start + token.length;
    const valid = token.length > 0 && token.start >= cursor && end <= line.text.length;

    if (valid) {
      if (token.start > cursor) {
        segments.push({ text: line.text.slice(cursor, token.start), kind: null });
      }

      segments.push({ text: line.text.slice(token.start, end), kind: token.kind });
      cursor = end;
    }
  }

  if (cursor < line.text.length) {
    segments.push({ text: line.text.slice(cursor), kind: null });
  }

  return segments;
};

// Built once per payload. The result is a plain array that nothing mutates, and it keeps the same
// identity until `preview` changes, so TextPreviewCode is skipped on every other parent update.
const renderedLines = computed<RenderedLine[]>(() => {
  if (!data.value) return [];

  return data.value.lines.map((line, index) => ({
    number: index + 1,
    segments: toSegments(line),
  }));
});

const truncationLabel = computed(() => {
  const current = data.value;
  if (!current?.truncated) return null;

  if (current.truncation === TextPreviewTruncation.Lines) {
    return `Showing the first ${current.lineCount.toLocaleString()} lines`;
  }

  const shown = formatBytes(current.bytesShown);

  return current.totalBytes
    ? `Showing the first ${shown} of ${formatBytes(current.totalBytes)}`
    : `Showing the first ${shown}`;
});

// Joined on demand: there is no reason to hold a second copy of the text for every preview.
const copy = async (): Promise<void> => {
  const text = data.value?.lines.map((line) => line.text).join("") ?? "";

  try {
    await navigator.clipboard.writeText(text);
    copied.value = true;
    setTimeout(() => (copied.value = false), 1500);
  } catch {
    // Clipboard access can be denied; nothing useful to do
  }
};
</script>

<template>
  <div
    class="code-preview frosted-glass glass-surface overflow-hidden rounded-lg border border-gray-200/70 dark:border-gray-700/70"
  >
    <div
      class="flex items-center gap-2 border-b border-gray-200/70 px-3 py-1.5 text-xs dark:border-gray-700/70"
    >
      <span class="font-medium text-gray-700 dark:text-gray-300">{{ languageLabel }}</span>
      <span v-if="data" class="tabular-nums text-gray-600 dark:text-gray-400">
        · {{ data.lineCount }} {{ data.lineCount === 1 ? "line" : "lines" }}
      </span>

      <div class="ml-auto flex items-center gap-1">
        <UButton
          icon="i-lucide-align-left"
          size="xs"
          variant="ghost"
          :color="wrap ? 'primary' : 'neutral'"
          :aria-label="wrap ? 'Disable line wrapping' : 'Wrap long lines'"
          @click="wrap = !wrap"
        />
        <UButton
          :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
          size="xs"
          variant="ghost"
          :color="copied ? 'success' : 'neutral'"
          aria-label="Copy preview"
          @click="copy"
        />
      </div>
    </div>

    <div v-if="!data" class="px-4 py-6 text-sm text-gray-600 dark:text-gray-400">
      No text preview is available.
    </div>

    <div v-else class="code-scroll" :class="{ wrap }">
      <TextPreviewCode :lines="renderedLines" :gutter-digits="gutterDigits" />
    </div>

    <div
      v-if="data && (truncationLabel || data.highlightingLimited)"
      class="flex items-center gap-2 border-t border-gray-200/70 px-3 py-1.5 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
    >
      <span v-if="truncationLabel">{{ truncationLabel }}</span>
      <span v-if="data.highlightingLimited">Syntax highlighting limited</span>
      <UButton
        class="ml-auto"
        label="Download full file"
        size="xs"
        variant="link"
        color="neutral"
        @click="emit('download')"
      />
    </div>
  </div>
</template>

<style scoped>
/* Token colors are CSS variables, so stored previews never need regenerating to restyle.
   Dark mode follows the .dark class on an ancestor (Nuxt UI). The variables inherit into
   TextPreviewCode, which owns the code block styles. */
.code-preview {
  --tok-fg: #24292f;
  --tok-kw: #cf222e;
  --tok-st: #0a3069;
  --tok-cm: #6e7781;
  --tok-nu: #0550ae;
  --tok-li: #0550ae;
  --tok-fn: #8250df;
  --tok-ty: #953800;
  --tok-ky: #116329;
  --tok-tg: #116329;
  --tok-at: #6639ba;
  --tok-mt: #9a6700;
  --tok-hd: #0550ae;
  --tok-ln: #8c959f;
  --tok-hover: rgb(0 0 0 / 0.04);
}

:is(.dark) .code-preview {
  --tok-fg: #e6edf3;
  --tok-kw: #ff7b72;
  --tok-st: #a5d6ff;
  --tok-cm: #8b949e;
  --tok-nu: #79c0ff;
  --tok-li: #79c0ff;
  --tok-fn: #d2a8ff;
  --tok-ty: #ffa657;
  --tok-ky: #7ee787;
  --tok-tg: #7ee787;
  --tok-at: #d2a8ff;
  --tok-mt: #e3b341;
  --tok-hd: #79c0ff;
  --tok-ln: #6e7681;
  --tok-hover: rgb(255 255 255 / 0.05);
}

.code-scroll {
  max-height: 24rem;
  overflow: auto;
}

/* Wrapping is a set of variables that TextPreviewCode reads, so the toggle is pure CSS */
.code-scroll.wrap {
  --code-width: auto;
  --code-white-space: pre-wrap;
  --code-overflow-wrap: anywhere;
}
</style>
