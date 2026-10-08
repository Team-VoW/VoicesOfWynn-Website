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

export type CleanupStep =
  | { kind: 'highPass'; frequency: number; slope: 12 | 24 | 48 }
  | { kind: 'hum'; base: 50 | 60; harmonics: number; reductionDb: number }
  | { kind: 'noise'; print: NoisePrint; options: NoiseReductionOptions }
  | { kind: 'clicks'; threshold: number }
  | { kind: 'deEss'; options: DeEssOptions }
  | { kind: 'eq'; bands: EqBand[] }
  | { kind: 'compress'; options: CompressorOptions }

export type CleanupKind = CleanupStep['kind']

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

export function describeStep(step: CleanupStep) {
  switch (step.kind) {
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
export function stepProblem(step: CleanupStep, sampleRate: number) {
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
  step: CleanupStep,
  selection?: FrameRange | null,
): { channels: Channels; clicks: number } {
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
