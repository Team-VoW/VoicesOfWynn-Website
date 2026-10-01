import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick, ref } from 'vue'
import { useAuditionMarks } from './useAuditionMarks'

beforeEach(() => {
  const entries = new Map<string, string>()
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => { entries.set(key, value) },
  })
})

describe('audition marks', () => {
  it('persists marks and keeps users and rounds separate', async () => {
    const user = ref<number | null>(7)
    const round = ref<number | null>(10)
    const { marks, toggle } = useAuditionMarks(user, round)

    toggle(3, 11)
    expect(useAuditionMarks(user, round).marks.value).toEqual([{ characterId: 3, auditionId: 11 }])

    round.value = 12
    await nextTick()
    expect(marks.value).toEqual([])
    toggle(4, 22)

    user.value = 8
    await nextTick()
    expect(marks.value).toEqual([])
    user.value = 7
    round.value = 10
    await nextTick()
    expect(marks.value).toEqual([{ characterId: 3, auditionId: 11 }])
    toggle(3, 11)
    expect(marks.value).toEqual([])
  })
})
