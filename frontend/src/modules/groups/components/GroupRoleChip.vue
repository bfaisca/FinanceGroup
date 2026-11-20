<script setup lang="ts">
import ToneChip from '@/components/ToneChip.vue'
import type { GroupMemberRole } from '@/types/api'
import { ROLE_ICONS, ROLE_LABELS } from '../labels'

defineProps<{ role: GroupMemberRole }>()
</script>

<template>
  <ToneChip :label="ROLE_LABELS[role]" :icon="ROLE_ICONS[role]" :tone="role === 'Owner' ? 'info' : 'neutral'" />
</template>
