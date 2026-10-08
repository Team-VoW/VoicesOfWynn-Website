// The heavy jobs the editor hands to its worker. Kept separate from the worker entry so the
// in-thread fallback (tests, browsers without module workers) runs the exact same code.

import { runCleanupChain, type CleanupKind, type CleanupStep } from './cleanupSteps'
import { analyze, matchLoudness, type AudioAnalysis, type LoudnessMatch } from './loudness'
import type { Channels } from './operations'
import { buildPeaks, type PeakPyramid } from './peaks'
import { buildSpectrogram, type Spectrogram } from './spectrogram'
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

export type AnalysisJob =
  | { kind: 'load'; bytes: ArrayBuffer }
  /** Just the head/tail silence of a WAV, for the Audio check page. */
  | { kind: 'silence'; bytes: ArrayBuffer }
  | { kind: 'analyze'; channels: Channels; sampleRate: number }
  | { kind: 'spectrogram'; channels: Channels; sampleRate: number }
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
      targetLufs: number
      ceilingDbtp: number
      before?: AudioAnalysis
    }
  | { kind: 'cleanup'; channels: Channels; sampleRate: number; steps: CleanupStep[] }

export interface AnalysisResults {
  load: LoadedWav
  silence: SilenceProfile
  analyze: AudioAnalysis
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
    case 'silence': {
      const wav = parseWav(job.bytes)
      return { result: profileSilence(wav.channels, wav.sampleRate), transfer: [] }
    }
    case 'analyze':
      return { result: analyze(job.channels, job.sampleRate), transfer: [] }
    case 'spectrogram': {
      const result = buildSpectrogram(job.channels, job.sampleRate)
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
      const result = matchLoudness(
        job.channels,
        job.sampleRate,
        job.targetLufs,
        job.ceilingDbtp,
        job.before,
      )
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
