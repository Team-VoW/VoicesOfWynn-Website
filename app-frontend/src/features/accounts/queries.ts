import { computed, type ComputedRef, type Ref } from 'vue'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  clearAccountAvatar,
  deleteAccount,
  getAccount,
  getAccountRoles,
  resetAccountPassword,
  searchAccounts,
  updateAccount,
  updateAccountRoles,
  uploadAccountAvatar,
} from '@/api/accounts'
import type { AccountSearchRequest, UpdateAccountRequest, UpdateAccountRolesRequest } from '@/api/types'

export function useAccountRoles() {
  return useQuery({
    queryKey: ['accounts', 'roles'],
    queryFn: ({ signal }) => getAccountRoles(signal),
    staleTime: 60_000,
  })
}

export function useAccountsSearch(
  params: Ref<AccountSearchRequest> | ComputedRef<AccountSearchRequest>,
) {
  return useQuery({
    queryKey: computed(() => ['accounts', 'search', params.value] as const),
    queryFn: ({ signal }) => searchAccounts(params.value, signal),
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  })
}

export function useAccount(userId: Ref<number | null>) {
  return useQuery({
    queryKey: computed(() => ['accounts', 'detail', userId.value] as const),
    queryFn: ({ signal }) => getAccount(userId.value!, signal),
    enabled: computed(() => userId.value !== null),
  })
}

export function useUpdateAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, request }: { userId: number; request: UpdateAccountRequest }) =>
      updateAccount(userId, request),
    onSuccess: () => invalidateAccounts(queryClient),
  })
}

export function useUpdateAccountRoles() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, request }: { userId: number; request: UpdateAccountRolesRequest }) =>
      updateAccountRoles(userId, request),
    onSuccess: () => invalidateAccounts(queryClient),
  })
}

export function useUploadAccountAvatar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, file }: { userId: number; file: Blob }) => uploadAccountAvatar(userId, file),
    onSuccess: () => invalidateAccounts(queryClient),
  })
}

export function useClearAccountAvatar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: number) => clearAccountAvatar(userId),
    onSuccess: () => invalidateAccounts(queryClient),
  })
}

export function useResetAccountPassword() {
  return useMutation({
    mutationFn: (userId: number) => resetAccountPassword(userId),
  })
}

export function useDeleteAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: number) => deleteAccount(userId),
    onSuccess: () => invalidateAccounts(queryClient),
  })
}

function invalidateAccounts(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['accounts'] })
}
