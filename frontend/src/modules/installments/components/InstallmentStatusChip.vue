<script setup lang="ts">
import { computed } from 'vue'
import ToneChip from '@/components/ToneChip.vue'
import type { InstallmentPlanStatus } from '@/types/api'
import { INSTALLMENT_STATUS_PRESENTATION } from '@/utils/labels'

const props = defineProps<{ status: InstallmentPlanStatus }>()
const presentation = computed(() => INSTALLMENT_STATUS_PRESENTATION[props.status])
</script>

<template>
  <ToneChip :label="presentation.label" :icon="presentation.icon" :tone="presentation.tone" data-testid="plan-status" />
</template>
