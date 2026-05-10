<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { refDebounced } from '@vueuse/core'
import {
  DialogClose,
  DialogContent,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { Edit, KeyRound, Search, Trash2, Upload, X } from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { AccountDetails, AccountSearchRequest, UpdateAccountRequest } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import AccountAvatarCropDialog from '../components/AccountAvatarCropDialog.vue'
import {
  useAccount,
  useAccountRoles,
  useAccountsSearch,
  useClearAccountAvatar,
  useDeleteAccount,
  useResetAccountPassword,
  useUpdateAccount,
  useUpdateAccountRoles,
} from '../queries'

const DEFAULT_PAGE_SIZE = 25
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const

const query = ref('')
const queryDebounced = refDebounced(query, 300)
const page = ref(1)
const pageSize = ref(DEFAULT_PAGE_SIZE)
const selectedUserId = ref<number | null>(null)
const dialogOpen = ref(false)
const cropOpen = ref(false)
const cropSource = ref<File | null>(null)
const avatarInputRef = ref<HTMLInputElement | null>(null)
const temporaryPassword = ref('')
const formError = ref('')

const form = reactive({
  displayName: '',
  password: '',
  discordId: '',
  email: '',
  publicEmail: true,
  discord: '',
  youtube: '',
  twitter: '',
  castingCallClub: '',
  bio: '',
  lore: '',
  roleIds: [] as number[],
})

const searchParams = computed<AccountSearchRequest>(() => ({
  query: queryDebounced.value || undefined,
  page: page.value,
  pageSize: pageSize.value,
}))

const { data: roles, isError: rolesIsError, error: rolesError } = useAccountRoles()
const {
  data: searchData,
  isLoading: searchLoading,
  isFetching: searchFetching,
  isError: searchIsError,
  error: searchError,
} = useAccountsSearch(searchParams)
const { data: selectedAccount, isLoading: detailLoading } = useAccount(selectedUserId)

const updateAccountMutation = useUpdateAccount()
const updateRolesMutation = useUpdateAccountRoles()
const clearAvatarMutation = useClearAccountAvatar()
const resetPasswordMutation = useResetAccountPassword()
const deleteAccountMutation = useDeleteAccount()

const results = computed(() => searchData.value?.results ?? [])
const total = computed(() => searchData.value?.total ?? 0)
const pageCount = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

watch([queryDebounced, pageSize], () => {
  page.value = 1
})

watch(selectedAccount, (account) => {
  if (!account) return
  fillForm(account)
})

function fillForm(account: AccountDetails) {
  form.displayName = account.displayName
  form.password = ''
  form.discordId = account.discordId ?? ''
  form.email = account.email ?? ''
  form.publicEmail = account.publicEmail
  form.discord = account.discord ?? ''
  form.youtube = account.youtube ?? ''
  form.twitter = account.twitter ?? ''
  form.castingCallClub = account.castingCallClub ?? ''
  form.bio = account.bio ?? ''
  form.lore = account.lore ?? ''
  form.roleIds = [...account.roleIds]
  temporaryPassword.value = ''
  formError.value = ''
}

function openAccount(userId: number) {
  selectedUserId.value = userId
  dialogOpen.value = true
}

function closeDialog(value: boolean) {
  dialogOpen.value = value
  if (!value) {
    selectedUserId.value = null
    temporaryPassword.value = ''
    formError.value = ''
  }
}

function optional(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : undefined
}

function requestFromForm(): UpdateAccountRequest {
  return {
    displayName: form.displayName,
    password: optional(form.password),
    discordId: optional(form.discordId),
    email: optional(form.email),
    publicEmail: form.publicEmail,
    discord: optional(form.discord),
    youtube: optional(form.youtube),
    twitter: optional(form.twitter),
    castingCallClub: optional(form.castingCallClub),
    bio: optional(form.bio),
    lore: optional(form.lore),
  }
}

async function saveProfile() {
  if (!selectedUserId.value) return
  formError.value = ''
  try {
    await updateAccountMutation.mutateAsync({
      userId: selectedUserId.value,
      request: requestFromForm(),
    })
    toast.success('Account profile saved.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

async function saveRoles() {
  if (!selectedUserId.value) return
  formError.value = ''
  try {
    await updateRolesMutation.mutateAsync({
      userId: selectedUserId.value,
      request: { roleIds: form.roleIds },
    })
    toast.success('Account roles saved.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

async function clearAvatar() {
  if (!selectedUserId.value || !confirm('Clear this account avatar?')) return
  formError.value = ''
  try {
    await clearAvatarMutation.mutateAsync(selectedUserId.value)
    toast.success('Avatar cleared.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

async function resetPassword() {
  if (!selectedUserId.value || !confirm('Generate a temporary password for this account?')) return
  formError.value = ''
  try {
    const response = await resetPasswordMutation.mutateAsync(selectedUserId.value)
    temporaryPassword.value = response.temporaryPassword
    toast.success('Temporary password generated.')
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

async function deleteAccount() {
  if (!selectedUserId.value || !confirm('Hard-delete this account? This cannot be undone.')) return
  formError.value = ''
  try {
    await deleteAccountMutation.mutateAsync(selectedUserId.value)
    toast.success('Account deleted.')
    closeDialog(false)
  } catch (err) {
    formError.value = messageFromContentError(err)
  }
}

function onAvatarError(event: Event, fallback: string) {
  const img = event.target as HTMLImageElement
  if (img.src !== fallback) img.src = fallback
}

function onAvatarPicked(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  input.value = ''
  if (!file) return
  cropSource.value = file
  cropOpen.value = true
}

function toggleRole(roleId: number, checked: boolean) {
  if (checked && !form.roleIds.includes(roleId)) {
    form.roleIds = [...form.roleIds, roleId]
  } else if (!checked) {
    form.roleIds = form.roleIds.filter((id) => id !== roleId)
  }
}
</script>

<template>
  <div class="mx-auto max-w-screen-xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Accounts</h1>
      <p class="text-sm text-muted-foreground">
        Search users, edit profile fields, manage roles, and handle account actions.
      </p>
    </header>

    <div class="grid gap-4 lg:grid-cols-[1fr_20rem]">
      <section class="space-y-4">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div class="relative min-w-0 flex-1">
            <Search class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              v-model="query"
              class="pl-9"
              placeholder="Search by name, email, social, or ID"
              autocomplete="off"
            />
          </div>
          <select v-model.number="pageSize" class="h-9 rounded-md border bg-background px-3 text-sm">
            <option v-for="size in PAGE_SIZE_OPTIONS" :key="size" :value="size">
              {{ size }} per page
            </option>
          </select>
        </div>

        <div
          v-if="searchIsError"
          class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ messageFromContentError(searchError) }}
        </div>

        <div class="overflow-hidden rounded-md border">
          <table class="w-full text-sm">
            <thead class="bg-muted/50 text-left">
              <tr>
                <th class="w-14 px-3 py-2 font-medium">Avatar</th>
                <th class="px-3 py-2 font-medium">User</th>
                <th class="hidden px-3 py-2 font-medium md:table-cell">Summary</th>
                <th class="w-24 px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="searchLoading">
                <td colspan="4" class="px-3 py-8 text-center text-muted-foreground">Loading accounts...</td>
              </tr>
              <tr v-else-if="results.length === 0">
                <td colspan="4" class="px-3 py-8 text-center text-muted-foreground">No accounts found.</td>
              </tr>
              <tr v-for="account in results" v-else :key="account.userId" class="border-t">
                <td class="px-3 py-2">
                  <img
                    :src="account.avatarUrl"
                    alt=""
                    class="size-10 rounded-md border bg-muted object-cover"
                    @error="onAvatarError($event, account.defaultAvatarUrl)"
                  />
                </td>
                <td class="px-3 py-2">
                  <div class="font-medium">{{ account.displayName }}</div>
                  <div class="text-xs text-muted-foreground">#{{ account.userId }}</div>
                </td>
                <td class="hidden max-w-md truncate px-3 py-2 text-muted-foreground md:table-cell">
                  {{ account.socialSummary || 'No profile links' }}
                </td>
                <td class="px-3 py-2 text-right">
                  <Button variant="outline" size="sm" class="gap-2" @click="openAccount(account.userId)">
                    <Edit class="size-4" />
                    Manage
                  </Button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="flex items-center justify-between gap-3 text-sm">
          <span class="text-muted-foreground">
            {{ total }} result{{ total === 1 ? '' : 's' }}
            <span v-if="searchFetching"> · Refreshing</span>
          </span>
          <div class="flex items-center gap-2">
            <Button variant="outline" size="sm" :disabled="page <= 1" @click="page--">Previous</Button>
            <span class="text-muted-foreground">Page {{ page }} of {{ pageCount }}</span>
            <Button variant="outline" size="sm" :disabled="page >= pageCount" @click="page++">Next</Button>
          </div>
        </div>
      </section>

      <aside class="space-y-3">
        <h2 class="text-sm font-medium">Discord roles</h2>
        <div
          v-if="rolesIsError"
          class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ messageFromContentError(rolesError) }}
        </div>
        <div class="space-y-2 rounded-md border p-3">
          <div
            v-for="role in roles ?? []"
            :key="role.id"
            class="flex items-center justify-between gap-3 text-sm"
          >
            <span class="flex items-center gap-2">
              <span class="size-3 rounded-sm" :style="{ backgroundColor: `#${role.color}` }" />
              {{ role.name }}
            </span>
            <span class="text-xs text-muted-foreground">{{ role.weight }}</span>
          </div>
        </div>
      </aside>
    </div>

    <DialogRoot :open="dialogOpen" @update:open="closeDialog">
      <DialogPortal>
        <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
        <DialogContent
          class="fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-6xl -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
        >
          <div class="mb-4 flex items-start justify-between gap-4">
            <DialogTitle class="text-lg font-semibold">
              {{ selectedAccount?.displayName ?? 'Account' }}
            </DialogTitle>
            <DialogClose
              aria-label="Close"
              class="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            >
              <X class="size-4" />
            </DialogClose>
          </div>

          <div v-if="detailLoading" class="py-12 text-center text-sm text-muted-foreground">
            Loading account...
          </div>

          <div v-else-if="selectedAccount" class="space-y-5">
            <div
              v-if="formError"
              class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
            >
              {{ formError }}
            </div>

            <div class="grid gap-5 md:grid-cols-[11rem_1fr]">
              <div class="space-y-3">
                <img
                  :key="selectedAccount.avatarUrl"
                  :src="selectedAccount.avatarUrl"
                  alt="Profile picture"
                  class="size-40 rounded-md border bg-muted object-cover md:size-44"
                  @error="onAvatarError($event, selectedAccount.defaultAvatarUrl)"
                />
                <Button
                  variant="outline"
                  class="w-full gap-2"
                  @click="avatarInputRef?.click()"
                >
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
              </div>

              <div class="grid gap-4 sm:grid-cols-2">
                <div class="space-y-2">
                  <Label for="display-name">Display name</Label>
                  <Input id="display-name" v-model="form.displayName" maxlength="31" />
                </div>
                <div class="space-y-2">
                  <Label for="password">New password</Label>
                  <Input id="password" v-model="form.password" type="password" autocomplete="new-password" />
                </div>
                <div class="space-y-2">
                  <Label for="email">Email</Label>
                  <Input id="email" v-model="form.email" maxlength="255" />
                </div>
                <div class="space-y-2">
                  <Label for="discord-id">Discord ID</Label>
                  <Input id="discord-id" v-model="form.discordId" maxlength="19" inputmode="numeric" />
                </div>
                <label class="flex items-end gap-2 pb-2 text-sm">
                  <input v-model="form.publicEmail" type="checkbox" class="size-4" />
                  Public email
                </label>
                <div class="space-y-2">
                  <Label for="discord">Discord</Label>
                  <Input id="discord" v-model="form.discord" maxlength="37" />
                </div>
                <div class="space-y-2">
                  <Label for="youtube">YouTube</Label>
                  <Input id="youtube" v-model="form.youtube" maxlength="56" />
                </div>
                <div class="space-y-2">
                  <Label for="twitter">Twitter</Label>
                  <Input id="twitter" v-model="form.twitter" maxlength="15" />
                </div>
                <div class="space-y-2">
                  <Label for="ccc">Casting Call Club</Label>
                  <Input id="ccc" v-model="form.castingCallClub" maxlength="64" />
                </div>
                <div class="space-y-2 sm:col-span-2">
                  <Label for="lore">Lore</Label>
                  <Input id="lore" v-model="form.lore" maxlength="63" />
                </div>
                <div class="space-y-2 sm:col-span-2">
                  <Label for="bio">Bio</Label>
                  <textarea
                    id="bio"
                    v-model="form.bio"
                    class="border-input min-h-32 w-full rounded-md border bg-transparent px-3 py-2 text-sm"
                  />
                </div>
              </div>
            </div>

            <div class="flex justify-end">
              <Button
                class="gap-2"
                :disabled="updateAccountMutation.isPending.value"
                @click="saveProfile"
              >
                <Edit class="size-4" />
                Save profile
              </Button>
            </div>

            <div class="space-y-3 border-t pt-5">
              <h3 class="text-sm font-medium">Roles</h3>
              <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                <label
                  v-for="role in roles ?? []"
                  :key="role.id"
                  class="flex items-center gap-2 rounded-md border px-3 py-2 text-sm"
                >
                  <input
                    type="checkbox"
                    class="size-4"
                    :checked="form.roleIds.includes(role.id)"
                    @change="toggleRole(role.id, ($event.target as HTMLInputElement).checked)"
                  />
                  <span class="size-3 rounded-sm" :style="{ backgroundColor: `#${role.color}` }" />
                  <span>{{ role.name }}</span>
                </label>
              </div>
              <div class="flex justify-end">
                <Button
                  variant="outline"
                  :disabled="updateRolesMutation.isPending.value"
                  @click="saveRoles"
                >
                  Save roles
                </Button>
              </div>
            </div>

            <div class="flex flex-col gap-3 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
              <div v-if="temporaryPassword" class="rounded-md border bg-muted/40 px-3 py-2 text-sm">
                Temporary password:
                <code class="font-mono">{{ temporaryPassword }}</code>
              </div>
              <div v-else-if="selectedAccount.systemAdmin" class="text-sm text-muted-foreground">
                Password reset is blocked for system-admin accounts.
              </div>
              <div v-else />
              <div class="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  class="gap-2"
                  :disabled="selectedAccount.systemAdmin || resetPasswordMutation.isPending.value"
                  @click="resetPassword"
                >
                  <KeyRound class="size-4" />
                  Reset password
                </Button>
                <Button
                  variant="destructive"
                  class="gap-2"
                  :disabled="deleteAccountMutation.isPending.value"
                  @click="deleteAccount"
                >
                  <Trash2 class="size-4" />
                  Delete account
                </Button>
              </div>
            </div>
          </div>
        </DialogContent>
      </DialogPortal>
    </DialogRoot>

    <AccountAvatarCropDialog
      v-model:open="cropOpen"
      :source="cropSource"
      :user-id="selectedUserId"
    />
  </div>
</template>
