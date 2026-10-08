// Remembers the last opened folder handle in IndexedDB (handles cannot go in localStorage) so the
// editor can offer to reopen it. Every failure just means there is nothing to reopen.

const DB_NAME = 'vow-audio-editor'
const STORE = 'handles'
const KEY = 'last-folder'

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(STORE)
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

async function run<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>) {
  const db = await open()
  try {
    return await new Promise<T>((resolve, reject) => {
      const request = action(db.transaction(STORE, mode).objectStore(STORE))
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error)
    })
  } finally {
    db.close()
  }
}

export async function rememberFolder(directory: FileSystemDirectoryHandle) {
  try {
    await run('readwrite', (store) => store.put(directory, KEY))
  } catch {
    // Private windows and blocked storage: nothing to remember.
  }
}

export async function recallFolder(): Promise<FileSystemDirectoryHandle | null> {
  try {
    if (typeof indexedDB === 'undefined') return null
    const handle = await run<unknown>('readonly', (store) => store.get(KEY))
    return handle instanceof FileSystemDirectoryHandle ? handle : null
  } catch {
    return null
  }
}
