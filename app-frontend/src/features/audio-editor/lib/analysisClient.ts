import { runJob, type AnalysisJob, type AnalysisResults } from './analysisJobs'

type Pending = {
  job: AnalysisJob
  resolve: (value: unknown) => void
  reject: (error: Error) => void
}

interface PoolWorker {
  worker: Worker
  pending: Map<number, Pending>
  /** Set by its first reply: until then an error means the worker never got going. */
  started: boolean
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
    started: false,
  }
  entry.worker.onmessage = (
    event: MessageEvent<{ id: number; result?: unknown; error?: string }>,
  ) => {
    entry.started = true
    const job = entry.pending.get(event.data.id)
    if (!job) return
    entry.pending.delete(event.data.id)
    if (event.data.error !== undefined) job.reject(new Error(event.data.error))
    else job.resolve(event.data.result)
  }
  entry.worker.onerror = (event) => {
    event.preventDefault()
    if (!entry.started) {
      // It never ran a job (no module workers, the script failed to load): respawning would only
      // fail again, so every job from here on runs on the page instead.
      poolFailed = true
      const workers = pool ?? [entry]
      pool = null
      for (const failed of workers) abandon(failed, true)
      return
    }
    abandon(entry, false)
    // Replace the crashed worker so the pool keeps its size.
    const index = pool?.indexOf(entry) ?? -1
    if (pool && index >= 0) pool[index] = createWorker()
  }
  return entry
}

/** Stops the worker, rerunning its jobs on the page when asked and their input is still here. */
function abandon(entry: PoolWorker, rerun: boolean) {
  entry.worker.terminate()
  for (const { job, resolve, reject } of entry.pending.values()) {
    // A file's bytes went to the worker with the job and are gone from this side.
    const bytesGone = 'bytes' in job && job.bytes.byteLength === 0
    if (rerun && !bytesGone) runInThread(job).then(resolve, reject)
    else reject(new Error('The analysis worker crashed.'))
  }
  entry.pending.clear()
}

function runInThread(job: AnalysisJob) {
  return new Promise<unknown>((resolve, reject) => {
    setTimeout(() => {
      try {
        resolve(runJob(job).result)
      } catch (error) {
        reject(error instanceof Error ? error : new Error(String(error)))
      }
    })
  })
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
  if (!workers) return runInThread(job) as Promise<AnalysisResults[K]>
  const target = workers.reduce((least, entry) =>
    entry.pending.size < least.pending.size ? entry : least,
  )
  const id = nextId++
  return new Promise((resolve, reject) => {
    target.pending.set(id, { job, resolve: resolve as (value: unknown) => void, reject })
    target.worker.postMessage({ id, job }, transfer)
  })
}
