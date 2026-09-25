import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  castVote,
  clearCharacterVotes,
  createCastingCharacter,
  createCastingRound,
  deleteCastingComment,
  deleteCastingAudition,
  deleteCastingCharacter,
  deleteCastingRound,
  getAdminCastingRound,
  getAdminCastingRounds,
  getCastingAuditions,
  getCastingReview,
  getCastingRound,
  getMyCastingPicks,
  getOpenCastingRounds,
  importCccCasting,
  removeVote,
  setCastingRoundStatus,
  setCastingComment,
  setCastingWinner,
  setCharacterDone,
  updateCastingCharacter,
  updateCastingRound,
  uploadCastingAudition,
} from '@/api/casting'
import type {
  CastingAuditionList,
  CastingRoundStatus,
  SaveCastingCharacterRequest,
  SaveCastingRoundRequest,
} from '@/api/types'

// Voting

export function useOpenCastingRounds(enabled: MaybeRefOrGetter<boolean> = true) {
  return useQuery({
    queryKey: ['casting', 'rounds'] as const,
    queryFn: ({ signal }) => getOpenCastingRounds(signal),
    enabled: computed(() => toValue(enabled)),
  })
}

export function useCastingRound(roundId: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => ['casting', 'round', toValue(roundId)] as const),
    queryFn: ({ signal }) => getCastingRound(toValue(roundId)!, signal),
    enabled: computed(() => toValue(roundId) !== null),
  })
}

export function useMyCastingPicks(roundId: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => ['casting', 'round', toValue(roundId), 'my-votes'] as const),
    queryFn: ({ signal }) => getMyCastingPicks(toValue(roundId)!, signal),
    enabled: computed(() => toValue(roundId) !== null),
  })
}

export function useCastingAuditions(characterId: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => ['casting', 'character', toValue(characterId), 'auditions'] as const),
    queryFn: ({ signal }) => getCastingAuditions(toValue(characterId)!, signal),
    enabled: computed(() => toValue(characterId) !== null),
  })
}

/**
 * Votes update the audition list optimistically so the button flips at once; the round summary
 * and "My votes" are refetched afterwards since their counts derive from the same rows.
 */
function useOptimisticAuditionChange<TVars extends { characterId: number }>(
  mutationFn: (vars: TVars) => Promise<void>,
  apply: (list: CastingAuditionList, vars: TVars) => CastingAuditionList,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onMutate: async (vars: TVars) => {
      const key = ['casting', 'character', vars.characterId, 'auditions']
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<CastingAuditionList>(key)
      if (previous) queryClient.setQueryData(key, apply(previous, vars))
      return { previous, key }
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) queryClient.setQueryData(context.key, context.previous)
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['casting'] }),
  })
}

export function useCastVote() {
  return useOptimisticAuditionChange(
    (vars: { characterId: number; auditionId: number; comment: string | null }) =>
      castVote(vars.auditionId, vars.comment),
    (list, vars) => ({
      ...list,
      auditions: list.auditions.map((a) =>
        a.id === vars.auditionId ? { ...a, myVote: true, myComment: vars.comment?.trim() || null } : a,
      ),
    }),
  )
}

/** Withdrawing a vote keeps the comment; it only goes when deleted on its own. */
export function useRemoveVote() {
  return useOptimisticAuditionChange(
    (vars: { characterId: number; auditionId: number }) => removeVote(vars.auditionId),
    (list, vars) => ({
      ...list,
      auditions: list.auditions.map((a) => (a.id === vars.auditionId ? { ...a, myVote: false } : a)),
    }),
  )
}

export function useClearCharacterVotes() {
  return useOptimisticAuditionChange(
    (vars: { characterId: number }) => clearCharacterVotes(vars.characterId),
    (list) => ({
      ...list,
      auditions: list.auditions.map((a) => ({ ...a, myVote: false })),
    }),
  )
}

/** A comment on its own, on an audition the voter may or may not have picked. */
export function useSetComment() {
  return useOptimisticAuditionChange(
    (vars: { characterId: number; auditionId: number; comment: string }) =>
      setCastingComment(vars.auditionId, vars.comment),
    (list, vars) => ({
      ...list,
      auditions: list.auditions.map((a) =>
        a.id === vars.auditionId ? { ...a, myComment: vars.comment.trim() || null } : a,
      ),
    }),
  )
}

export function useDeleteComment() {
  return useOptimisticAuditionChange(
    (vars: { characterId: number; auditionId: number }) => deleteCastingComment(vars.auditionId),
    (list, vars) => ({
      ...list,
      auditions: list.auditions.map((a) => (a.id === vars.auditionId ? { ...a, myComment: null } : a)),
    }),
  )
}

export function useSetCharacterDone() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: { characterId: number; done: boolean }) => setCharacterDone(vars.characterId, vars.done),
    // Marking done reveals the other comments, so the audition list has to be refetched.
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['casting'] }),
  })
}

// Management

export function useAdminCastingRounds(includeArchived: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: computed(() => ['admin', 'casting', 'rounds', toValue(includeArchived)] as const),
    queryFn: ({ signal }) => getAdminCastingRounds(toValue(includeArchived), signal),
  })
}

export function useAdminCastingRound(roundId: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => ['admin', 'casting', 'round', toValue(roundId)] as const),
    queryFn: ({ signal }) => getAdminCastingRound(toValue(roundId)!, signal),
    enabled: computed(() => toValue(roundId) !== null),
    // While an import runs, poll so the progress message and new auditions show up.
    refetchInterval: (query) => (query.state.data?.round.importStatus === 'Running' ? 3000 : false),
  })
}

export function useCastingReview(roundId: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => ['admin', 'casting', 'round', toValue(roundId), 'review'] as const),
    queryFn: ({ signal }) => getCastingReview(toValue(roundId)!, signal),
    enabled: computed(() => toValue(roundId) !== null),
  })
}

/** Any management change can alter what voters see, so both caches are dropped. */
function useCastingAdminMutation<TVars, TResult>(mutationFn: (vars: TVars) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin', 'casting'] }),
        queryClient.invalidateQueries({ queryKey: ['casting'] }),
      ])
    },
  })
}

export function useSaveCastingRound() {
  return useCastingAdminMutation(({ id, body }: { id: number | null; body: SaveCastingRoundRequest }) =>
    id === null ? createCastingRound(body).then((r) => r.id) : updateCastingRound(id, body).then(() => id),
  )
}

export function useSetCastingRoundStatus() {
  return useCastingAdminMutation(({ roundId, status }: { roundId: number; status: CastingRoundStatus }) =>
    setCastingRoundStatus(roundId, status),
  )
}

export function useDeleteCastingRound() {
  return useCastingAdminMutation((roundId: number) => deleteCastingRound(roundId))
}

export function useImportCcc() {
  return useCastingAdminMutation(({ roundId, url }: { roundId: number; url: string }) =>
    importCccCasting(roundId, url),
  )
}

export function useSaveCastingCharacter() {
  return useCastingAdminMutation(
    ({ roundId, id, body }: { roundId: number; id: number | null; body: SaveCastingCharacterRequest }) =>
      id === null
        ? createCastingCharacter(roundId, body).then(() => undefined)
        : updateCastingCharacter(id, body),
  )
}

export function useDeleteCastingCharacter() {
  return useCastingAdminMutation((characterId: number) => deleteCastingCharacter(characterId))
}

export function useUploadCastingAudition() {
  return useCastingAdminMutation(
    ({ characterId, auditioneeName, file }: { characterId: number; auditioneeName: string; file: File }) =>
      uploadCastingAudition(characterId, auditioneeName, file),
  )
}

export function useDeleteCastingAudition() {
  return useCastingAdminMutation((auditionId: number) => deleteCastingAudition(auditionId))
}

export function useSetCastingWinner() {
  return useCastingAdminMutation(({ characterId, auditionId }: { characterId: number; auditionId: number | null }) =>
    setCastingWinner(characterId, auditionId),
  )
}
