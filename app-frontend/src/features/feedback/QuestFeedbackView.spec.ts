import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { apiFetch } from '@/api/client'
import QuestFeedbackView from './QuestFeedbackView.vue'

vi.mock('@/api/client', () => ({ apiFetch: vi.fn() }))
const fetchMock = vi.mocked(apiFetch)
const quest = {
  groupingKey: 'RECOVER THE PAST',
  questName: 'Recover the Past',
  unmatched: true,
  unmatchedCount: 10,
  averageScore: 2.5,
  ratingCount: 10,
  commentCount: 1,
  latestSubmission: '2026-09-05T12:00:00Z',
}
let wrapper: VueWrapper
let client: QueryClient
async function start() {
  client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  wrapper = mount(QuestFeedbackView, {
    global: { plugins: [[VueQueryPlugin, { queryClient: client }]] },
  })
  await flushPromises()
}
async function click(text: string) {
  const button = wrapper.findAll('button').find((button) => button.text() === text)
  expect(button).toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}
beforeEach(() => {
  fetchMock.mockReset()
  fetchMock.mockImplementation(async (path) =>
    path.endsWith('/detail')
      ? {
          distribution: [
            { score: 1, count: 3 },
            { score: 3, count: 7 },
          ],
          feedback: {
            items: [
              {
                score: 1,
                questName: 'Recover the Past',
                unmatched: true,
                comment: '<img src=x onerror=alert(1)>\nLess shouting',
                modVersion: 'v2.2.0',
                createdAt: '2026-09-05T12:00:00Z',
              },
            ],
            total: 26,
            page: 1,
            pageSize: 25,
          },
        }
      : { items: [quest], total: 26, page: 1, pageSize: 25 },
  )
})
afterEach(() => {
  wrapper?.unmount()
  client?.clear()
})
describe('quest feedback', () => {
  it('defaults to lowest averages with a minimum sample size of one', async () => {
    await start()
    expect(fetchMock).toHaveBeenCalledWith(
      '/admin/feedback/quests',
      expect.objectContaining({
        query: expect.objectContaining({ sort: 'lowest', minimumCount: 1, page: 1 }),
      }),
    )
    expect(wrapper.text()).toContain('2.50 / 5')
    expect(wrapper.text()).toContain('Ratings (sample size)')
    expect(wrapper.text()).toContain('Unmatched')
  })
  it('applies search, dates, sorting and minimum count, and paginates', async () => {
    await start()
    await click('Next')
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/admin/feedback/quests',
      expect.objectContaining({ query: expect.objectContaining({ page: 2 }) }),
    )
    await wrapper.get('input[type=search]').setValue('Recover')
    await wrapper.findAll('input[type=date]')[0]!.setValue('2026-09-01')
    await wrapper.findAll('input[type=date]')[1]!.setValue('2026-09-05')
    await wrapper.get('input[type=number]').setValue(5)
    await wrapper.get('select').setValue('highest')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/admin/feedback/quests',
      expect.objectContaining({
        query: {
          search: 'Recover',
          from: '2026-09-01',
          to: '2026-09-05',
          minimumCount: 5,
          sort: 'highest',
          page: 1,
        },
      }),
    )
  })
  it('shows distribution and renders comments as text; comments filter resets detail pagination', async () => {
    await start()
    await click('Recover the Past')
    expect(wrapper.text()).toContain('10 ratings in the selected date range')
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toContain('Mod v2.2.0')
    expect(wrapper.text()).toContain('Unmatched: Recover the Past')
    await click('Next feedback')
    await wrapper.get('input[type=checkbox]').setValue(true)
    await flushPromises()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/admin/feedback/quests/detail',
      expect.objectContaining({
        query: expect.objectContaining({ key: 'RECOVER THE PAST', commentsOnly: true, page: 1 }),
      }),
    )
  })
  it('has empty and error states with retry', async () => {
    fetchMock.mockRejectedValueOnce(new Error('offline'))
    await start()
    expect(wrapper.text()).toContain('Could not load quest feedback')
    fetchMock.mockResolvedValueOnce({ items: [], total: 0, page: 1, pageSize: 25 })
    await click('Retry')
    expect(wrapper.text()).toContain('No ratings match these filters')
  })
  it('shows loading while a request is pending', async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}))
    await start()
    expect(wrapper.text()).toContain('Loading quest feedback')
  })
})
