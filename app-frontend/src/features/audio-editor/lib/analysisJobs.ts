// The heavy jobs the editor hands to its worker. Kept separate from the worker entry so the
// in-thread fallback (tests, browsers without module workers) runs the exact same code.

import {
  runCleanupChain,
  runCleanupStep,
  type CleanupKind,
  type CleanupStep,
  type ProcessStep,
} from './cleanupSteps'
import { sibilancePeakDb } from './filters'
import { analyze, type AudioAnalysis } from './loudness'
import { matchLoudness, type LoudnessMatch, type MatchOptions } from './matchLoudness'
import type { Channels, FrameRange } from './operations'
import { buildPeaks, type PeakPyramid } from './peaks'
import {
  buildSpectrogram,
  type Spectrogram,
  type SpectrogramInput,
  type SpectrogramRequest,
} from './spectrogram'
import { parseWav, type WavLayout } from './wav'
import { applyTrim, planTrim, profileSilence, type SilenceProfile, type TrimPlan } from './silence'

/** Everything the file list needs for a WAV, without sending its samples back. */
export interface LoadedWav {
  sampleRate: number
  layout: WavLayout
  channelCount: number
  frames: number
  peaks: PeakPyramid
  analysis: AudioAnalysis
}

/** A WAV decoded off the page, its samples and peaks handed over rather than copied. */
export interface DecodedWavJob {
  sampleRate: number
  layout: WavLayout
  channels: Channels
  peaks: PeakPyramid
}

export type AnalysisJob =
  | { kind: 'load'; bytes: ArrayBuffer }
  | { kind: 'decode'; bytes: ArrayBuffer }
  /** Just the head/tail silence of a WAV, for the Audio check page. */
  | { kind: 'silence'; bytes: ArrayBuffer }
  | { kind: 'analyze'; channels: Channels; sampleRate: number }
  | { kind: 'sibilance'; channels: Channels; sampleRate: number; frequency: number }
  | {
      kind: 'spectrogram'
      input: SpectrogramInput
      sampleRate: number
      request: SpectrogramRequest
    }
  /** One cleanup step (kept to `selection` when there is one) or spectral edit. */
  | {
      kind: 'step'
      channels: Channels
      sampleRate: number
      step: ProcessStep
      selection: FrameRange | null
    }
  | {
      kind: 'trim'
      channels: Channels
      sampleRate: number
      keepLeadSeconds: number
      keepTailSeconds: number
      /** Omitted: use this file's own suggested threshold. */
      thresholdDb?: number
    }
  | {
      kind: 'match'
      channels: Channels
      sampleRate: number
      options: MatchOptions
      before?: AudioAnalysis
    }
  | { kind: 'cleanup'; channels: Channels; sampleRate: number; steps: CleanupStep[] }

export interface AnalysisResults {
  load: LoadedWav
  decode: DecodedWavJob
  step: { channels: Channels; clicks: number }
  silence: SilenceProfile
  analyze: AudioAnalysis
  sibilance: number
  spectrogram: Spectrogram
  match: LoudnessMatch
  trim: TrimResult
  cleanup: CleanupResult
}

export interface CleanupResult {
  channels: Channels
  /** Steps that could not run on this file (a noise print at another rate). */
  skipped: CleanupKind[]
  analysis: AudioAnalysis
}

export interface TrimResult {
  /** Null when there was nothing to cut. */
  plan: TrimPlan | null
  thresholdDb: number
  channels: Channels
  analysis: AudioAnalysis
}

export function runJob(job: AnalysisJob): { result: unknown; transfer: Transferable[] } {
  switch (job.kind) {
    case 'load': {
      const wav = parseWav(job.bytes)
      const peaks = buildPeaks(wav.channels)
      const result: LoadedWav = {
        sampleRate: wav.sampleRate,
        layout: wav.layout,
        channelCount: wav.channels.length,
        frames: wav.channels[0]?.length ?? 0,
        peaks,
        analysis: analyze(wav.channels, wav.sampleRate),
      }
      return {
        result,
        transfer: peaks.flatMap((level) => level.channels.map((channel) => channel.buffer)),
      }
    }
    case 'decode': {
      const wav = parseWav(job.bytes)
      const peaks = buildPeaks(wav.channels)
      const result: DecodedWavJob = { ...wav, peaks }
      return {
        result,
        transfer: [
          ...wav.channels.map((channel) => channel.buffer),
          ...peaks.flatMap((level) => level.channels.map((channel) => channel.buffer)),
        ],
      }
    }
    case 'step': {
      const result = runCleanupStep(job.channels, job.sampleRate, job.step, job.selection)
      return { result, transfer: result.channels.map((channel) => channel.buffer) }
    }
    case 'silence': {
      const wav = parseWav(job.bytes)
      return { result: profileSilence(wav.channels, wav.sampleRate), transfer: [] }
    }
    case 'analyze':
      return { result: analyze(job.channels, job.sampleRate), transfer: [] }
    case 'sibilance':
      return { result: sibilancePeakDb(job.channels, job.sampleRate, job.frequency), transfer: [] }
    case 'spectrogram': {
      const result = buildSpectrogram(job.input, job.sampleRate, job.request)
      return { result, transfer: [result.intensity.buffer] }
    }
    case 'trim': {
      const thresholdDb =
        job.thresholdDb ?? profileSilence(job.channels, job.sampleRate).suggestedThresholdDb
      const plan = planTrim(job.channels, job.sampleRate, {
        thresholdDb,
        keepLeadSeconds: job.keepLeadSeconds,
        keepTailSeconds: job.keepTailSeconds,
      })
      const cuts = plan && (plan.removedLeadSeconds > 0 || plan.removedTailSeconds > 0)
      const channels = cuts ? applyTrim(job.channels, job.sampleRate, plan) : job.channels
      const result: TrimResult = {
        plan: cuts ? plan : null,
        thresholdDb,
        channels,
        analysis: analyze(channels, job.sampleRate),
      }
      return { result, transfer: cuts ? channels.map((channel) => channel.buffer) : [] }
    }
    case 'match': {
      const result = matchLoudness(job.channels, job.sampleRate, job.options, job.before)
      return { result, transfer: result.channels.map((channel) => channel.buffer) }
    }
    case 'cleanup': {
      const { channels, skipped } = runCleanupChain(job.channels, job.sampleRate, job.steps)
      const result: CleanupResult = {
        channels,
        skipped,
        analysis: analyze(channels, job.sampleRate),
      }
      return { result, transfer: channels.map((channel) => channel.buffer) }
    }
  }
}
