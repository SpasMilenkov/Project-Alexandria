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
    <g class="gds-core">
      <path d="M6 4h4v8h-4zM5 5h1v6h-1zM10 5h1v6h-1zM4 6h1v4h-1zM11 6h1v4h-1z" />
    </g>
    <g class="gds-r" style="--dx: 0px; --dy: 1px; animation-delay: 0s">
      <path d="M7 0h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: 0px; --dy: -1px; animation-delay: 0s">
      <path d="M7 14h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: 1px; --dy: 0px; animation-delay: 0s">
      <path d="M0 7h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: -1px; --dy: 0px; animation-delay: 0s">
      <path d="M14 7h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: 1px; --dy: 1px; animation-delay: -0.6s">
      <path d="M2 2h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: -1px; --dy: 1px; animation-delay: -0.6s">
      <path d="M12 2h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: 1px; --dy: -1px; animation-delay: -0.6s">
      <path d="M2 12h2v2h-2z" />
    </g>
    <g class="gds-r" style="--dx: -1px; --dy: -1px; animation-delay: -0.6s">
      <path d="M12 12h2v2h-2z" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #c5a04b;
}
svg[data-color-mode="1"] .gds-core {
  fill: #e2c56b;
}

.gds-r {
  animation: gds-pulse 1.2s steps(1, end) infinite;
}

@keyframes gds-pulse {
  0% {
    transform: translate(0, 0);
  }

  50% {
    transform: translate(var(--dx), var(--dy));
  }
}

@media (prefers-reduced-motion: reduce) {
  .gds-r {
    animation: none !important;
  }
}
</style>
