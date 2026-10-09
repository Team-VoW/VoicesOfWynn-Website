import { runJob, type AnalysisJob } from './analysisJobs'

// Typed against the DOM lib the app compiles with; the worker scope's postMessage takes transfers
// as a plain second argument rather than Window's targetOrigin.
const scope = self as unknown as {
  onmessage: ((event: MessageEvent<{ id: number; job: AnalysisJob }>) => void) | null
  postMessage(message: unknown, transfer?: Transferable[]): void
}

scope.onmessage = (event) => {
  const { id, job } = event.data
  try {
    const { result, transfer } = runJob(job)
    scope.postMessage({ id, result }, transfer)
  } catch (error) {
    scope.postMessage({ id, error: error instanceof Error ? error.message : String(error) })
  }
}
