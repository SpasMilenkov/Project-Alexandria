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
    <g class="gdk-mug">
      <path
        d="M2 6h8v1h-8zM2 7h1v6h-1zM9 7h1v6h-1zM3 8h6v1h-6zM10 8h3v1h-3zM4 9h5v5h-5zM12 9h1v3h-1zM3 11h1v3h-1zM10 11h2v1h-2z"
      />
      <path d="M3 7h6v1h-6zM1 14h13v1h-13z" fill-opacity=".6" />
    </g>
    <g class="gdk-st">
      <path d="M5 2h1v1h-1zM4 3h1v2h-1zM5 5h1v1h-1z" fill-opacity=".6" />
    </g>
    <g class="gdk-st" style="animation-delay: -1.2s">
      <path d="M8 2h1v1h-1zM7 3h1v2h-1zM8 5h1v1h-1z" fill-opacity=".6" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #658f9b;
}
svg[data-color-mode="1"] .gdk-mug path:nth-child(2) {
  fill: #aa8b70;
  fill-opacity: 1;
}
svg[data-color-mode="1"] .gdk-st path {
  fill: #adb9bd;
  fill-opacity: 1;
}

.gdk-st {
  animation: gdk-rise 2.4s steps(1, end) infinite;
}

@keyframes gdk-rise {
  0% {
    transform: translateY(2px);
    opacity: 0;
  }

  25% {
    transform: translateY(1px);
    opacity: 1;
  }

  50% {
    transform: translateY(0);
    opacity: 1;
  }

  75% {
    transform: translateY(-1px);
    opacity: 0.3;
  }

  100% {
    transform: translateY(-2px);
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdk-st {
    animation: none !important;
  }
}
</style>
