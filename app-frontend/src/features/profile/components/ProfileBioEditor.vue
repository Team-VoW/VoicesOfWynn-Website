<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import type { Component } from 'vue'
import {
  AlignCenter,
  AlignJustify,
  AlignLeft,
  AlignRight,
  Bold,
  Eraser,
  Heading1,
  Heading2,
  Heading3,
  Image,
  Italic,
  Link,
  List,
  ListOrdered,
  Redo2,
  Strikethrough,
  Underline,
  Undo2,
} from 'lucide-vue-next'

const model = defineModel<string>({ required: true })
const props = defineProps<{ id?: string }>()
defineOptions({ inheritAttrs: false })
const editorRef = ref<HTMLElement | null>(null)
let updatingFromEditor = false

type ToolbarAction =
  | { icon: Component; label: string; command: string; value?: string }
  | { icon: Component; label: string; custom: 'link' | 'image' }

const toolbarGroups: ToolbarAction[][] = [
  [
    { icon: Undo2, label: 'Undo', command: 'undo' },
    { icon: Redo2, label: 'Redo', command: 'redo' },
    { icon: Eraser, label: 'Remove formatting', command: 'removeFormat' },
  ],
  [
    { icon: Bold, label: 'Bold', command: 'bold' },
    { icon: Italic, label: 'Italic', command: 'italic' },
    { icon: Underline, label: 'Underline', command: 'underline' },
    { icon: Strikethrough, label: 'Strikethrough', command: 'strikeThrough' },
  ],
  [
    { icon: Heading1, label: 'Heading 1', command: 'formatBlock', value: 'h1' },
    { icon: Heading2, label: 'Heading 2', command: 'formatBlock', value: 'h2' },
    { icon: Heading3, label: 'Heading 3', command: 'formatBlock', value: 'h3' },
  ],
  [
    { icon: List, label: 'Bulleted list', command: 'insertUnorderedList' },
    { icon: ListOrdered, label: 'Numbered list', command: 'insertOrderedList' },
  ],
  [
    { icon: Link, label: 'Link', custom: 'link' },
    { icon: Image, label: 'Image', custom: 'image' },
  ],
  [
    { icon: AlignLeft, label: 'Align left', command: 'justifyLeft' },
    { icon: AlignCenter, label: 'Align center', command: 'justifyCenter' },
    { icon: AlignRight, label: 'Align right', command: 'justifyRight' },
    { icon: AlignJustify, label: 'Justify', command: 'justifyFull' },
  ],
]

function syncEditorContent(value: string) {
  if (editorRef.value && editorRef.value.innerHTML !== value) {
    editorRef.value.innerHTML = value
  }
}

function emitEditorContent() {
  if (!editorRef.value) return
  updatingFromEditor = true
  model.value = editorRef.value.innerHTML
  nextTick(() => {
    updatingFromEditor = false
  })
}

function runCommand(command: string, value?: string) {
  editorRef.value?.focus()
  document.execCommand(command, false, value)
  emitEditorContent()
}

function setLink() {
  const href = prompt('Link URL')
  if (!href) return
  editorRef.value?.focus()
  document.execCommand('createLink', false, href)
  const selection = window.getSelection()
  const anchor = selection?.anchorNode?.parentElement?.closest('a')
  if (anchor) {
    anchor.target = '_blank'
    anchor.rel = 'noopener noreferrer'
  }
  emitEditorContent()
}

function insertImage() {
  const src = prompt('Image URL')
  if (!src) return
  runCommand('insertImage', src)
}

function runAction(action: ToolbarAction) {
  if ('custom' in action) {
    if (action.custom === 'link') setLink()
    if (action.custom === 'image') insertImage()
    return
  }

  runCommand(action.command, action.value)
}

watch(
  model,
  (value) => {
    if (!updatingFromEditor) syncEditorContent(value)
  },
  { immediate: true },
)

onMounted(() => syncEditorContent(model.value))
</script>

<template>
  <div class="overflow-hidden rounded-md border border-input bg-background">
    <div class="flex flex-wrap gap-1 border-b bg-muted/40 p-2">
      <div
        v-for="(group, groupIndex) in toolbarGroups"
        :key="groupIndex"
        class="flex gap-1 border-r border-border pr-1 last:border-r-0 last:pr-0"
      >
        <button
          v-for="action in group"
          :key="action.label"
          type="button"
          class="inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          :aria-label="action.label"
          :title="action.label"
          @mousedown.prevent
          @click="runAction(action)"
        >
          <component :is="action.icon" class="size-4" />
        </button>
      </div>
    </div>
    <div
      :id="props.id"
      ref="editorRef"
      class="profile-bio-editor min-h-44 px-3 py-2 text-sm outline-none"
      contenteditable="true"
      role="textbox"
      aria-multiline="true"
      aria-label="Bio"
      @input="emitEditorContent"
      @blur="emitEditorContent"
    />
  </div>
</template>

<style scoped>
.profile-bio-editor :deep(h1) {
  margin: 0.6rem 0;
  font-size: 1.5rem;
  font-weight: 700;
}

.profile-bio-editor :deep(h2) {
  margin: 0.55rem 0;
  font-size: 1.25rem;
  font-weight: 700;
}

.profile-bio-editor :deep(h3) {
  margin: 0.5rem 0;
  font-size: 1.1rem;
  font-weight: 700;
}

.profile-bio-editor :deep(p) {
  margin: 0.5rem 0;
}

.profile-bio-editor :deep(ul),
.profile-bio-editor :deep(ol) {
  margin: 0.5rem 0;
  padding-left: 1.5rem;
}

.profile-bio-editor :deep(ul) {
  list-style: disc;
}

.profile-bio-editor :deep(ol) {
  list-style: decimal;
}

.profile-bio-editor :deep(a) {
  color: var(--brand-purple);
  text-decoration: underline;
}

.profile-bio-editor :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
