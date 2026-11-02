<script setup lang="ts">
import { computed } from "vue";

type TokenKind = "key" | "string" | "number" | "literal" | "plain";

interface Token {
  kind: TokenKind;
  text: string;
}

const { payload } = defineProps<{
  payload: Record<string, unknown>;
}>();

const TOKEN_PATTERN =
  /(?<quoted>"(?:\\.|[^"\\])*")(?<colon>\s*:)?|\b(?:true|false|null)\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/gu;

const TOKEN_CLASS: Record<TokenKind, string> = {
  key: "text-gray-900 dark:text-gray-100",
  literal: "text-violet-700 dark:text-violet-300",
  number: "text-sky-700 dark:text-sky-300",
  plain: "text-gray-600 dark:text-gray-400",
  string: "text-emerald-700 dark:text-emerald-300",
};

const classify = (match: RegExpExecArray): TokenKind => {
  const { quoted, colon } = match.groups ?? {};

  if (quoted !== undefined) return colon === undefined ? "string" : "key";
  if (/^[-\d]/u.test(match[0])) return "number";

  return "literal";
};

// Rendered as plain spans rather than v-html, so payload content can never inject markup.
const tokens = computed<Token[]>(() => {
  const source = JSON.stringify(payload, null, 2);
  const result: Token[] = [];
  let cursor = 0;

  for (const match of source.matchAll(TOKEN_PATTERN)) {
    const start = match.index;

    if (start > cursor) result.push({ kind: "plain", text: source.slice(cursor, start) });

    result.push({ kind: classify(match), text: match[0] });
    cursor = start + match[0].length;
  }

  if (cursor < source.length) result.push({ kind: "plain", text: source.slice(cursor) });

  return result;
});
</script>

<template>
  <pre
    class="m-0 overflow-auto rounded-lg border border-gray-200/70 bg-neutral-50 p-3 font-mono text-xs leading-relaxed dark:border-gray-700/70 dark:bg-neutral-900"
  ><span v-for="(token, index) in tokens" :key="index" :class="TOKEN_CLASS[token.kind]">{{ token.text }}</span></pre>
</template>
