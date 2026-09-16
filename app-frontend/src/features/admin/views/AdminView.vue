<script setup lang="ts">
import AnalyticsAggregationCard from '../components/AnalyticsAggregationCard.vue'
import BroadcastsCard from '../components/BroadcastsCard.vue'
import FunFactsCard from '../components/FunFactsCard.vue'
import ModReleaseCard from '../components/ModReleaseCard.vue'
</script>

<template>
  <div class="space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold">Admin</h1>
      <p class="text-sm text-muted-foreground">
        Maintenance jobs and everything the mod is told when a player launches it. Restricted to
        Project Directors and Admins.
      </p>
    </header>

    <AnalyticsAggregationCard />
    <ModReleaseCard />
    <BroadcastsCard />
    <FunFactsCard />
  </div>
</template>
