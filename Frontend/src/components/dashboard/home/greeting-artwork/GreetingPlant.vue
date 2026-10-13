<template>
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 16 16"
    width="64"
    height="64"
    fill="currentColor"
    shape-rendering="crispEdges"
    aria-hidden="true"
    focusable="false"
  >
    <g class="gdp-pot">
      <path d="M3 10h10v1h-10z" />
      <path d="M4 11h8v3h-8zM5 14h6v1h-6z" fill-opacity=".6" />
    </g>
    <g class="gdp-1">
      <path d="M7 8h2v2h-2z" />
    </g>
    <g class="gdp-2">
      <path d="M7 6h2v2h-2z" />
    </g>
    <g class="gdp-3">
      <path d="M3 3h2v2h-2zM5 4h2v2h-2zM4 5h1v1h-1zM7 5h1v1h-1z" />
    </g>
    <g class="gdp-4">
      <path d="M11 3h2v2h-2zM9 4h2v2h-2zM8 5h1v1h-1zM11 5h1v1h-1z" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #7c9b62;
}
svg[data-color-mode="1"] .gdp-pot {
  fill: #aa7864;
}
svg[data-color-mode="1"] .gdp-pot path:nth-child(2) {
  fill: #c2977c;
  fill-opacity: 1;
}
svg[data-color-mode="1"] .gdp-4 {
  fill: #97af76;
}

.gdp-1 {
  animation: gdp-1 5s steps(1, end) infinite;
}

.gdp-2 {
  animation: gdp-2 5s steps(1, end) infinite;
}

.gdp-3 {
  animation: gdp-3 5s steps(1, end) infinite;
}

.gdp-4 {
  animation: gdp-4 5s steps(1, end) infinite;
}

@keyframes gdp-1 {
  0%,
  5% {
    opacity: 0;
  }

  6%,
  92% {
    opacity: 1;
  }

  93%,
  100% {
    opacity: 0;
  }
}

@keyframes gdp-2 {
  0%,
  20% {
    opacity: 0;
  }

  21%,
  92% {
    opacity: 1;
  }

  93%,
  100% {
    opacity: 0;
  }
}

@keyframes gdp-3 {
  0%,
  36% {
    opacity: 0;
  }

  37%,
  92% {
    opacity: 1;
  }

  93%,
  100% {
    opacity: 0;
  }
}

@keyframes gdp-4 {
  0%,
  52% {
    opacity: 0;
  }

  53%,
  92% {
    opacity: 1;
  }

  93%,
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdp-1,
  .gdp-2,
  .gdp-3,
  .gdp-4 {
    animation: none !important;
  }
}
</style>
