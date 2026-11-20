<script setup lang="ts">
import ToneChip from '@/components/ToneChip.vue'
import type { GroupSummaryDto } from '../types'
import GroupRoleChip from './GroupRoleChip.vue'

defineProps<{ group: GroupSummaryDto }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'group-detail', params: { id: group.id } }"
    class="group-card"
    :class="{ 'group-card--archived': !group.isActive }"
  >
    <span class="group-card__top">
      <span class="group-card__name">{{ group.name }}</span>
      <i class="pi pi-chevron-right group-card__arrow" aria-hidden="true" />
    </span>
    <span v-if="group.description" class="group-card__description">{{ group.description }}</span>
    <span class="group-card__meta">
      <GroupRoleChip :role="group.myRole" />
      <ToneChip v-if="!group.isActive" label="Arquivado" icon="pi pi-inbox" tone="neutral" />
      <span class="group-card__count">
        <i class="pi pi-users" aria-hidden="true" />
        {{ group.memberCount }} {{ group.memberCount === 1 ? 'membro' : 'membros' }}
      </span>
    </span>
  </RouterLink>
</template>

<style scoped>
.group-card {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  padding: 1.125rem 1.25rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
  color: inherit;
  text-decoration: none;
  min-width: 0;
}

.group-card:hover {
  background: var(--p-content-hover-background);
}

.group-card--archived {
  opacity: 0.85;
}

.group-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.group-card__name {
  font-size: 1.0625rem;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.group-card__arrow {
  color: var(--gf-text-muted);
}

.group-card__description {
  color: var(--gf-text-muted);
  overflow-wrap: anywhere;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.group-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}

.group-card__count {
  display: inline-flex;
  gap: 0.375rem;
  align-items: center;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}
</style>
