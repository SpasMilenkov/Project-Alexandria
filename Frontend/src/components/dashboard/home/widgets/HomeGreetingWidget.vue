<script setup lang="ts">
import { useTimestamp } from "@vueuse/core";
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import HomeGreetingDecoration from "@/components/dashboard/home/HomeGreetingDecoration.vue";
import { HomeGreetingDecoration as Decoration, HomeGreetingStyle } from "@/enums/home-widget";
import { useAuthStore } from "@/stores/auth";
import { homeClockDateLabels } from "@/utils/home-clock";
import { homeGreetingHeading } from "@/utils/home-greeting";

const { widget } = defineProps<{ widget: HomeWidget }>();
const auth = useAuthStore();
const timestamp = useTimestamp({ interval: 60_000 });

const heading = computed(() =>
  homeGreetingHeading(timestamp.value, widget.options, auth.user?.user.name),
);

const date = computed(() => homeClockDateLabels(timestamp.value, undefined));

const subtitle = computed(() => widget.options.greetingSubtitle.trim());
</script>

<template>
  <div class="greeting-body relative min-h-0 min-w-0 flex-1">
    <div class="greeting-scroll absolute inset-0 overflow-auto">
      <div
        class="greeting-content"
        :class="{
          'greeting-welcome': widget.options.greetingStyle === HomeGreetingStyle.Welcome,
          'greeting-minimal': widget.options.greetingStyle === HomeGreetingStyle.Minimal,
        }"
        :data-greeting-style="widget.options.greetingStyle"
        :data-alignment="widget.options.greetingAlignment"
      >
        <div
          v-if="widget.options.greetingDecoration !== Decoration.None"
          class="greeting-art shrink-0 text-gray-600 dark:text-gray-400"
        >
          <HomeGreetingDecoration
            :decoration="widget.options.greetingDecoration"
            :color-mode="widget.options.greetingColorMode"
          />
        </div>

        <div class="greeting-copy min-w-0">
          <p
            v-if="widget.options.greetingShowDate"
            class="greeting-date text-xs font-medium text-gray-600 dark:text-gray-400"
          >
            <span class="greeting-date-full">{{ date.date }}</span>
            <span class="greeting-date-compact">{{ date.compactDate }}</span>
          </p>

          <h2
            class="greeting-heading font-semibold tracking-tight text-gray-900 dark:text-gray-100"
          >
            {{ heading }}
          </h2>

          <p v-if="subtitle" class="greeting-subtitle text-sm text-gray-600 dark:text-gray-400">
            {{ subtitle }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.greeting-body {
  container: greeting-body / size;
}

.greeting-content {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 1rem;
  min-height: 100%;
}

.greeting-copy {
  overflow-wrap: anywhere;
}

.greeting-art {
  width: clamp(2rem, min(14cqi, 32cqb), 4rem);
  height: clamp(2rem, min(14cqi, 32cqb), 4rem);
}

.greeting-heading {
  font-size: clamp(1rem, min(5cqi, 14cqb), 2rem);
  line-height: 1.2;
}

.greeting-date {
  margin-bottom: 0.5rem;
}

.greeting-date-compact {
  display: none;
}

.greeting-subtitle {
  margin-top: 0.5rem;
  line-height: 1.5;
}

.greeting-content[data-alignment="1"] {
  justify-content: center;
  text-align: center;
}

.greeting-content[data-alignment="2"] {
  justify-content: flex-end;
  text-align: right;
}

.greeting-welcome {
  flex-direction: column;
  align-items: flex-start;
  justify-content: center;
  gap: 1rem;
}

.greeting-welcome[data-alignment="1"] {
  align-items: center;
  justify-content: center;
}

.greeting-welcome[data-alignment="2"] {
  align-items: flex-end;
  justify-content: center;
}

.greeting-welcome .greeting-heading {
  font-size: clamp(1.25rem, min(8cqi, 20cqb), 4.5rem);
}

.greeting-welcome .greeting-art {
  width: clamp(2.5rem, min(24cqi, 24cqb), 8rem);
  height: clamp(2.5rem, min(24cqi, 24cqb), 8rem);
}

@container greeting-body (max-width: 240px) {
  .greeting-minimal {
    flex-direction: column;
    align-items: flex-start;
    justify-content: center;
  }

  .greeting-minimal[data-alignment="1"] {
    align-items: center;
  }

  .greeting-minimal[data-alignment="2"] {
    align-items: flex-end;
  }
}

@container greeting-body (min-aspect-ratio: 3 / 1) {
  .greeting-welcome {
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
  }

  .greeting-welcome[data-alignment="1"] {
    justify-content: center;
  }

  .greeting-welcome[data-alignment="2"] {
    justify-content: flex-end;
  }
}

@container greeting-body (max-height: 160px) {
  .greeting-content {
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
    gap: 0.5rem;
  }

  .greeting-content[data-alignment="1"] {
    align-items: center;
    justify-content: center;
  }

  .greeting-content[data-alignment="2"] {
    align-items: center;
    justify-content: flex-end;
  }

  .greeting-art,
  .greeting-welcome .greeting-art {
    width: 2rem;
    height: 2rem;
  }

  .greeting-heading,
  .greeting-welcome .greeting-heading {
    font-size: clamp(1rem, min(6cqi, 18cqb), 1.75rem);
  }

  .greeting-subtitle {
    font-size: 0.75rem;
    line-height: 1rem;
  }

  .greeting-date-full {
    display: none;
  }

  .greeting-date-compact {
    display: inline;
  }
}

@container greeting-body (max-width: 240px) and (max-height: 160px) {
  .greeting-art {
    display: none;
  }
}

@container greeting-body (max-width: 240px) and (max-height: 112px) {
  .greeting-date {
    display: none;
  }
}
</style>
