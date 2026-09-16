import { apiFetch } from './client'
import type { ContributorDetail, ContributorListResponse, ContributorVotes } from './types'

export function listContributors(
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<ContributorListResponse> {
  return apiFetch<ContributorListResponse>('/contributors', { query: { page, pageSize }, signal })
}

export function getContributor(userId: number, signal?: AbortSignal): Promise<ContributorDetail> {
  return apiFetch<ContributorDetail>(`/contributors/${userId}`, { signal })
}

export function getContributorVotes(
  userId: number,
  signal?: AbortSignal,
): Promise<ContributorVotes> {
  return apiFetch<ContributorVotes>(`/contributors/${userId}/my-votes`, { signal })
}
