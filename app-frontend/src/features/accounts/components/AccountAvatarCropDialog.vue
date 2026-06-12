<script setup lang="ts">
import { ref, useTemplateRef, watch } from 'vue'
import { Check, X } from 'lucide-vue-next'
import {
  DialogClose,
  DialogContent,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { Cropper, type CropperResult } from 'vue-advanced-cropper'
import 'vue-advanced-cropper/dist/style.css'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useUploadSelfAvatar } from '@/features/profile/queries'
import { useUploadAccountAvatar } from '../queries'

const props = defineProps<{
  userId: number | null
  open: boolean
  source: File | null
  self?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  uploaded: []
}>()

const uploadMutation = useUploadAccountAvatar()
const uploadSelfMutation = useUploadSelfAvatar()
const cropperRef = useTemplateRef<InstanceType<typeof Cropper>>('cropperRef')
const sourceUrl = ref<string | null>(null)
const error = ref('')

watch(
  () => [props.open, props.source] as const,
  ([open, source], _old, onCleanup) => {
    error.value = ''
    if (!open || !source) {
      sourceUrl.value = null
      return
    }
    const url = URL.createObjectURL(source)
    sourceUrl.value = url
    onCleanup(() => URL.revokeObjectURL(url))
  },
  { immediate: true },
)

function setOpen(value: boolean) {
  emit('update:open', value)
}

async function save() {
  if ((!props.self && !props.userId) || !cropperRef.value) return
  error.value = ''

  const result = cropperRef.value.getResult() as CropperResult
  const canvas = result?.canvas
  if (!canvas) {
    error.value = 'Could not extract crop. Try re-selecting the image.'
    return
  }

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob((b) => resolve(b), 'image/webp', 0.9),
  )
  if (!blob) {
    error.value = 'Failed to encode the cropped image.'
    return
  }

  try {
    if (props.self) {
      await uploadSelfMutation.mutateAsync(blob)
    } else {
      await uploadMutation.mutateAsync({ userId: props.userId!, file: blob })
    }
    toast.success('Avatar updated.')
    emit('uploaded')
    setOpen(false)
  } catch (err) {
    error.value = messageFromContentError(err)
  }
}
</script>

<template>
  <DialogRoot :open="open" @update:open="setOpen">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-xl -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
      >
        <div class="mb-4 flex items-start justify-between gap-4">
          <DialogTitle class="text-lg font-semibold">Crop avatar</DialogTitle>
          <DialogClose
            aria-label="Close"
            class="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          >
            <X class="size-4" />
          </DialogClose>
        </div>

        <div
          v-if="error"
          class="mb-4 rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ error }}
        </div>

        <div class="mb-4 overflow-hidden rounded-md border bg-muted">
          <Cropper
            v-if="sourceUrl"
            ref="cropperRef"
            class="h-80 w-full bg-black"
            :src="sourceUrl"
            :stencil-props="{ aspectRatio: 1 }"
            image-restriction="fit-area"
          />
          <div v-else class="flex h-80 items-center justify-center text-sm text-muted-foreground">
            No image selected.
          </div>
        </div>

        <div class="flex justify-end gap-2">
          <DialogClose as-child>
            <Button variant="outline">Cancel</Button>
          </DialogClose>
          <Button
            class="gap-2"
            :disabled="!sourceUrl || uploadMutation.isPending.value || uploadSelfMutation.isPending.value"
            @click="save"
          >
            <Check class="size-4" />
            Save avatar
          </Button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
