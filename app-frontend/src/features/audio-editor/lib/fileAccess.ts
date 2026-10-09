// Opening and saving local files. Chromium gets real handles, so Ctrl+S writes over the original;
// other browsers get plain Files and every save becomes a download.

export interface OpenedFile {
  file: File
  handle: FileSystemFileHandle | null
  /** Folder-relative path for display, e.g. `quest/npc-1.wav`. */
  path: string
}

export const AUDIO_EXTENSIONS = ['.wav', '.ogg', '.mp3', '.flac', '.m4a', '.opus', '.aif', '.aiff']

const PICKER_TYPES: FilePickerAcceptType[] = [
  {
    description: 'Audio',
    accept: {
      'audio/*': AUDIO_EXTENSIONS,
    },
  },
]

export function isAudioFileName(name: string) {
  const lower = name.toLowerCase()
  return AUDIO_EXTENSIONS.some((extension) => lower.endsWith(extension))
}

export function canWriteInPlace() {
  return typeof window !== 'undefined' && typeof window.showSaveFilePicker === 'function'
}

export function canOpenFolders() {
  return typeof window !== 'undefined' && typeof window.showDirectoryPicker === 'function'
}

function isAbort(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

async function walk(directory: FileSystemDirectoryHandle, prefix: string, into: OpenedFile[]) {
  const entries: (FileSystemFileHandle | FileSystemDirectoryHandle)[] = []
  for await (const entry of directory.values()) entries.push(entry)
  entries.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
  for (const entry of entries) {
    const path = prefix ? `${prefix}/${entry.name}` : entry.name
    if (entry.kind === 'directory') {
      if (!entry.name.startsWith('.')) await walk(entry as FileSystemDirectoryHandle, path, into)
    } else if (isAudioFileName(entry.name)) {
      const handle = entry as FileSystemFileHandle
      into.push({ file: await handle.getFile(), handle, path })
    }
  }
}

export async function readDirectory(directory: FileSystemDirectoryHandle) {
  const files: OpenedFile[] = []
  await walk(directory, '', files)
  return files
}

/** Returns null when the person cancels the picker. */
export async function pickDirectory() {
  try {
    const directory = await window.showDirectoryPicker!({ mode: 'readwrite', id: 'vow-audio' })
    return { directory, files: await readDirectory(directory) }
  } catch (error) {
    if (isAbort(error)) return null
    throw error
  }
}

export async function pickFiles(): Promise<OpenedFile[] | null> {
  try {
    const handles = await window.showOpenFilePicker!({
      multiple: true,
      types: PICKER_TYPES,
      id: 'vow-audio',
    })
    return Promise.all(
      handles.map(async (handle) => ({ file: await handle.getFile(), handle, path: handle.name })),
    )
  } catch (error) {
    if (isAbort(error)) return null
    throw error
  }
}

export function fromFileList(files: Iterable<File>): OpenedFile[] {
  return Array.from(files)
    .filter((file) => isAudioFileName(file.name))
    .map((file) => ({ file, handle: null, path: file.webkitRelativePath || file.name }))
}

/**
 * Dropped items, keeping writable handles where the browser offers them. Every handle promise is
 * requested before the first await: the DataTransfer is emptied once the drop handler yields.
 */
export async function fromDataTransfer(transfer: DataTransfer): Promise<OpenedFile[]> {
  const items = Array.from(transfer.items).filter((item) => item.kind === 'file')
  if (items.length === 0 || typeof items[0]!.getAsFileSystemHandle !== 'function')
    return fromFileList(Array.from(transfer.files))

  const handlePromises = items.map((item) => item.getAsFileSystemHandle!())
  const plainFiles = items.map((item) => item.getAsFile())
  const files: OpenedFile[] = []
  for (const [index, handle] of (await Promise.all(handlePromises)).entries()) {
    if (!handle) {
      // Some drag sources hand over bytes without a handle; still editable, saved as a download.
      const file = plainFiles[index]
      if (file) files.push(...fromFileList([file]))
      continue
    }
    if (handle.kind === 'directory') {
      await walk(handle as FileSystemDirectoryHandle, handle.name, files)
    } else if (isAudioFileName(handle.name)) {
      const fileHandle = handle as FileSystemFileHandle
      files.push({ file: await fileHandle.getFile(), handle: fileHandle, path: handle.name })
    }
  }
  return files
}

export async function ensureWritable(handle: FileSystemHandle) {
  if (!handle.queryPermission || !handle.requestPermission) return true
  if ((await handle.queryPermission({ mode: 'readwrite' })) === 'granted') return true
  return (await handle.requestPermission({ mode: 'readwrite' })) === 'granted'
}

export async function writeFile(handle: FileSystemFileHandle, data: Blob) {
  if (!(await ensureWritable(handle))) throw new Error(`Write access to ${handle.name} was denied.`)
  const stream = await handle.createWritable()
  try {
    await stream.write(data)
    await stream.close()
  } catch (error) {
    await stream.abort().catch(() => {})
    throw error
  }
}

/** Returns the new handle, or null when the person cancels. */
export async function saveAs(suggestedName: string, data: Blob) {
  try {
    const handle = await window.showSaveFilePicker!({
      suggestedName,
      types: [{ description: 'WAV audio', accept: { 'audio/wav': ['.wav'] } }],
      id: 'vow-audio',
    })
    await writeFile(handle, data)
    return handle
  } catch (error) {
    if (isAbort(error)) return null
    throw error
  }
}

export function download(fileName: string, data: Blob) {
  const url = URL.createObjectURL(data)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export function withWavExtension(name: string) {
  return name.replace(/\.[^./]+$/, '') + '.wav'
}
