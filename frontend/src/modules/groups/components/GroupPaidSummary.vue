<script setup lang="ts">
import { formatCurrency } from '@/utils/format'

/**
 * Valor original × efetivamente pago (D-048): "Original R$ 500,00 · Pago R$ 523,50 (+ juros R$ 23,50)".
 * `paid` é o total efetivamente pago (principal + juros); `interest` é a parte de juros/multa.
 */
const props = withDefaults(defineProps<{ amount: number; paid: number; interest: number; withOriginal?: boolean }>(), {
  withOriginal: true,
})
</script>

<template>
  <span class="paid-summary" data-testid="paid-summary">
    <span v-if="props.withOriginal"
      >Original <strong class="money">{{ formatCurrency(props.amount) }}</strong></span
    >
    <span v-if="props.withOriginal && props.paid > 0" aria-hidden="true"> · </span>
    <span v-if="props.paid > 0"
      >Pago <strong class="money">{{ formatCurrency(props.paid) }}</strong>
      <span v-if="props.interest > 0" class="paid-summary__interest"
        >(+ juros {{ formatCurrency(props.interest) }})</span
      ></span
    >
  </span>
</template>

<style scoped>
.paid-summary__interest {
  color: var(--gf-text-muted);
}
</style>
