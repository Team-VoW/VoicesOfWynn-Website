import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { apiFetchBlob } from '@/api/client'
import SeekableAudioPlayer from './SeekableAudioPlayer.vue'

vi.mock('@/api/client', () => ({ apiFetchBlob: vi.fn() }))

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('SeekableAudioPlayer', () => {
  it('amplifies above 100% through Web Audio while retaining ordinary volume control', async () => {
    vi.mocked(apiFetchBlob).mockResolvedValue(new Blob())
    const gain = { gain: { value: 1 }, connect: vi.fn() }
    vi.stubGlobal(
      'AudioContext',
      class {
        createGain() {
          return gain
        }
        createMediaElementSource() {
          return { connect: vi.fn() }
        }
        async resume() {}
        async close() {}
      },
    )
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn(() => 'blob:audition'),
    })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(function (
      this: HTMLMediaElement,
    ) {
      queueMicrotask(() => this.dispatchEvent(new Event('loadedmetadata')))
    })

    const wrapper = mount(SeekableAudioPlayer, {
      props: {
        src: 'https://blob.test/clip.mp3',
        label: 'audition',
        expandable: true,
        audioFilePath: '/casting/auditions/1/audio',
      },
    })
    await wrapper.get('button[aria-expanded]').trigger('click')
    const slider = wrapper.get('input[type="range"]')
    await slider.setValue('50')
    expect((wrapper.get('audio').element as HTMLAudioElement).volume).toBe(0.5)

    await slider.setValue('150')
    await flushPromises()
    expect(apiFetchBlob).toHaveBeenCalledWith('/casting/auditions/1/audio')
    expect(gain.gain.value).toBe(1.5)
    expect((wrapper.get('audio').element as HTMLAudioElement).volume).toBe(1)
    wrapper.unmount()
  })
})
