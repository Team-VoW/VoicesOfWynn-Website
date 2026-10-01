import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import AudioAnalysis from './AudioAnalysis.vue'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('AudioAnalysis', () => {
  it('tracks playback and seeks from both visualizations', async () => {
    const canvas = {
      clearRect: vi.fn(),
      fillRect: vi.fn(),
      createImageData: () => ({ data: new Uint8ClampedArray(720 * 160 * 4) }),
      putImageData: vi.fn(),
      fillStyle: '',
    }
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      canvas as unknown as CanvasRenderingContext2D,
    )
    vi.stubGlobal(
      'AudioContext',
      class {
        async decodeAudioData() {
          return { sampleRate: 44100, getChannelData: () => new Float32Array(4096) }
        }
        async close() {}
      },
    )
    vi.spyOn(Blob.prototype, 'arrayBuffer').mockResolvedValue(new ArrayBuffer(8))
    const wrapper = mount(AudioAnalysis, {
      props: { loadAudio: async () => new Blob(), position: 10, duration: 40 },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Spectrogram')
    expect(wrapper.text()).toContain('0:10 / 0:40')
    const spectrogram = wrapper.get('[aria-label="Seek in spectrogram"]')
    await spectrogram.trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.emitted('seek')?.at(-1)).toEqual([0.375])

    vi.spyOn(spectrogram.element, 'getBoundingClientRect').mockReturnValue({
      left: 10,
      width: 200,
    } as DOMRect)
    Object.defineProperty(spectrogram.element, 'setPointerCapture', { value: vi.fn() })
    const pointer = new MouseEvent('pointerdown', { bubbles: true, clientX: 110 })
    Object.defineProperty(pointer, 'pointerId', { value: 1 })
    spectrogram.element.dispatchEvent(pointer)
    await flushPromises()
    expect(wrapper.emitted('seek')?.at(-1)).toEqual([0.5])

    await wrapper.setProps({ position: 20 })
    expect(spectrogram.attributes('aria-valuenow')).toBe('20')
    wrapper.unmount()
  })
})
