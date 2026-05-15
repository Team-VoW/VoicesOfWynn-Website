<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { Edit, ExternalLink, KeyRound, Upload, X } from 'lucide-vue-next'
import {
  DialogClose,
  DialogContent,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { toast } from 'vue-sonner'
import { WEBSITE_BASE_URL } from '@/api/config'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ProfileAvatar } from '@/components/ui/profile-avatar'
import AccountAvatarCropDialog from '@/features/accounts/components/AccountAvatarCropDialog.vue'
import { messageFromContentError } from '@/features/content/contentUtils'
import { ProfileLimits } from '@/lib/profileLimits'
import { useAuthStore } from '@/stores/auth'
import {
  useClearSelfAvatar,
  useSelfProfile,
  useSetSelfPassword,
  useUpdateSelfProfile,
} from '../queries'

const auth = useAuthStore()
const cropOpen = ref(false)
const cropSource = ref<File | null>(null)
const avatarInputRef = ref<HTMLInputElement | null>(null)
const isAvatarDropActive = ref(false)
const formError = ref('')
const passwordDialogOpen = ref(false)
const passwordError = ref('')
const acceptedAvatarTypes = new Set(['image/png', 'image/jpeg', 'image/webp'])

const form = reactive({
  displayName: '',
  email: '',
  publicEmail: true,
  discord: '',
  youtube: '',
  twitter: '',
  castingCallClub: '',
  bio: '',
  lore: '',
})
const hasLocalEdits = ref(false)
let suppressDirty = false
watch(
  form,
  () => {
    if (!suppressDirty) hasLocalEdits.value = true
  },
  { deep: true },
)

const passwordForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmNewPassword: '',
})

const { data: profile, isLoading, isError, error } = useSelfProfile()
const updateMutation = useUpdateSelfProfile()
const passwordMutation = useSetSelfPassword()
const clearAvatarMutation = useClearSelfAvatar()

const publicProfileUrl = computed(() =>
  profile.value ? `${WEBSITE_BASE_URL}/cast/${profile.value.userId}` : '',
)
const passwordChangeRequiresCurrentPassword = computed(
  () => profile.value?.passwordChangeRequiresCurrentPassword ?? true,
)

const canSubmitPassword = computed(
  () =>
    (!passwordChangeRequiresCurrentPassword.value || passwordForm.oldPassword.length > 0) &&
    passwordForm.newPassword.length >= ProfileLimits.passwordMin &&
    passwordForm.confirmNewPassword.length > 0 &&
    passwordForm.newPassword === passwordForm.confirmNewPassword,
)

watch(
  profile,
  (value) => {
    if (!value) return
    if (!hasLocalEdits.value) {
      suppressDirty = true
      form.displayName = value.displayName
      form.email = value.email ?? ''
      form.publicEmail = value.publicEmail
      form.discord = value.discord ?? ''
      form.youtube = value.youtube ?? ''
      form.twitter = value.twitter ?? ''
      form.castingCallClub = value.castingCallClub ?? ''
      form.bio = value.bio ?? ''
      form.lore = value.lore ?? ''
      nextTick(() => {
        suppressDirty = false
      })
    }
    auth.setForcePasswordChange(value.forcePasswordChange)
    formError.value = ''
  },
  { immediate: true },
)

function optional(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

async function saveProfile() {
  formError.value = ''
  try {
    await updateMutation.mutateAsync({
      displayName: form.displayName,
      email: optional(form.email),
      publicEmail: form.publicEmail,
      discord: optional(form.discord),
      youtube: optional(form.youtube),
      twitter: optional(form.twitter),
      castingCallClub: optional(form.castingCallClub),
      bio: optional(form.bio),
      lore: optional(form.lore),
    })
    hasLocalEdits.value = false
    toast.success('Profile saved.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

function resetPasswordForm() {
  passwordForm.oldPassword = ''
  passwordForm.newPassword = ''
  passwordForm.confirmNewPassword = ''
  passwordError.value = ''
}

function setPasswordDialogOpen(value: boolean) {
  passwordDialogOpen.value = value
  if (!value) resetPasswordForm()
}

async function setPassword() {
  passwordError.value = ''
  if (passwordForm.newPassword !== passwordForm.confirmNewPassword) {
    passwordError.value = 'New passwords do not match.'
    return
  }

  try {
    await passwordMutation.mutateAsync({
      oldPassword: passwordChangeRequiresCurrentPassword.value ? passwordForm.oldPassword : null,
      newPassword: passwordForm.newPassword,
      confirmNewPassword: passwordForm.confirmNewPassword,
    })
    auth.setForcePasswordChange(false)
    setPasswordDialogOpen(false)
    toast.success('Password updated.')
  } catch (err) {
    passwordError.value = messageFromContentError(err)
  }
}

async function clearAvatar() {
  if (!confirm('Clear your avatar?')) return
  formError.value = ''
  try {
    await clearAvatarMutation.mutateAsync()
    toast.success('Avatar cleared.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

function openAvatarCrop(file: File) {
  if (!acceptedAvatarTypes.has(file.type)) {
    formError.value = 'Use a PNG, JPEG, or WebP image for your avatar.'
    return
  }

  formError.value = ''
  cropSource.value = file
  cropOpen.value = true
}

function onAvatarPicked(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  input.value = ''
  if (!file) return
  openAvatarCrop(file)
}

function onAvatarDrop(event: DragEvent) {
  isAvatarDropActive.value = false
  const file = event.dataTransfer?.files?.[0] ?? null
  if (!file) return
  openAvatarCrop(file)
}
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6">
    <header class="space-y-1">
      <h1 class="font-display text-2xl">Profile</h1>
      <p class="text-sm text-muted-foreground">Manage your public account details and avatar.</p>
    </header>

    <div
      v-if="auth.forcePasswordChange"
      class="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900"
    >
      Password change required. Use reset password to set a new password before continuing.
    </div>

    <div
      v-if="isError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ messageFromContentError(error) }}
    </div>

    <div v-if="isLoading" class="rounded-md border p-8 text-center text-sm text-muted-foreground">
      Loading profile...
    </div>

    <section v-else-if="profile" class="overflow-hidden rounded-xl border bg-card shadow-sm">
      <div class="grid gap-6 p-6 md:grid-cols-[12rem_1fr]">
      <div class="space-y-3">
        <div
          class="relative inline-flex"
          :class="isAvatarDropActive ? 'ring-2 ring-offset-2 ring-[--brand-violet] rounded-full' : ''"
          @dragenter.prevent="isAvatarDropActive = true"
          @dragover.prevent="isAvatarDropActive = true"
          @dragleave.prevent="isAvatarDropActive = false"
          @drop.prevent="onAvatarDrop"
        >
          <ProfileAvatar
            :src="profile.avatarUrl"
            :fallback-src="profile.defaultAvatarUrl"
            size="xl"
          />
          <div
            v-if="isAvatarDropActive"
            class="pointer-events-none absolute inset-0 flex items-center justify-center rounded-full bg-background/80 px-4 text-center text-sm font-medium text-foreground"
          >
            Drop image to upload
          </div>
        </div>
        <p class="text-xs text-muted-foreground">
          Drop a PNG, JPEG, or WebP image onto your avatar.
        </p>
        <Button variant="outline" class="w-full gap-2" @click="avatarInputRef?.click()">
          <Upload class="size-4" />
          Upload
        </Button>
        <input
          ref="avatarInputRef"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          class="hidden"
          @change="onAvatarPicked"
        />
        <Button
          variant="outline"
          class="w-full"
          :disabled="clearAvatarMutation.isPending.value"
          @click="clearAvatar"
        >
          Clear avatar
        </Button>
        <Button variant="outline" class="w-full gap-2" @click="setPasswordDialogOpen(true)">
          <KeyRound class="size-4" />
          Reset password
        </Button>
        <Button
          as="a"
          variant="outline"
          class="w-full gap-2"
          :href="publicProfileUrl"
          target="_blank"
          rel="noopener"
        >
          <ExternalLink class="size-4" />
          View public profile
        </Button>
      </div>

      <form class="space-y-5" @submit.prevent="saveProfile">
        <div
          v-if="formError"
          class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ formError }}
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <div class="space-y-2">
            <Label for="profile-display-name">Display name</Label>
            <Input
              id="profile-display-name"
              v-model="form.displayName"
              :maxlength="ProfileLimits.displayName"
              required
            />
          </div>
          <div class="space-y-2">
            <Label for="profile-email">Email</Label>
            <Input
              id="profile-email"
              v-model="form.email"
              :maxlength="ProfileLimits.email"
              type="email"
            />
          </div>
          <label class="flex items-end gap-2 pb-2 text-sm">
            <input v-model="form.publicEmail" type="checkbox" class="size-4" />
            Public email
          </label>
          <div class="space-y-2">
            <Label for="profile-discord">Discord username</Label>
            <Input id="profile-discord" v-model="form.discord" :maxlength="ProfileLimits.discord" />
          </div>
          <div class="space-y-2">
            <Label for="profile-youtube">YouTube</Label>
            <Input id="profile-youtube" v-model="form.youtube" :maxlength="ProfileLimits.youtube" />
          </div>
          <div class="space-y-2">
            <Label for="profile-twitter">Twitter</Label>
            <Input id="profile-twitter" v-model="form.twitter" :maxlength="ProfileLimits.twitter" />
          </div>
          <div class="space-y-2">
            <Label for="profile-ccc">Casting Call Club</Label>
            <Input
              id="profile-ccc"
              v-model="form.castingCallClub"
              :maxlength="ProfileLimits.castingCallClub"
            />
          </div>
          <div class="space-y-2 sm:col-span-2">
            <Label for="profile-lore">Lore</Label>
            <Input id="profile-lore" v-model="form.lore" :maxlength="ProfileLimits.lore" />
          </div>
          <div class="space-y-2 sm:col-span-2">
            <Label for="profile-bio">Bio</Label>
            <textarea
              id="profile-bio"
              v-model="form.bio"
              class="border-input min-h-36 w-full rounded-md border bg-transparent px-3 py-2 text-sm"
            />
          </div>
        </div>

        <div class="flex justify-end">
          <Button
            type="submit"
            variant="brand"
            class="gap-2"
            :disabled="updateMutation.isPending.value || !form.displayName.trim()"
          >
            <Edit class="size-4" />
            Save profile
          </Button>
        </div>
      </form>
      </div>
    </section>

    <AccountAvatarCropDialog v-model:open="cropOpen" :source="cropSource" :user-id="null" self />

    <DialogRoot :open="passwordDialogOpen" @update:open="setPasswordDialogOpen">
      <DialogPortal>
        <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
        <DialogContent
          class="fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
        >
          <div class="mb-4 flex items-start justify-between gap-4">
            <DialogTitle class="font-display text-lg">Reset password</DialogTitle>
            <DialogClose
              aria-label="Close"
              class="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            >
              <X class="size-4" />
            </DialogClose>
          </div>

          <form class="space-y-4" @submit.prevent="setPassword">
            <div
              v-if="passwordError"
              class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
            >
              {{ passwordError }}
            </div>

            <div v-if="passwordChangeRequiresCurrentPassword" class="space-y-2">
              <Label for="current-password">Current password</Label>
              <Input
                id="current-password"
                v-model="passwordForm.oldPassword"
                type="password"
                autocomplete="current-password"
                required
              />
            </div>
            <div class="space-y-2">
              <Label for="new-password">New password</Label>
              <Input
                id="new-password"
                v-model="passwordForm.newPassword"
                type="password"
                autocomplete="new-password"
                :minlength="ProfileLimits.passwordMin"
                required
              />
            </div>
            <div class="space-y-2">
              <Label for="confirm-new-password">Repeat new password</Label>
              <Input
                id="confirm-new-password"
                v-model="passwordForm.confirmNewPassword"
                type="password"
                autocomplete="new-password"
                :minlength="ProfileLimits.passwordMin"
                required
              />
              <p
                v-if="
                  passwordForm.confirmNewPassword.length > 0 &&
                  passwordForm.newPassword !== passwordForm.confirmNewPassword
                "
                class="text-sm text-destructive"
              >
                New passwords do not match.
              </p>
            </div>

            <div class="flex justify-end gap-2">
              <DialogClose as-child>
                <Button variant="outline">Cancel</Button>
              </DialogClose>
              <Button
                type="submit"
                variant="brand"
                class="gap-2"
                :disabled="passwordMutation.isPending.value || !canSubmitPassword"
              >
                <KeyRound class="size-4" />
                Reset password
              </Button>
            </div>
          </form>
        </DialogContent>
      </DialogPortal>
    </DialogRoot>
  </div>
</template>
