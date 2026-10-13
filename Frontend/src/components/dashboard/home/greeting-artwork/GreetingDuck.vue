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
    <g class="gdu-duck">
      <path
        d="M6 2h3v1h-3zM5 3h1v8h-1zM7 3h3v5h-3zM6 4h1v5h-1zM14 5h1v1h-1zM4 6h1v4h-1zM10 6h4v2h-4zM3 7h1v2h-1zM11 8h3v2h-3zM6 10h1v2h-1zM10 10h3v1h-3zM7 11h5v1h-5z"
      />
      <path d="M3 4h2v2h-2zM7 8h4v2h-4zM6 9h1v1h-1zM7 10h3v1h-3z" fill-opacity=".6" />
    </g>
    <g class="gdu-duck gdu-lids" opacity="0">
      <path d="M6 3h1v1h-1z" />
    </g>
    <g class="gdu-ra">
      <path d="M1 12h3v1h-3zM5 12h3v1h-3zM9 12h3v1h-3zM13 12h2v1h-2z" fill-opacity=".3" />
    </g>
    <g class="gdu-rb" opacity="0">
      <path d="M2 13h3v1h-3zM6 13h3v1h-3zM10 13h3v1h-3zM14 13h2v1h-2z" fill-opacity=".3" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #d6b14b;
}
svg[data-color-mode="1"] .gdu-duck path:nth-child(2) {
  fill: #ead18b;
  fill-opacity: 1;
}
svg[data-color-mode="1"] .gdu-ra path,
svg[data-color-mode="1"] .gdu-rb path {
  fill: #6b9eaf;
  fill-opacity: 1;
}

.gdu-duck {
  animation: gdu-bob 1.6s steps(1, end) infinite;
}

.gdu-ra {
  animation: gdu-a 1.6s steps(1, end) infinite;
}

.gdu-rb {
  animation: gdu-b 1.6s steps(1, end) infinite;
}

.gdu-lids {
  animation: gdu-blink 5s steps(1, end) infinite;
}

@keyframes gdu-bob {
  0% {
    transform: translateY(0);
  }

  50% {
    transform: translateY(1px);
  }
}

@keyframes gdu-a {
  0% {
    opacity: 1;
  }

  50% {
    opacity: 0;
  }
}

@keyframes gdu-b {
  0% {
    opacity: 0;
  }

  50% {
    opacity: 1;
  }
}

@keyframes gdu-blink {
  0%,
  85% {
    opacity: 0;
  }

  86%,
  90% {
    opacity: 1;
  }

  91%,
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdu-duck,
  .gdu-ra,
  .gdu-rb,
  .gdu-lids {
    animation: none !important;
  }
}
</style>
