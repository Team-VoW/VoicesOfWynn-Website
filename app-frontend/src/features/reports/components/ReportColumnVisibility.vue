<script setup lang="ts">
import { Columns3 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { REPORT_SORT_FIELDS } from '@/api/types'
import { useReportColumnVisibility } from '../composables/useReportColumnVisibility'

const { visibility, toggle, columnLabels } = useReportColumnVisibility()
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button variant="outline" size="sm">
        <Columns3 class="size-4" />
        Columns
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end" class="w-44">
      <DropdownMenuLabel>Show columns</DropdownMenuLabel>
      <DropdownMenuSeparator />
      <DropdownMenuCheckboxItem
        v-for="key in REPORT_SORT_FIELDS"
        :key="key"
        :model-value="visibility[key]"
        @update:model-value="(v) => toggle(key, v === true)"
        @select.prevent
      >
        {{ columnLabels[key] }}
      </DropdownMenuCheckboxItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
