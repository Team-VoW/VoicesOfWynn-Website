<script setup lang="ts">
// The page's recurring mark: a frozen voice line. Every section is introduced by
// it, because every section is about the same thing — lines that used to be silent.
const amplitudes = [5, 11, 19, 8, 24, 15, 7, 20, 12, 22, 9, 14, 6]
const barWidth = 3
const gap = 3
const width = amplitudes.length * (barWidth + gap) - gap
</script>

<template>
  <svg
    :viewBox="`0 0 ${width} 24`"
    :width="width"
    height="24"
    fill="currentColor"
    aria-hidden="true"
    focusable="false"
  >
    <rect
      v-for="(amplitude, index) in amplitudes"
      :key="index"
      :x="index * (barWidth + gap)"
      :y="(24 - amplitude) / 2"
      :width="barWidth"
      :height="amplitude"
      :rx="barWidth / 2"
    />
  </svg>
</template>
