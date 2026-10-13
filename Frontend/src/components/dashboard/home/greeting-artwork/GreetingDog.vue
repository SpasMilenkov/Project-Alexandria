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
    <g class="gdd-body">
      <path
        d="M3 3h4v1h-4zM2 4h2v2h-2zM5 4h1v11h-1zM4 5h1v3h-1zM0 6h1v1h-1zM3 6h1v2h-1zM6 8h3v4h-3zM11 8h3v4h-3zM9 10h2v2h-2zM6 12h1v3h-1zM12 12h2v3h-2zM4 14h1v1h-1zM11 14h1v1h-1z"
      />
      <path
        d="M6 4h2v4h-2zM9 8h2v2h-2zM7 12h2v3h-2zM10 12h2v2h-2zM10 14h1v1h-1z"
        fill-opacity=".6"
      />
      <path d="M1 6h2v2h-2zM0 7h1v1h-1z" fill-opacity=".3" />
    </g>
    <g class="gdd-ta">
      <path d="M15 5h1v3h-1zM14 8h1v1h-1z" />
    </g>
    <g class="gdd-tb" opacity="0">
      <path d="M14 9h2v1h-2zM15 10h1v1h-1z" />
    </g>
    <g class="gdd-lids" opacity="0">
      <path d="M4 4h1v1h-1z" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #87674e;
}
svg[data-color-mode="1"] .gdd-body path:nth-child(2) {
  fill: #d3b68b;
  fill-opacity: 1;
}
svg[data-color-mode="1"] .gdd-body path:nth-child(3) {
  fill: #b58d65;
  fill-opacity: 1;
}

.gdd-ta {
  animation: gdd-a 0.6s steps(1, end) infinite;
}

.gdd-tb {
  animation: gdd-b 0.6s steps(1, end) infinite;
}

.gdd-lids {
  animation: gdd-blink 5s steps(1, end) infinite;
}

@keyframes gdd-a {
  0% {
    opacity: 1;
  }

  50% {
    opacity: 0;
  }
}

@keyframes gdd-b {
  0% {
    opacity: 0;
  }

  50% {
    opacity: 1;
  }
}

@keyframes gdd-blink {
  0%,
  88% {
    opacity: 0;
  }

  89%,
  93% {
    opacity: 1;
  }

  94%,
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdd-ta,
  .gdd-tb,
  .gdd-lids {
    animation: none !important;
  }
}
</style>
