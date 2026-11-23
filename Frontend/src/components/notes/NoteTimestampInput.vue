<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef, watch } from "vue";

import type { MomentLimit } from "@/features/notes/link-picker";

import {
  type TimestampParts,
  type TimestampUnit,
  clampTimestamp,
  joinTimestamp,
  padUnit,
  splitTimestamp,
  timestampUnits,
  unitMax,
} from "@/features/notes/timestamp-fields";
import { formatPlaybackTime } from "@/utils/date-formatters";

// A start time typed segment by segment, minutes and seconds, plus hours for long recordings. It
// never holds anything but a time inside the recording. Two digits move on to the next segment,
// ":" jumps there, and the arrow keys step a segment up or down.
const { limit } = defineProps<{ limit: MomentLimit }>();

const model = defineModel<number>({ required: true });

const emit = defineEmits<{
  // Focus left the whole field.
  commit: [];
  // Enter was pressed in it.
  submit: [];
}>();

const UNIT_LABELS: Record<TimestampUnit, string> = {
  hours: "Hours",
  minutes: "Minutes",
  seconds: "Seconds",
};

const rootRef = useTemplateRef<HTMLElement>("root");
const inputs = ref<Partial<Record<TimestampUnit, HTMLInputElement>>>({});

const units = computed(() => timestampUnits(limit));

const textFor = (value: number): Record<TimestampUnit, string> => {
  const parts = splitTimestamp(clampTimestamp(value, limit), limit);

  return {
    hours: padUnit(parts.hours),
    minutes: padUnit(parts.minutes),
    seconds: padUnit(parts.seconds),
  };
};

const texts = ref<Record<TimestampUnit, string>>(textFor(model.value));
const isFocused = ref(false);

// While a segment is being typed in, the field keeps the typed digits instead of reformatting.
watch(
  () => [model.value, limit] as const,
  () => {
    if (!isFocused.value) texts.value = textFor(model.value);
  },
);

const setInput = (unit: TimestampUnit, element: unknown) => {
  if (element instanceof HTMLInputElement) inputs.value[unit] = element;
};

const partsFromTexts = (): TimestampParts => {
  const read = (unit: TimestampUnit) => Math.min(Number(texts.value[unit] || 0), unitMax(unit, limit));

  return {
    hours: units.value.includes("hours") ? read("hours") : 0,
    minutes: read("minutes"),
    seconds: read("seconds"),
  };
};

const publish = () => {
  model.value = clampTimestamp(joinTimestamp(partsFromTexts()), limit);
};

const focusUnit = async (unit: TimestampUnit | undefined) => {
  if (!unit) return;

  await nextTick();
  inputs.value[unit]?.focus();
  inputs.value[unit]?.select();
};

const neighbour = (unit: TimestampUnit, step: 1 | -1): TimestampUnit | undefined =>
  units.value[units.value.indexOf(unit) + step];

const onInput = (unit: TimestampUnit, event: Event) => {
  const element = event.target as HTMLInputElement;
  const digits = element.value.replace(/\D/gu, "").slice(0, 2);

  element.value = digits;
  texts.value[unit] = digits;
  publish();

  if (digits.length === 2) void focusUnit(neighbour(unit, 1));
};

const step = (unit: TimestampUnit, by: 1 | -1) => {
  const current = Number(texts.value[unit] || 0);
  const next = Math.min(Math.max(0, current + by), unitMax(unit, limit));

  texts.value[unit] = padUnit(next);
  publish();
};

const SEPARATOR_KEYS = new Set([":", ".", " "]);

// Which segment a key moves to: the next on a separator or past the last digit, the previous on
// Backspace in an empty segment or before the first digit.
const moveFor = (event: KeyboardEvent): 1 | -1 | null => {
  const element = event.target as HTMLInputElement;
  const atStart = element.selectionStart === 0 && element.selectionEnd === 0;
  const atEnd = element.selectionStart === element.value.length;

  if (SEPARATOR_KEYS.has(event.key)) return 1;
  if (event.key === "ArrowRight" && atEnd) return 1;
  if (event.key === "Backspace" && element.value === "") return -1;
  if (event.key === "ArrowLeft" && atStart) return -1;

  return null;
};

const onKeydown = (unit: TimestampUnit, event: KeyboardEvent) => {
  if (event.key === "ArrowUp" || event.key === "ArrowDown") {
    event.preventDefault();
    step(unit, event.key === "ArrowUp" ? 1 : -1);
    return;
  }

  if (event.key === "Enter") {
    event.preventDefault();
    publish();
    emit("submit");
    return;
  }

  const move = moveFor(event);

  if (move === null) return;

  event.preventDefault();
  void focusUnit(neighbour(unit, move));
};

const onFocusIn = (event: FocusEvent) => {
  isFocused.value = true;
  (event.target as HTMLInputElement).select();
};

// Moving between segments keeps the field focused; leaving it pads the digits and reports.
const onFocusOut = (event: FocusEvent) => {
  if (event.relatedTarget instanceof Node && rootRef.value?.contains(event.relatedTarget)) return;

  isFocused.value = false;
  publish();
  texts.value = textFor(model.value);
  emit("commit");
};

const lengthLabel = computed(() => {
  if (limit === null) return null;

  return `of ${formatPlaybackTime(limit)}`;
});
</script>

<template>
  <div class="flex items-center gap-2">
    <div
      ref="root"
      role="group"
      aria-label="Start time"
      class="inline-flex items-center rounded-md bg-default px-2 py-1 text-sm text-gray-900 ring ring-inset ring-accented focus-within:ring-2 focus-within:ring-primary dark:text-gray-100"
      @focusin="onFocusIn"
      @focusout="onFocusOut"
    >
      <template v-for="(unit, index) in units" :key="unit">
        <span v-if="index > 0" class="px-0.5 text-gray-500 dark:text-gray-500" aria-hidden="true">
          :
        </span>

        <input
          :ref="(element) => setInput(unit, element)"
          :value="texts[unit]"
          :aria-label="UNIT_LABELS[unit]"
          inputmode="numeric"
          autocomplete="off"
          maxlength="2"
          placeholder="00"
          class="w-5 bg-transparent text-center tabular-nums outline-none placeholder:text-gray-500"
          @input="onInput(unit, $event)"
          @keydown="onKeydown(unit, $event)"
        />
      </template>
    </div>

    <span v-if="lengthLabel" class="text-xs tabular-nums text-gray-500 dark:text-gray-500">
      {{ lengthLabel }}
    </span>
  </div>
</template>
