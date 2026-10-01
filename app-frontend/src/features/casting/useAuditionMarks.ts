import { ref, watch, type Ref } from 'vue'

export interface AuditionMark {
  characterId: number
  auditionId: number
}

export function useAuditionMarks(userId: Ref<number | null>, roundId: Ref<number | null>) {
  const marks = ref<AuditionMark[]>([])
  const key = () => `vow.casting.marks.${userId.value}.${roundId.value}`

  watch(
    [userId, roundId],
    () => {
      marks.value = []
      if (userId.value === null || roundId.value === null) return
      try {
        const parsed: unknown = JSON.parse(localStorage.getItem(key()) ?? '[]')
        if (Array.isArray(parsed)) {
          marks.value = parsed.filter(
            (mark): mark is AuditionMark =>
              typeof mark === 'object' &&
              mark !== null &&
              Number.isInteger(mark.characterId) &&
              Number.isInteger(mark.auditionId),
          )
        }
      } catch {
        /* Storage may be unavailable or contain old data. */
      }
    },
    { immediate: true },
  )

  function toggle(characterId: number, auditionId: number) {
    if (userId.value === null || roundId.value === null) return
    const exists = marks.value.some(
      (mark) => mark.characterId === characterId && mark.auditionId === auditionId,
    )
    marks.value = exists
      ? marks.value.filter(
          (mark) => mark.characterId !== characterId || mark.auditionId !== auditionId,
        )
      : [...marks.value, { characterId, auditionId }]
    try {
      localStorage.setItem(key(), JSON.stringify(marks.value))
    } catch {
      /* Keep working for this session. */
    }
  }

  return { marks, toggle }
}
