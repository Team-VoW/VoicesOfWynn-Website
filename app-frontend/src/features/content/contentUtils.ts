import { ApiError } from '@/api/client'

interface ValidationProblem {
  errors?: Record<string, string[]>
  title?: string
}

export const CONTENT_NONE = 'none'

export function optionalContentId(value: string): number | undefined {
  return value === CONTENT_NONE ? undefined : Number(value)
}

export function messageFromContentError(err: unknown): string {
  if (err instanceof ApiError) {
    const body = err.body as ValidationProblem | null
    const firstError = body?.errors ? Object.values(body.errors)[0]?.[0] : undefined
    return firstError ?? body?.title ?? err.message
  }

  return err instanceof Error ? err.message : 'Unknown error'
}
