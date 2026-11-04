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
    <g class="gdc-body">
      <path
        d="M3 1h1v3h-1zM4 2h1v6h-1zM5 3h3v5h-3zM2 4h1v1h-1zM3 5h1v3h-1zM2 6h1v1h-1zM8 8h2v2h-2zM4 9h4v1h-4zM10 9h2v1h-2zM4 10h2v4h-2zM8 10h1v1h-1zM12 10h1v4h-1zM3 13h1v1h-1zM6 13h1v1h-1zM8 13h4v1h-4z"
      />
      <path
        d="M7 1h1v2h-1zM6 2h1v1h-1zM2 5h1v1h-1zM4 8h4v1h-4zM9 10h3v3h-3zM8 11h1v2h-1z"
        fill-opacity=".6"
      />
      <path d="M6 10h2v3h-2z" fill-opacity=".3" />
    </g>
    <g class="gdc-ta">
      <path d="M13 7h1v1h-1zM14 8h1v5h-1zM13 12h1v1h-1z" />
    </g>
    <g class="gdc-tb" opacity="0">
      <path d="M15 7h1v2h-1zM14 9h1v4h-1zM13 12h1v1h-1z" />
    </g>
    <g class="gdc-lids" opacity="0">
      <path d="M3 4h1v1h-1z" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #967355;
}
svg[data-color-mode="1"] .gdc-body path:nth-child(2) {
  fill: #d2b38a;
  fill-opacity: 1;
}
svg[data-color-mode="1"] .gdc-body path:nth-child(3) {
  fill: #e8d6b7;
  fill-opacity: 1;
}

.gdc-ta {
  animation: gdc-a 1.4s steps(1, end) infinite;
}

.gdc-tb {
  animation: gdc-b 1.4s steps(1, end) infinite;
}

.gdc-lids {
  animation: gdc-blink 4s steps(1, end) infinite;
}

@keyframes gdc-a {
  0% {
    opacity: 1;
  }

  50% {
    opacity: 0;
  }
}

@keyframes gdc-b {
  0% {
    opacity: 0;
  }

  50% {
    opacity: 1;
  }
}

@keyframes gdc-blink {
  0%,
  90% {
    opacity: 0;
  }

  91%,
  95% {
    opacity: 1;
  }

  96%,
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdc-ta,
  .gdc-tb,
  .gdc-lids {
    animation: none !important;
  }
}
</style>
