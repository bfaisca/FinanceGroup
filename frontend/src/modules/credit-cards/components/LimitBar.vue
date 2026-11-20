<script setup lang="ts">
import { computed } from 'vue'
import { formatCurrency, formatPercent } from '@/utils/format'
import { limitPresentation } from '../limit'

/**
 * Barra de limite do cartão: utilizado × limite com valores e percentual.
 * "Limite excedido" (`availableLimit < 0`) é comunicado por ícone e texto, não só por cor.
 */
const props = defineProps<{
  usedLimit: number
  creditLimit: number
  availableLimit: number
  /** Id do rótulo externo (ex.: nome do cartão) para o `aria-labelledby` da barra. */
  labelledBy?: string
}>()

const presentation = computed(() => limitPresentation(props.usedLimit, props.creditLimit, props.availableLimit))
const exceeded = computed(() => props.availableLimit < 0)
const percentText = computed(() =>
  presentation.value.percent === null ? null : formatPercent(presentation.value.percent),
)
const summary = computed(
  () =>
    `Utilizado ${formatCurrency(props.usedLimit)} de ${formatCurrency(props.creditLimit)}` +
    (percentText.value ? ` (${percentText.value})` : ''),
)
</script>

<template>
  <div class="limit-bar" :class="`limit-bar--${presentation.state}`" data-testid="limit-bar">
    <div
      class="limit-bar__track"
      role="progressbar"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-valuenow="Math.round(presentation.barValue)"
      :aria-valuetext="summary"
      :aria-labelledby="labelledBy"
      :aria-label="labelledBy ? undefined : 'Limite utilizado'"
    >
      <div class="limit-bar__fill" :style="{ width: `${presentation.barValue}%` }" />
    </div>
    <p class="limit-bar__summary">
      <span>
        Utilizado <strong class="money">{{ formatCurrency(usedLimit) }}</strong> de
        <strong class="money">{{ formatCurrency(creditLimit) }}</strong>
        <template v-if="percentText"> ({{ percentText }})</template>
      </span>
    </p>
    <p v-if="exceeded" class="limit-bar__alert" data-testid="limit-exceeded">
      <i class="pi pi-exclamation-triangle" aria-hidden="true" />
      Limite excedido em {{ formatCurrency(Math.abs(availableLimit)) }}
    </p>
    <p v-else class="limit-bar__available" data-testid="limit-available">
      Disponível <strong class="money">{{ formatCurrency(availableLimit) }}</strong>
    </p>
  </div>
</template>

<style scoped>
.limit-bar {
  --fill: var(--gf-info);
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.limit-bar--high {
  --fill: var(--gf-warning);
}

.limit-bar--exceeded {
  --fill: var(--gf-danger);
}

.limit-bar__track {
  height: 0.625rem;
  border-radius: 999px;
  background: color-mix(in srgb, var(--gf-neutral) 20%, transparent);
  overflow: hidden;
}

.limit-bar__fill {
  height: 100%;
  border-radius: 999px;
  background: var(--fill);
}

.limit-bar--exceeded .limit-bar__fill {
  /* Listras: reforço visual além da cor. */
  background: repeating-linear-gradient(
    135deg,
    var(--fill) 0,
    var(--fill) 6px,
    color-mix(in srgb, var(--fill) 60%, transparent) 6px,
    color-mix(in srgb, var(--fill) 60%, transparent) 12px
  );
}

.limit-bar__summary,
.limit-bar__available,
.limit-bar__alert {
  margin: 0;
  font-size: 0.875rem;
}

.limit-bar__available {
  color: var(--gf-text-muted);
}

.limit-bar__alert {
  display: flex;
  gap: 0.375rem;
  align-items: center;
  color: var(--gf-danger);
  font-weight: 600;
}
</style>
