<script setup lang="ts">
defineProps<{ bio: string }>()
</script>

<template>
  <!--
    Contributor bios are rich text. The markup is sanitized when written and again when the API
    serves it, with a tag and attribute allowlist that excludes scripts and event handlers, so it
    is rendered rather than escaped. The reset below restores the element styling Tailwind's
    preflight removes, which the legacy site did with article-css-reset.css.
  -->
  <article class="bio-content" v-html="bio" />
</template>

<style scoped>
.bio-content {
  line-height: 1.6;
}

.bio-content :deep(h1),
.bio-content :deep(h2),
.bio-content :deep(h3) {
  font-weight: 700;
  margin-block: 0.83em 0.5em;
}

.bio-content :deep(h1) {
  font-size: 1.75em;
}

.bio-content :deep(h2) {
  font-size: 1.4em;
}

.bio-content :deep(h3) {
  font-size: 1.15em;
}

.bio-content :deep(p) {
  margin-block: 0.75em;
}

.bio-content :deep(ul),
.bio-content :deep(ol) {
  margin-block: 0.75em;
  padding-inline-start: 1.5em;
}

.bio-content :deep(ul) {
  list-style: disc;
}

.bio-content :deep(ol) {
  list-style: decimal;
}

.bio-content :deep(a) {
  color: #7b1a9b;
  text-decoration: underline;
  text-underline-offset: 2px;
}

.bio-content :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 0.5rem;
}

.bio-content :deep(strong) {
  font-weight: 700;
}

.bio-content :deep(em) {
  font-style: italic;
}
</style>
