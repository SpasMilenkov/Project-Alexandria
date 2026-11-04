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
    <g class="gdm-moon">
      <path
        d="M4 3h1v10h-1zM3 4h1v8h-1zM2 5h1v6h-1zM5 9h1v4h-1zM6 10h1v3h-1zM7 11h4v1h-4zM7 12h3v1h-3z"
      />
    </g>
    <g class="gdm-s">
      <path d="M12 2h1v3h-1zM11 3h1v1h-1zM13 3h1v1h-1z" />
    </g>
    <g class="gdm-s" style="animation-delay: -0.8s">
      <path d="M13 8h2v2h-2z" />
    </g>
    <g class="gdm-s" style="animation-delay: -1.6s">
      <path d="M8 2h1v1h-1z" />
    </g>
  </svg>
</template>

<style scoped>
svg[data-color-mode="1"] {
  fill: #9a91c4;
}
svg[data-color-mode="1"] .gdm-s {
  fill: #cdbb83;
}

.gdm-moon {
  animation: gdm-bob 2.4s steps(1, end) infinite;
}

.gdm-s {
  animation: gdm-twinkle 2.4s steps(1, end) infinite;
}

@keyframes gdm-bob {
  0% {
    transform: translateY(0);
  }

  50% {
    transform: translateY(1px);
  }
}

@keyframes gdm-twinkle {
  0% {
    opacity: 1;
  }

  40% {
    opacity: 0.3;
  }

  60% {
    opacity: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .gdm-moon,
  .gdm-s {
    animation: none !important;
  }
}
</style>
