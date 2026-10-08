// The parts of the File System Access API that TypeScript's DOM lib leaves out because only
// Chromium ships them. Everything here is feature-detected before use.

interface FileSystemPermissionDescriptor {
  mode?: 'read' | 'readwrite'
}

interface FileSystemHandle {
  queryPermission?(descriptor?: FileSystemPermissionDescriptor): Promise<PermissionState>
  requestPermission?(descriptor?: FileSystemPermissionDescriptor): Promise<PermissionState>
}

interface FileSystemDirectoryHandle {
  values(): AsyncIterableIterator<FileSystemFileHandle | FileSystemDirectoryHandle>
}

interface FilePickerAcceptType {
  description?: string
  accept: Record<string, string[]>
}

interface OpenFilePickerOptions {
  multiple?: boolean
  types?: FilePickerAcceptType[]
  excludeAcceptAllOption?: boolean
  id?: string
}

interface SaveFilePickerOptions {
  suggestedName?: string
  types?: FilePickerAcceptType[]
  id?: string
}

interface DirectoryPickerOptions {
  mode?: 'read' | 'readwrite'
  id?: string
}

interface Window {
  showOpenFilePicker?(options?: OpenFilePickerOptions): Promise<FileSystemFileHandle[]>
  showSaveFilePicker?(options?: SaveFilePickerOptions): Promise<FileSystemFileHandle>
  showDirectoryPicker?(options?: DirectoryPickerOptions): Promise<FileSystemDirectoryHandle>
}

interface DataTransferItem {
  getAsFileSystemHandle?(): Promise<FileSystemHandle | null>
}
