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
    <g class="gdv-spin">
      <path
        d="M6 1h4v4h-4zM4 2h2v1h-2zM10 2h2v5h-2zM3 3h2v1h-2zM12 3h1v6h-1zM2 4h2v1h-2zM5 4h1v3h-1zM13 4h1v8h-1zM2 5h1v7h-1zM4 5h1v9h-1zM6 5h1v1h-1zM9 5h1v1h-1zM1 6h1v4h-1zM14 6h1v4h-1zM3 7h1v6h-1zM11 7h1v4h-1zM5 9h1v5h-1zM10 9h1v3h-1zM6 10h1v5h-1zM9 10h1v5h-1zM7 11h2v4h-2zM12 11h1v2h-1zM11 12h1v2h-1zM10 13h1v1h-1z"
      />
      <path
        d="M7 5h2v2h-2zM6 6h1v4h-1zM9 6h1v4h-1zM5 7h1v2h-1zM10 7h1v2h-1zM7 9h2v2h-2z"
        fill-opacity=".6"
      />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #667096;
}
svg[data-color-mode="1"] .gdv-spin path:nth-child(2) {
  fill: #c690a2;
  fill-opacity: 1;
}

.gdv-spin {
  transform-origin: 8px 8px;
  transform-box: view-box;
  animation: gdv-rot 1.6s steps(4, end) infinite;
}

@keyframes gdv-rot {
  from {
    transform: rotate(0);
  }

  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdv-spin {
    animation: none !important;
  }
}
</style>
