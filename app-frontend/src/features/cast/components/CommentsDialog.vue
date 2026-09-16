<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import {
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { Trash2, X } from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useAuthStore } from '@/stores/auth'
import type { VoicedNpc } from '@/api/types'
import { useDeleteNpcComment, useNpcComments, usePostNpcComment } from '../queries'

const props = defineProps<{ npc: VoicedNpc | null }>()
const emit = defineEmits<{ (e: 'update:npc', value: VoicedNpc | null): void }>()

// Matches the API's cap, so an over-long comment is caught before a request is made.
const CONTENT_MAX_LENGTH = 2000

const auth = useAuthStore()
const npcId = computed(() => props.npc?.npcId ?? null)
const open = computed({
  get: () => props.npc !== null,
  set: (value) => {
    if (!value) emit('update:npc', null)
  },
})

const form = reactive({ name: '', email: '', content: '' })
const formError = ref('')

const { data, isPending, isError, error } = useNpcComments(
  npcId,
  computed(() => npcId.value !== null),
)
const postMutation = usePostNpcComment()
const deleteMutation = useDeleteNpcComment()

const comments = computed(() => data.value?.comments ?? [])
const remaining = computed(() => CONTENT_MAX_LENGTH - form.content.length)

watch(npcId, () => {
  form.name = ''
  form.email = ''
  form.content = ''
  formError.value = ''
})

async function submit() {
  if (npcId.value === null) return
  const content = form.content.trim()
  if (!content) {
    formError.value = 'A comment cannot be empty.'
    return
  }

  formError.value = ''
  try {
    await postMutation.mutateAsync({
      npcId: npcId.value,
      request: {
        // A signed-in contributor posts under their account; the API ignores these fields then.
        name: auth.isAuthenticated ? null : form.name || null,
        email: auth.isAuthenticated ? null : form.email || null,
        content,
      },
    })
    form.content = ''
    toast.success('Your comment was posted.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

async function remove(commentId: number) {
  if (npcId.value === null) return
  try {
    await deleteMutation.mutateAsync({ npcId: npcId.value, commentId })
    toast.success('Comment deleted.')
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}

function formatDate(value: string | null) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-[#1b0f2d]/60 backdrop-blur-sm" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 flex max-h-[85vh] w-[min(40rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 flex-col rounded-2xl bg-white shadow-xl focus:outline-none"
      >
        <header class="flex items-start justify-between gap-4 border-b border-[#2a1438]/10 p-5">
          <div>
            <DialogTitle class="font-display text-lg text-[#2a1438]">
              Comments on {{ npc?.npcName }}
            </DialogTitle>
            <DialogDescription class="mt-1 text-sm text-[#2a1438]/65">
              Praise the performance or suggest what could be improved.
            </DialogDescription>
          </div>
          <DialogClose
            class="flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-lg text-[#2a1438]/60 transition-colors hover:bg-[#a340c4]/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
            aria-label="Close comments"
          >
            <X class="size-5" aria-hidden="true" />
          </DialogClose>
        </header>

        <div class="min-h-0 flex-1 overflow-y-auto p-5">
          <div v-if="isPending" class="space-y-4">
            <Skeleton v-for="index in 3" :key="index" class="h-16 w-full" />
          </div>

          <p v-else-if="isError" class="text-sm text-destructive">
            The comments could not be loaded. {{ error?.message }}
          </p>

          <p v-else-if="!comments.length" class="text-sm text-[#2a1438]/65">
            Nobody has commented yet. Be the first.
          </p>

          <ul v-else class="space-y-4">
            <li
              v-for="comment in comments"
              :key="comment.commentId"
              class="flex gap-3 rounded-xl border border-[#2a1438]/8 bg-[#faf6fd] p-4"
            >
              <img
                v-if="comment.avatarUrl"
                :src="comment.avatarUrl"
                alt=""
                loading="lazy"
                class="size-9 shrink-0 rounded-full bg-white object-cover"
              />
              <div class="min-w-0 flex-1">
                <p class="flex flex-wrap items-center gap-2 text-sm">
                  <span class="font-medium text-[#2a1438]">{{ comment.authorName }}</span>
                  <span
                    v-if="comment.verified"
                    class="rounded-full bg-[#a340c4]/12 px-2 py-0.5 text-xs text-[#7b1a9b]"
                  >
                    Contributor
                  </span>
                  <span v-if="comment.createdAt" class="text-xs text-[#2a1438]/50">
                    {{ formatDate(comment.createdAt) }}
                  </span>
                </p>
                <p class="mt-1.5 whitespace-pre-line break-words text-[#2a1438]/80">
                  {{ comment.content }}
                </p>
              </div>
              <button
                v-if="comment.canDelete"
                type="button"
                class="flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg text-[#2a1438]/50 transition-colors hover:bg-destructive/10 hover:text-destructive focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
                :aria-label="`Delete the comment by ${comment.authorName}`"
                :disabled="deleteMutation.isPending.value"
                @click="remove(comment.commentId)"
              >
                <Trash2 class="size-4" aria-hidden="true" />
              </button>
            </li>
          </ul>
        </div>

        <form class="border-t border-[#2a1438]/10 p-5" @submit.prevent="submit">
          <div v-if="!auth.isAuthenticated" class="mb-3 grid gap-3 sm:grid-cols-2">
            <div>
              <Label for="comment-name">Name</Label>
              <Input
                id="comment-name"
                v-model="form.name"
                class="mt-1.5"
                maxlength="31"
                placeholder="Anonymous"
                autocomplete="nickname"
              />
            </div>
            <div>
              <Label for="comment-email">E-mail (optional)</Label>
              <Input
                id="comment-email"
                v-model="form.email"
                type="email"
                class="mt-1.5"
                maxlength="255"
                autocomplete="email"
              />
              <!-- Explains why an address is worth giving, since it is never displayed. -->
              <p class="mt-1 text-xs text-[#2a1438]/55">Only used to pick your avatar.</p>
            </div>
          </div>

          <Label for="comment-content">
            {{ auth.isAuthenticated ? `Comment as ${auth.displayName}` : 'Comment' }}
          </Label>
          <textarea
            id="comment-content"
            v-model="form.content"
            class="mt-1.5 min-h-20 w-full resize-y rounded-md border border-input bg-transparent px-3 py-2 text-base shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 md:text-sm"
            :maxlength="CONTENT_MAX_LENGTH"
            required
          />

          <p v-if="formError" class="mt-2 text-sm text-destructive">{{ formError }}</p>

          <div class="mt-3 flex items-center justify-between gap-4">
            <span class="text-xs text-[#2a1438]/55">{{ remaining }} characters left</span>
            <Button type="submit" variant="brand" :disabled="postMutation.isPending.value">
              {{ postMutation.isPending.value ? 'Posting…' : 'Post comment' }}
            </Button>
          </div>
        </form>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
