<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { Plus, X } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { UpdateModReleaseRequest } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useModRelease, useUpdateModRelease } from '../queries'

const { data, isPending: isLoading } = useModRelease()
const save = useUpdateModRelease()

const form = ref<UpdateModReleaseRequest>(blank())
const error = ref('')

const isSaving = computed(() => save.isPending.value)

function blank(): UpdateModReleaseRequest {
  return {
    latestVersion: '',
    updateNotificationVersion: '',
    killSwitchVersion: '',
    downloadUrl: '',
    changelogUrl: '',
    audioBaseUrl: '',
    audioMirrorUrls: [],
  }
}

watch(
  data,
  (release) => {
    if (!release) return
    form.value = {
      latestVersion: release.latestVersion,
      updateNotificationVersion: release.updateNotificationVersion,
      killSwitchVersion: release.killSwitchVersion,
      downloadUrl: release.downloadUrl,
      changelogUrl: release.changelogUrl,
      audioBaseUrl: release.audioBaseUrl,
      audioMirrorUrls: [...release.audioMirrorUrls],
    }
  },
  { immediate: true },
)

function addMirror() {
  form.value.audioMirrorUrls.push('')
}

function removeMirror(index: number) {
  form.value.audioMirrorUrls.splice(index, 1)
}

async function submit() {
  error.value = ''
  try {
    await save.mutateAsync({
      ...form.value,
      audioMirrorUrls: form.value.audioMirrorUrls.map((url) => url.trim()).filter(Boolean),
    })
    toast.success('Mod release settings saved.')
  } catch (err) {
    error.value = messageFromContentError(err)
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="space-y-1">
      <h2 class="text-sm font-semibold">Mod release</h2>
      <p class="text-sm text-muted-foreground">
        What the mod is told on launch. A client at or below the kill switch version disables
        itself; at or below the update notification version it shows the download link.
      </p>
    </div>

    <p v-if="isLoading" class="text-sm text-muted-foreground">Loading…</p>

    <form v-else class="space-y-5" @submit.prevent="submit">
      <div class="grid gap-4 sm:grid-cols-3">
        <div class="space-y-2">
          <Label for="latest-version">Latest version</Label>
          <Input id="latest-version" v-model="form.latestVersion" placeholder="2.0.3" />
        </div>
        <div class="space-y-2">
          <Label for="notify-version">Notify at or below</Label>
          <Input id="notify-version" v-model="form.updateNotificationVersion" placeholder="2.0.2" />
        </div>
        <div class="space-y-2">
          <Label for="killswitch-version">Kill switch at or below</Label>
          <Input id="killswitch-version" v-model="form.killSwitchVersion" placeholder="0" />
        </div>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="download-url">Download link</Label>
          <Input id="download-url" v-model="form.downloadUrl" placeholder="https://…" />
        </div>
        <div class="space-y-2">
          <Label for="changelog-url">Changelog link</Label>
          <Input id="changelog-url" v-model="form.changelogUrl" placeholder="https://…" />
        </div>
      </div>

      <div class="space-y-2">
        <Label for="audio-base-url">Audio base URL</Label>
        <Input id="audio-base-url" v-model="form.audioBaseUrl" placeholder="https://…/sounds/" />
      </div>

      <div class="space-y-2">
        <Label>Audio mirrors</Label>
        <div
          v-for="(_, index) in form.audioMirrorUrls"
          :key="index"
          class="flex items-center gap-2"
        >
          <Input v-model="form.audioMirrorUrls[index]" placeholder="https://…/sounds/" />
          <Button type="button" variant="ghost" size="icon-sm" @click="removeMirror(index)">
            <X class="size-4" />
          </Button>
        </div>
        <Button type="button" variant="outline" size="sm" @click="addMirror">
          <Plus class="size-4" />
          Add mirror
        </Button>
      </div>

      <div
        v-if="error"
        class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
      >
        {{ error }}
      </div>

      <div class="flex items-center gap-3">
        <Button type="submit" :disabled="isSaving">
          {{ isSaving ? 'Saving…' : 'Save release settings' }}
        </Button>
        <p v-if="data" class="text-xs text-muted-foreground">
          Last changed {{ new Date(data.updatedAt).toLocaleString() }}
        </p>
      </div>
    </form>
  </section>
</template>
