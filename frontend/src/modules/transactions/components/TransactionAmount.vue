<script setup lang="ts">
import { computed } from 'vue'
import type { TransactionType } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { TRANSACTION_TYPE_LABELS } from '@/utils/labels'

/** Valor com sinal, ícone e texto para leitores de tela: despesa/receita não dependem só da cor. */
const props = defineProps<{ amount: number; type: TransactionType }>()

const isIncome = computed(() => props.type === 'Income')
const formatted = computed(() => `${isIncome.value ? '+' : '−'} ${formatCurrency(props.amount)}`)
</script>

<template>
  <span class="money" :class="isIncome ? 'money--income' : 'money--expense'">
    <i :class="isIncome ? 'pi pi-arrow-up-right' : 'pi pi-arrow-down-right'" aria-hidden="true" />
    <span class="sr-only">{{ TRANSACTION_TYPE_LABELS[type] }}: </span>{{ formatted }}
  </span>
</template>
