import { runJob, type AnalysisJob, type AnalysisResults } from './analysisJobs'

type Pending = { resolve: (value: unknown) => void; reject: (error: Error) => void }

interface PoolWorker {
  worker: Worker
  pending: Map<number, Pending>
}

let pool: PoolWorker[] | null = null
let poolFailed = false
let nextId = 1

/** One worker per core, leaving one for the page itself. */
export const WORKER_COUNT = Math.max(
  1,
  Math.min(8, (typeof navigator !== 'undefined' ? navigator.hardwareConcurrency : 2) - 1 || 1),
)

function createWorker(): PoolWorker {
  const entry: PoolWorker = {
    worker: new Worker(new URL('./analysis.worker.ts', import.meta.url), { type: 'module' }),
    pending: new Map(),
  }
  entry.worker.onmessage = (
    event: MessageEvent<{ id: number; result?: unknown; error?: string }>,
  ) => {
    const job = entry.pending.get(event.data.id)
    if (!job) return
    entry.pending.delete(event.data.id)
    if (event.data.error !== undefined) job.reject(new Error(event.data.error))
    else job.resolve(event.data.result)
  }
  entry.worker.onerror = (event) => {
    event.preventDefault()
    for (const job of entry.pending.values()) job.reject(new Error('The analysis worker crashed.'))
    entry.pending.clear()
    entry.worker.terminate()
    // Replace the crashed worker so the pool keeps its size.
    if (pool) pool[pool.indexOf(entry)] = createWorker()
  }
  return entry
}

function getPool() {
  if (pool || poolFailed || typeof Worker === 'undefined') return pool
  try {
    pool = Array.from({ length: WORKER_COUNT }, createWorker)
  } catch {
    poolFailed = true
    pool = null
  }
  return pool
}

/**
 * Runs a job on the least busy worker. Channels are copied to the worker, so the caller keeps its
 * buffers; anything listed in `transfer` (a file's bytes) is handed over without a copy and is
 * unusable afterwards. Results (peaks, spectrogram pixels, matched audio) come back transferred.
 */
export function runAnalysisJob<K extends AnalysisJob['kind']>(
  job: Extract<AnalysisJob, { kind: K }>,
  transfer: Transferable[] = [],
): Promise<AnalysisResults[K]> {
  const workers = getPool()
  if (!workers) {
    return new Promise((resolve, reject) => {
      setTimeout(() => {
        try {
          resolve(runJob(job).result as AnalysisResults[K])
        } catch (error) {
          reject(error instanceof Error ? error : new Error(String(error)))
        }
      })
    })
  }
  const target = workers.reduce((least, entry) =>
    entry.pending.size < least.pending.size ? entry : least,
  )
  const id = nextId++
  return new Promise((resolve, reject) => {
    target.pending.set(id, { resolve: resolve as (value: unknown) => void, reject })
    target.worker.postMessage({ id, job }, transfer)
  })
}
