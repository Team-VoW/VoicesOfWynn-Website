// The VoW delivery rules for a voice line. The Audio check page measures against them with ffmpeg
// on the server; the audio editor measures and fixes against them in the browser.

export const LEADING_SILENCE_LIMIT = 0.1
export const TRAILING_SILENCE_LIMIT = 0.4
export const TRUE_PEAK_LIMIT = -1
/**
 * Where the audio editor starts warning about true peak. Looser than the -1 dBTP target that Match
 * loudness aims for, so a line a hair over it is not flagged across a whole batch.
 */
export const TRUE_PEAK_WARNING = -0.75
/** Matches `AudioAnalysis:SilenceNoiseThresholdDb` in the API's AudioAnalysisService. */
export const SILENCE_THRESHOLD_DB = -50

export const AUDIO_FILE_NAME_PATTERN = /^[a-z0-9]+-[a-z0-9]+-[1-9][0-9]*\.wav$/
export const AUDIO_FILE_NAME_ERROR =
  'Filename must be questname-npcname-number.wav with only lowercase letters, numbers, exactly two hyphens, and no leading zeros in the number.'

export interface LoudnessPreset {
  id: 'whispering' | 'speaking' | 'shouting'
  label: string
  lufs: number
}

export const LOUDNESS_PRESETS: readonly LoudnessPreset[] = [
  { id: 'whispering', label: 'Whispering', lufs: -23 },
  { id: 'speaking', label: 'Speaking', lufs: -18 },
  { id: 'shouting', label: 'Shouting', lufs: -13 },
]

export interface LufsVerdict {
  label: string
  tone: 'danger' | 'warning' | 'success' | 'info'
}

export function lufsVerdict(lufs: number): LufsVerdict {
  if (lufs < -25)
    return { label: 'very quiet — review unless intentionally subtle', tone: 'danger' }
  if (lufs < -22) return { label: 'whispering — on the quiet side', tone: 'info' }
  if (lufs < -20) return { label: 'whispering — perfect (~-23)', tone: 'success' }
  if (lufs < -19) return { label: 'speaking — a touch quiet', tone: 'info' }
  if (lufs < -17) return { label: 'speaking — perfect (~-18)', tone: 'success' }
  if (lufs < -16) return { label: 'speaking — a bit louder', tone: 'info' }
  if (lufs < -14) return { label: 'speaking — LOUD, watch headroom', tone: 'warning' }
  if (lufs < -12) return { label: 'shouting — perfect (~-13)', tone: 'success' }
  return { label: 'very loud — likely too hot for shouting', tone: 'warning' }
}

export function validateAudioFileName(fileName: string) {
  return AUDIO_FILE_NAME_PATTERN.test(fileName) ? null : AUDIO_FILE_NAME_ERROR
}
