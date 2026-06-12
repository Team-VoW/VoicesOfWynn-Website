import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  clearSelfAvatar,
  getSelfProfile,
  setSelfPassword,
  updateSelfProfile,
  uploadSelfAvatar,
} from '@/api/profile'
import type { SetSelfPasswordRequest, UpdateSelfProfileRequest } from '@/api/types'

export function useSelfProfile() {
  return useQuery({
    queryKey: ['me'],
    queryFn: ({ signal }) => getSelfProfile(signal),
  })
}

export function useUpdateSelfProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateSelfProfileRequest) => updateSelfProfile(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me'] }),
  })
}

export function useSetSelfPassword() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: SetSelfPasswordRequest) => setSelfPassword(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me'] }),
  })
}

export function useUploadSelfAvatar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: Blob) => uploadSelfAvatar(file),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me'] }),
  })
}

export function useClearSelfAvatar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => clearSelfAvatar(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me'] }),
  })
}
