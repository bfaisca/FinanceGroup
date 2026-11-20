<script setup lang="ts">
import { computed } from 'vue'
import ProgressBar from 'primevue/progressbar'
import { ratioPercent } from '@/utils/format'

/** Progresso pagas/total do plano — somente apresentação dos contadores devolvidos pela API. */
const props = defineProps<{ paidCount: number; installmentCount: number; description: string }>()

const percent = computed(() => Math.round(ratioPercent(props.paidCount, props.installmentCount) ?? 0))
</script>

<template>
  <div class="plan-progress">
    <ProgressBar
      :value="percent"
      :show-value="false"
      :aria-label="`${paidCount} de ${installmentCount} parcelas pagas: ${description}`"
    />
    <small data-testid="plan-progress-text">{{ paidCount }}/{{ installmentCount }} pagas</small>
  </div>
</template>

<style scoped>
.plan-progress {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 7rem;
}

.plan-progress :deep(.p-progressbar) {
  height: 0.5rem;
}

.plan-progress small {
  color: var(--gf-text-muted);
}
</style>
