export interface ParsedVoiceLine {
  originalLine: string
  cleanedLine: string
  speaker: string
  fileName: string
}

export interface ParsedScriptResult {
  sourceLineCount: number
  skippedLineCount: number
  voiceLines: ParsedVoiceLine[]
}

export interface ParseScriptOptions {
  stripLineInformation?: boolean
}

export interface FormatGeneratedOutputOptions {
  includeFileNames?: boolean
}

export interface GeneratedCodeLine {
  line: string
  file: string
}

export interface SelectedAudioFileNames {
  fileNames: string[]
  skippedFiles: string[]
  duplicateFileNames: string[]
}

export interface AudioFileComparison {
  expectedCount: number
  actualCount: number
  matchedFileNames: string[]
  missingFileNames: string[]
  extraFileNames: string[]
}

const audioFileExtensionPattern = /\.(ogg|wav)$/i

function getStringBefore(text: string, stopAt: string): string {
  const index = text.indexOf(stopAt)
  return index > 0 ? text.slice(0, index) : ''
}

function removeBetweenNotes(line: string): string {
  return line.replace(/\{.*?\}/g, '')
}

function removeCharacterChangeNote(line: string): string {
  return line.includes('/$') ? getStringBefore(line, '/$') : line
}

function stripLineInformation(line: string): string {
  let stripped = line

  if (stripped.includes('{') && stripped.includes('}')) {
    stripped = removeBetweenNotes(stripped)
  }

  if (stripped.includes('//')) {
    stripped = getStringBefore(stripped, '//')
  }

  return stripped
}

function cleanScriptLines(rawText: string, shouldStripLineInformation: boolean): string[] {
  return rawText
    .split(/\r?\n/)
    .map((rawLine) => {
      return shouldStripLineInformation ? stripLineInformation(rawLine) : rawLine
    })
    .filter((line) => {
      if (line.startsWith('//')) return false
      if (line.startsWith('---')) return false
      if (line === '') return false
      if (line.includes('Emotions will')) return false
      return true
    })
}

function getSpeakerName(line: string): string {
  const characterChangeIndex = line.indexOf('/$')
  if (characterChangeIndex !== -1) {
    return line.slice(characterChangeIndex + 2).trim()
  }

  const colonIndex = line.indexOf(':')
  if (colonIndex > 0) {
    return line.slice(0, colonIndex).trim()
  }

  return 'unknown'
}

function normalizeName(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, '')
}

function removeInlineTags(line: string): string {
  return line
    .replace(/<name>/g, 'soldier')
    .replace(/<[^>]*>/g, '')
}

function getAudioFileNameWithoutExtension(fileName: string): string {
  return fileName.replace(audioFileExtensionPattern, '').toLowerCase()
}

export function parseScriptText(
  rawText: string,
  questName: string,
  options: ParseScriptOptions = {}
): ParsedScriptResult {
  const shouldStripLineInformation = options.stripLineInformation ?? true
  const lines = cleanScriptLines(rawText, shouldStripLineInformation)
  const normalizedQuestName = normalizeName(questName)
  const generatedNames: string[] = []

  const voiceLines = lines.map((line) => {
    const speaker = getSpeakerName(stripLineInformation(line))
    const baseFileName = `${normalizedQuestName}-${normalizeName(speaker)}`
    const dialogueNumber = generatedNames.filter((fileName) =>
      fileName.includes(`${baseFileName}-`)
    ).length + 1
    const fileName = `${baseFileName}-${dialogueNumber}`

    generatedNames.push(fileName)

    return {
      originalLine: line,
      cleanedLine: removeCharacterChangeNote(line).trim(),
      speaker,
      fileName,
    }
  })

  return {
    sourceLineCount: rawText.split(/\r?\n/).length,
    skippedLineCount: rawText.split(/\r?\n/).length - lines.length,
    voiceLines,
  }
}

export function formatGeneratedOutput(
  voiceLines: ParsedVoiceLine[],
  options: FormatGeneratedOutputOptions = {}
): string {
  const includeFileNames = options.includeFileNames ?? true

  return voiceLines
    .map((line) => includeFileNames ? `${line.cleanedLine} | ${line.fileName}` : line.cleanedLine)
    .join('\n')
}

export function generateCodeLines(rawText: string, questName: string): GeneratedCodeLine[] {
  return parseScriptText(rawText, questName, { stripLineInformation: true }).voiceLines.map((line) => ({
    line: removeInlineTags(line.cleanedLine).trim(),
    file: line.fileName.replace(/\[played\]/gi, '').trim(),
  }))
}

export function generateCodeJsonOutput(rawText: string, questName: string): string {
  return JSON.stringify(generateCodeLines(rawText, questName), null, 2)
}

export function getExpectedAudioFileNames(rawText: string, questName: string): string[] {
  return parseScriptText(rawText, questName, { stripLineInformation: true }).voiceLines.map((line) => line.fileName)
}

export function getSelectedAudioFileNames(files: File[]): SelectedAudioFileNames {
  const fileNames: string[] = []
  const skippedFiles: string[] = []
  const counts = new Map<string, number>()

  files.forEach((file) => {
    if (!audioFileExtensionPattern.test(file.name)) {
      skippedFiles.push(file.name)
      return
    }

    const fileName = getAudioFileNameWithoutExtension(file.name)
    fileNames.push(fileName)
    counts.set(fileName, (counts.get(fileName) ?? 0) + 1)
  })

  const duplicateFileNames = Array.from(counts.entries())
    .filter(([, count]) => count > 1)
    .map(([fileName]) => fileName)

  return {
    fileNames,
    skippedFiles,
    duplicateFileNames,
  }
}

export function compareAudioFiles(expectedFileNames: string[], actualFileNames: string[]): AudioFileComparison {
  const expectedSet = new Set(expectedFileNames.map((fileName) => fileName.toLowerCase()))
  const actualSet = new Set(actualFileNames.map((fileName) => fileName.toLowerCase()))

  const missingFileNames = Array.from(expectedSet)
    .filter((fileName) => !actualSet.has(fileName))
    .sort()
  const extraFileNames = Array.from(actualSet)
    .filter((fileName) => !expectedSet.has(fileName))
    .sort()
  const matchedFileNames = Array.from(expectedSet)
    .filter((fileName) => actualSet.has(fileName))
    .sort()

  return {
    expectedCount: expectedSet.size,
    actualCount: actualSet.size,
    matchedFileNames,
    missingFileNames,
    extraFileNames,
  }
}
