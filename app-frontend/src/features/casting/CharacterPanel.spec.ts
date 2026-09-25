import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { apiFetch } from '@/api/client'
import type { CastingAuditionList, CastingCharacterSummary } from '@/api/types'
import CharacterPanel from './components/CharacterPanel.vue'

vi.mock('@/api/client', () => ({ apiFetch: vi.fn<typeof apiFetch>() }))
vi.mock('vue-sonner', () => ({
  toast: { error: vi.fn<(message: string) => void>(), success: vi.fn<(message: string) => void>() },
}))
const fetchMock = vi.mocked(apiFetch)

const character: CastingCharacterSummary = {
  id: 3,
  name: 'Theorick',
  questName: 'Detlas',
  direction: 'Weary knight, late 50s.',
  auditionCount: 2,
  myPickCount: 0,
  done: false,
}

function auditions(overrides: Partial<CastingAuditionList> = {}): CastingAuditionList {
  return {
    characterId: 3,
    commentsRevealed: false,
    auditions: [
      {
        id: 11,
        number: 1,
        auditioneeName: 'VelvetVoice',
        audioUrl: 'https://blob.test/11.mp3',
        durationSeconds: 20,
        myVote: false,
        myComment: null,
        anonymousComments: [],
      },
      {
        id: 12,
        number: 2,
        auditioneeName: 'RookReads',
        audioUrl: 'https://blob.test/12.mp3',
        durationSeconds: 31,
        myVote: false,
        myComment: null,
        anonymousComments: [],
      },
    ],
    ...overrides,
  }
}

let wrapper: VueWrapper

async function start(props: Partial<{ character: CastingCharacterSummary; votingOpen: boolean }> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  wrapper = mount(CharacterPanel, {
    props: { character, votingOpen: true, ...props },
    global: { plugins: [[VueQueryPlugin, { queryClient: client }]] },
  })
  await flushPromises()
}

function button(text: string) {
  return wrapper.findAll('button').find((b) => b.text() === text)
}

beforeEach(() => {
  fetchMock.mockReset()
  fetchMock.mockImplementation(async (path) => (path.endsWith('/auditions') ? auditions() : undefined))
})

afterEach(() => wrapper?.unmount())

describe('CharacterPanel', () => {
  it('casts a vote with the comment typed into the inline editor', async () => {
    await start()

    await button('Vote')!.trigger('click')
    await wrapper.find('textarea').setValue('  Strong last line  ')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(fetchMock).toHaveBeenCalledWith('/casting/auditions/11/vote', {
      method: 'PUT',
      body: { comment: 'Strong last line' },
    })
  })

  it('shows the pick straight away, before the server answers', async () => {
    let release: () => void = () => {}
    fetchMock.mockImplementation((path, opts) =>
      opts?.method === 'PUT'
        ? new Promise((resolve) => (release = () => resolve(undefined)))
        : Promise.resolve(path.endsWith('/auditions') ? auditions() : undefined),
    )
    await start()

    await button('Vote')!.trigger('click')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('#1 VelvetVoice')
    expect(wrapper.text()).toContain('No comment added.')
    release()
  })

  it('leaves a comment on an audition without voting for it', async () => {
    await start()

    await button('Comment')!.trigger('click')
    await wrapper.find('textarea').setValue('Good energy, wrong age')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(fetchMock).toHaveBeenCalledWith('/casting/auditions/11/comment', {
      method: 'PUT',
      body: { comment: 'Good energy, wrong age' },
    })
    expect(fetchMock).not.toHaveBeenCalledWith('/casting/auditions/11/vote', expect.anything())
  })

  it('keeps the comment when the vote is removed, and deletes it only on request', async () => {
    const voted = auditions()
    voted.auditions[0] = { ...voted.auditions[0]!, myVote: true, myComment: 'Strong last line' }
    fetchMock.mockImplementation(async (path) => (path.endsWith('/auditions') ? voted : undefined))
    await start()

    await wrapper.findAll('button').find((b) => b.text().includes('Voted'))!.trigger('click')
    await flushPromises()
    expect(fetchMock).toHaveBeenCalledWith('/casting/auditions/11/vote', { method: 'DELETE' })
    expect(wrapper.text()).toContain('Strong last line')

    await button('Delete')!.trigger('click')
    await flushPromises()
    expect(fetchMock).toHaveBeenCalledWith('/casting/auditions/11/comment', { method: 'DELETE' })
  })

  it('offers "Mark character as done" below the auditions too', async () => {
    await start()

    const doneButtons = wrapper.findAll('button').filter((b) => b.text() === 'Mark character as done')
    expect(doneButtons).toHaveLength(2)

    await doneButtons[1]!.trigger('click')
    await flushPromises()
    expect(fetchMock).toHaveBeenCalledWith('/casting/characters/3/done', { method: 'PUT' })
  })

  it('hides the vote buttons once the character is marked done', async () => {
    await start({ character: { ...character, done: true } })

    expect(button('Vote')).toBeUndefined()
    expect(button('Reopen voting')).toBeDefined()
    expect(wrapper.text()).toContain('Submitted as abstain')
  })

  it('lists other voters only as anonymous comment text', async () => {
    fetchMock.mockImplementation(async (path) =>
      path.endsWith('/auditions')
        ? auditions({
            commentsRevealed: true,
            auditions: [{ ...auditions().auditions[0]!, anonymousComments: ['Clean mic'] }],
          })
        : undefined,
    )
    await start({ character: { ...character, done: true } })

    await button('1 anonymous comment')!.trigger('click')

    expect(wrapper.text()).toContain('Anonymous')
    expect(wrapper.text()).toContain('Clean mic')
  })
})
