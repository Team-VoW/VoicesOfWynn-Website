import { apiFetch } from './client'
import type {
  AccountDetails,
  AccountRole,
  AccountSearchRequest,
  AccountSearchResponse,
  CreateAccountRequest,
  CreateAccountResponse,
  ResetPasswordResponse,
  UpdateAccountRequest,
  UpdateAccountRolesRequest,
} from './types'

export function getAccountRoles(signal?: AbortSignal): Promise<AccountRole[]> {
  return apiFetch<AccountRole[]>('/admin/accounts/roles', { signal })
}

export function searchAccounts(
  params: AccountSearchRequest,
  signal?: AbortSignal,
): Promise<AccountSearchResponse> {
  return apiFetch<AccountSearchResponse>('/admin/accounts/search', {
    query: {
      query: params.query,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  })
}

export function getAccount(userId: number, signal?: AbortSignal): Promise<AccountDetails> {
  return apiFetch<AccountDetails>(`/admin/accounts/${userId}`, { signal })
}

export function updateAccount(userId: number, request: UpdateAccountRequest): Promise<void> {
  return apiFetch<void>(`/admin/accounts/${userId}`, {
    method: 'PATCH',
    body: request,
  })
}

export function updateAccountRoles(
  userId: number,
  request: UpdateAccountRolesRequest,
): Promise<void> {
  return apiFetch<void>(`/admin/accounts/${userId}/roles`, {
    method: 'PUT',
    body: request,
  })
}

export function uploadAccountAvatar(
  userId: number,
  file: Blob,
  fileName = 'avatar.webp',
): Promise<void> {
  const form = new FormData()
  form.append('file', file, fileName)
  return apiFetch<void>(`/admin/accounts/${userId}/avatar`, {
    method: 'PUT',
    body: form,
  })
}

export function clearAccountAvatar(userId: number): Promise<void> {
  return apiFetch<void>(`/admin/accounts/${userId}/avatar`, {
    method: 'DELETE',
  })
}

export function resetAccountPassword(userId: number): Promise<ResetPasswordResponse> {
  return apiFetch<ResetPasswordResponse>(`/admin/accounts/${userId}/reset-password`, {
    method: 'POST',
  })
}

export function deleteAccount(userId: number): Promise<void> {
  return apiFetch<void>(`/admin/accounts/${userId}`, {
    method: 'DELETE',
  })
}

export function createAccount(request: CreateAccountRequest): Promise<CreateAccountResponse> {
  return apiFetch<CreateAccountResponse>('/admin/accounts', {
    method: 'POST',
    body: request,
  })
}
