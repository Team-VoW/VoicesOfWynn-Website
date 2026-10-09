// Cleanup steps as plain data, so applying one, auditioning it live and running a chain of them
// over a batch in the worker all go through the exact same processing.

import { removeClicks } from './clicks'
import {
  compress,
  deEss,
  equalize,
  highPass,
  removeHum,
  type CompressorOptions,
  type DeEssOptions,
  type EqBand,
} from './filters'
import { reduceNoise, type NoisePrint, type NoiseReductionOptions } from './noiseReduction'
import { blendRange, type Channels, type FrameRange } from './operations'
import {
  offsetSelection,
  selectionBounds,
  spectralEdit,
  spectralProblem,
  spectralReach,
  type SpectralEdit,
  type SpectralMode,
} from './spectral'

export type CleanupStep =
  | { kind: 'highPass'; frequency: number; slope: 12 | 24 | 48 }
  | { kind: 'hum'; base: 50 | 60; harmonics: number; reductionDb: number }
  | { kind: 'noise'; print: NoisePrint; options: NoiseReductionOptions }
  | { kind: 'clicks'; threshold: number }
  | { kind: 'deEss'; options: DeEssOptions }
  | { kind: 'eq'; bands: EqBand[] }
  | { kind: 'compress'; options: CompressorOptions }

export type CleanupKind = CleanupStep['kind']

/** An edit of an area of the spectrogram. It carries its own area, so it ignores the selection. */
export type SpectralStep = { kind: 'spectral'; edit: SpectralEdit }

/** Anything that can be previewed and applied: a cleanup step or a spectral edit. */
export type ProcessStep = CleanupStep | SpectralStep

export const SPECTRAL_NAMES: Record<SpectralMode, string> = {
  gain: 'Spectral gain',
  delete: 'Spectral delete',
  heal: 'Heal',
  isolate: 'Selection solo',
}

/** What to call the step on screen, without its settings. */
export function stepName(step: ProcessStep) {
  return step.kind === 'spectral' ? SPECTRAL_NAMES[step.edit.mode] : CLEANUP_NAMES[step.kind]
}

/** The order the panel lists them in, which is also the order a batch chain runs them. */
export const CLEANUP_ORDER: CleanupKind[] = [
  'highPass',
  'hum',
  'noise',
  'clicks',
  'deEss',
  'eq',
  'compress',
]

export const CLEANUP_NAMES: Record<CleanupKind, string> = {
  highPass: 'High-pass',
  hum: 'Hum',
  noise: 'Noise reduction',
  clicks: 'Clicks',
  deEss: 'De-esser',
  eq: 'EQ',
  compress: 'Compressor',
}

/** Joins where a selection's processed audio meets the untouched rest. */
const BLEND_MS = 10
/** Mouth clicks are shorter than this; anything longer is a consonant. */
const MAX_CLICK_MS = 2

export function describeStep(step: ProcessStep) {
  switch (step.kind) {
    case 'spectral': {
      const { mode, gainDb } = step.edit
      if (mode === 'gain') return `Spectral gain ${gainDb > 0 ? '+' : ''}${gainDb} dB`
      if (mode === 'delete') return 'Delete spectral selection'
      if (mode === 'heal') return 'Heal'
      return 'Isolate spectral selection'
    }
    case 'highPass':
      return `High-pass ${step.frequency} Hz`
    case 'hum':
      return `Remove ${step.base} Hz hum`
    case 'noise':
      return `Noise reduction ${step.options.reductionDb} dB`
    case 'clicks':
      return 'Remove clicks'
    case 'deEss':
      return `De-ess above ${step.options.frequency} Hz`
    case 'eq':
      return 'EQ'
    case 'compress':
      return `Compress ${step.options.ratio}:1 at ${step.options.thresholdDb} dB`
  }
}

/** Why a step cannot run on audio at this rate, or null when it can. */
export function stepProblem(step: ProcessStep, sampleRate: number) {
  if (step.kind === 'spectral') return spectralProblem(step.edit, sampleRate)
  if (step.kind === 'noise' && step.print.sampleRate !== sampleRate)
    return `The noise print is from a ${step.print.sampleRate} Hz file; this one is ${sampleRate} Hz.`
  return null
}

function processWhole(channels: Channels, sampleRate: number, step: CleanupStep): Channels {
  switch (step.kind) {
    case 'highPass':
      return highPass(channels, sampleRate, step.frequency, step.slope)
    case 'hum':
      return removeHum(channels, sampleRate, step.base, step.harmonics, step.reductionDb)
    case 'noise':
      return reduceNoise(channels, sampleRate, step.print, step.options)
    case 'clicks':
      // Handled by runCleanupStep, which needs the repair count.
      return channels
    case 'deEss':
      return deEss(channels, sampleRate, step.options)
    case 'eq':
      return equalize(channels, sampleRate, step.bands)
    case 'compress':
      return compress(channels, sampleRate, step.options)
  }
}

/**
 * Runs one step over the file, keeping only the selection's part of the result when there is one.
 * `clicks` counts the repairs of a click step, and is 0 for every other kind.
 */
export function runCleanupStep(
  channels: Channels,
  sampleRate: number,
  step: ProcessStep,
  selection?: FrameRange | null,
): { channels: Channels; clicks: number } {
  if (step.kind === 'spectral')
    return { channels: spectralEdit(channels, sampleRate, step.edit), clicks: 0 }
  if (step.kind === 'clicks') {
    return removeClicks(
      channels,
      sampleRate,
      { threshold: step.threshold, maxClickMs: MAX_CLICK_MS },
      selection,
    )
  }
  const processed = processWhole(channels, sampleRate, step)
  return {
    channels: blendRange(channels, processed, selection, (BLEND_MS / 1000) * sampleRate),
    clicks: 0,
  }
}

/** Runs the steps in order over whole files, skipping any that cannot run at this rate. */
export function runCleanupChain(channels: Channels, sampleRate: number, steps: CleanupStep[]) {
  const skipped: CleanupKind[] = []
  let output = channels
  for (const step of steps) {
    if (stepProblem(step, sampleRate)) skipped.push(step.kind)
    else output = runCleanupStep(output, sampleRate, step).channels
  }
  return { channels: output, skipped }
}

/**
 * The frames a step changes, for running it on just that stretch of a long file: a spectral edit's
 * own area, otherwise the selection (null meaning the whole file).
 */
export function stepRange(step: ProcessStep, selection: FrameRange | null): FrameRange | null {
  if (step.kind !== 'spectral') return selection
  const bounds = selectionBounds(step.edit.selection)
  return bounds ? { start: Math.floor(bounds.start), end: Math.ceil(bounds.end) } : selection
}

/** Frames either side of `stepRange` the step reads, so filters and windows settle first. */
export function stepReach(step: ProcessStep, sampleRate: number, padSeconds: number) {
  const pad = Math.round(padSeconds * sampleRate)
  return step.kind === 'spectral' ? Math.max(pad, spectralReach(step.edit)) : pad
}

/** The step as it applies to audio starting `frames` later, for running it on a slice. */
export function offsetStep<T extends ProcessStep>(step: T, frames: number): T {
  if (step.kind !== 'spectral' || frames === 0) return step
  return {
    ...step,
    edit: { ...step.edit, selection: offsetSelection(step.edit.selection, frames) },
  }
}
