<script setup lang="ts">
import { Settings2 } from 'lucide-vue-next'
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui'
import { computed } from 'vue'
import { Button } from '@/components/ui/button'
import { formatFrequency, type FrequencyScale } from '../lib/frequencyScale'
import { MAX_HEAL_SECONDS } from '../lib/spectral'
import { FFT_SIZES } from '../lib/spectrogram'
import { SPECTRUM_COLOR_NAMES, type SpectrumColors } from '../lib/spectrumColors'
import { useAudioWorkspace } from '../stores/workspace'
import ScrubNumber from './ScrubNumber.vue'

/** How the spectrogram looks, and how its edits behave, behind a button in its corner. */
const workspace = useAudioWorkspace()
const settings = computed(() => workspace.spectralSettings)

const SCALES: { id: FrequencyScale; label: string }[] = [
  { id: 'log', label: 'Log' },
  { id: 'mel', label: 'Mel' },
  { id: 'linear', label: 'Linear' },
]

const zoomLabel = computed(() => {
  const view = workspace.frequencyView
  return view ? `${formatFrequency(view.low)} – ${formatFrequency(view.high)} Hz` : 'Full range'
})

const select = 'h-8 rounded-md border bg-background px-2 text-sm'
const hint = 'text-xs text-muted-foreground'
const heading = 'text-xs font-semibold uppercase tracking-wide text-muted-foreground'
const kbd = 'rounded border bg-muted px-1 font-mono text-[10px] text-foreground'
</script>

<template>
  <PopoverRoot>
    <PopoverTrigger
      class="grid size-7 place-items-center rounded-md bg-black/55 text-[#d6e2f5] shadow hover:bg-black/75 data-[state=open]:bg-black/80 [&_svg]:size-4"
      aria-label="Spectrogram settings"
      title="Spectrogram settings"
    >
      <Settings2 />
    </PopoverTrigger>
    <PopoverPortal>
      <PopoverContent
        side="bottom"
        align="end"
        :side-offset="6"
        :collision-padding="8"
        class="z-50 w-72 space-y-4 rounded-md border bg-popover p-3 text-sm text-popover-foreground shadow-md data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
      >
        <section class="space-y-2">
          <h3 :class="heading">Display</h3>
          <div class="grid grid-cols-[4.5rem_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5">
            <label :class="hint" for="spectral-fft">Resolution</label>
            <select id="spectral-fft" v-model.number="settings.fftSize" :class="select">
              <option v-for="fftSize in FFT_SIZES" :key="fftSize" :value="fftSize">
                {{ fftSize
                }}{{ fftSize <= 512 ? ' · sharp timing' : fftSize >= 4096 ? ' · sharp pitch' : '' }}
              </option>
            </select>
            <span :class="hint">Scale</span>
            <div class="inline-flex rounded-md border bg-background p-0.5" role="group">
              <button
                v-for="option in SCALES"
                :key="option.id"
                type="button"
                :aria-pressed="settings.scale === option.id"
                class="flex-1 rounded px-2 py-1 text-xs font-medium"
                :class="
                  settings.scale === option.id
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:text-foreground'
                "
                @click="settings.scale = option.id"
              >
                {{ option.label }}
              </button>
            </div>
            <label :class="hint" for="spectral-range">Range</label>
            <ScrubNumber
              id="spectral-range"
              v-model="settings.rangeDb"
              :min="30"
              :max="160"
              :step="2"
              unit="dB"
              title="How far below the loudest level still shows; lower it to see only the louder parts"
            />
            <label :class="hint" for="spectral-colors">Colours</label>
            <select id="spectral-colors" v-model="settings.colors" :class="select">
              <option
                v-for="(name, id) in SPECTRUM_COLOR_NAMES"
                :key="id"
                :value="id as SpectrumColors"
              >
                {{ name }}
              </option>
            </select>
            <span :class="hint">Zoom</span>
            <div class="flex items-center gap-2">
              <span class="min-w-0 flex-1 truncate text-xs tabular-nums">{{ zoomLabel }}</span>
              <Button
                size="sm"
                variant="ghost"
                :disabled="!workspace.frequencyView"
                @click="workspace.frequencyView = null"
                >Reset</Button
              >
            </div>
          </div>
          <p :class="hint">
            <span :class="kbd">Alt</span>+wheel zooms the frequencies,
            <span :class="kbd">Alt+Shift</span>+wheel scrolls them; double-click the labels to see
            them all again. Drag the bar between waveform and spectrogram to resize them.
          </p>
        </section>

        <section class="space-y-2 border-t pt-3">
          <h3 :class="heading">Edits</h3>
          <div class="grid grid-cols-[4.5rem_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5">
            <label :class="hint" for="spectral-feather">Feather</label>
            <ScrubNumber
              id="spectral-feather"
              v-model="settings.feather"
              :min="0"
              :max="100"
              :step="5"
              unit="%"
              title="How softly edits fade out past the edges of the selection"
            />
            <label :class="hint" for="spectral-heal">Heal from</label>
            <select id="spectral-heal" v-model="settings.healDirection" :class="select">
              <option value="auto">The nearest sound</option>
              <option value="time">Before and after</option>
              <option value="frequency">Above and below</option>
            </select>
          </div>
          <p :class="hint">
            Edits run at the resolution shown, so what you select is what changes. Before and after
            suits short noises, above and below long whistles. Heal works on up to
            {{ MAX_HEAL_SECONDS }} s at a time.
          </p>
        </section>
      </PopoverContent>
    </PopoverPortal>
  </PopoverRoot>
</template>
