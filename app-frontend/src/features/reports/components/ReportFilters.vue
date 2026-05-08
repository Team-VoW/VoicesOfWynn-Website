<script setup lang="ts">
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { REPORT_STATUSES, type ReportStatus } from '@/api/types'

interface Filters {
  npc: string
  content: string
  status: ReportStatus | 'any'
}

const npc = defineModel<string>('npc', { default: '' })
const content = defineModel<string>('content', { default: '' })
const status = defineModel<Filters['status']>('status', { default: 'any' })
</script>

<template>
  <div class="grid gap-4 md:grid-cols-3">
    <div class="space-y-2">
      <Label for="filter-npc">NPC</Label>
      <Input id="filter-npc" v-model="npc" placeholder="e.g. Theorick" />
    </div>
    <div class="space-y-2">
      <Label for="filter-content">Message contains</Label>
      <Input id="filter-content" v-model="content" placeholder="search message text" />
    </div>
    <div class="space-y-2">
      <Label for="filter-status">Status</Label>
      <Select v-model="status">
        <SelectTrigger id="filter-status" class="w-full">
          <SelectValue placeholder="Any" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="any">Any</SelectItem>
          <SelectItem v-for="s in REPORT_STATUSES" :key="s" :value="s">{{ s }}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  </div>
</template>
