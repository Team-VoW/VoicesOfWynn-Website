<script setup lang="ts">
import { AccordionContent, AccordionHeader, AccordionItem, AccordionTrigger } from 'reka-ui'
import { ChevronDown } from 'lucide-vue-next'

// The id doubles as the deep-link target: /faq#data-processing has been linked from the mod and
// from the legacy download pages for years, so these must stay stable.
defineProps<{ id: string; question: string }>()
</script>

<template>
  <AccordionItem
    :id="id"
    :value="id"
    class="scroll-mt-24 border-b border-[#2a1438]/10 last:border-b-0"
  >
    <AccordionHeader as="h3">
      <!-- font-display sits on the label, not on this flex row: its leading-trim pseudo-elements
           would otherwise become flex items of their own and push the text off the left edge. -->
      <AccordionTrigger
        class="faq-trigger flex w-full cursor-pointer items-center justify-between gap-4 py-5 text-left text-base text-[#2a1438] transition-colors hover:text-[#7b1a9b] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
      >
        <span class="font-display">{{ question }}</span>
        <ChevronDown
          class="faq-chevron size-5 shrink-0 text-[#7b1a9b] transition-transform motion-reduce:transition-none"
          aria-hidden="true"
        />
      </AccordionTrigger>
    </AccordionHeader>

    <AccordionContent class="faq-content overflow-hidden">
      <div class="faq-answer max-w-3xl pb-6 pr-4 leading-relaxed text-[#2a1438]/80">
        <slot />
      </div>
    </AccordionContent>
  </AccordionItem>
</template>

<style scoped>
.faq-trigger[data-state='open'] .faq-chevron {
  transform: rotate(180deg);
}

/* Reka publishes the measured panel height as a custom property, which is what makes a
   height animation possible at all - "auto" is not an animatable value. */
.faq-content[data-state='open'] {
  animation: faq-open 0.25s ease-out;
}

.faq-content[data-state='closed'] {
  animation: faq-close 0.2s ease-out;
}

@keyframes faq-open {
  from {
    height: 0;
  }
  to {
    height: var(--reka-accordion-content-height);
  }
}

@keyframes faq-close {
  from {
    height: var(--reka-accordion-content-height);
  }
  to {
    height: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .faq-content[data-state='open'],
  .faq-content[data-state='closed'] {
    animation: none;
  }
}

/* Answers are authored as markup in the page, so they are styled here rather than each
   paragraph and link carrying its own classes. */
.faq-answer :deep(p + p),
.faq-answer :deep(p + ol),
.faq-answer :deep(p + img) {
  margin-top: 0.75rem;
}

.faq-answer :deep(a) {
  color: #7b1a9b;
  text-underline-offset: 4px;
}

.faq-answer :deep(a:hover) {
  text-decoration: underline;
}

.faq-answer :deep(code) {
  border-radius: 0.25rem;
  background-color: rgba(163, 64, 196, 0.1);
  padding: 0.1rem 0.35rem;
  font-size: 0.9em;
}

.faq-answer :deep(ol) {
  margin-left: 1.25rem;
  list-style: decimal;
}

.faq-answer :deep(li + li) {
  margin-top: 0.35rem;
}
</style>
