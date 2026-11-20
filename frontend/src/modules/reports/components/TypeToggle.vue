<script setup lang="ts">
import SelectButton from 'primevue/selectbutton'
import type { TransactionType } from '@/types/api'

/** Alterna o relatório entre despesas e receitas. */
const model = defineModel<TransactionType>({ required: true })
const options = [
  { value: 'Expense' as TransactionType, label: 'Despesas' },
  { value: 'Income' as TransactionType, label: 'Receitas' },
]
</script>

<template>
  <SelectButton
    :model-value="model"
    :options="options"
    option-label="label"
    option-value="value"
    :allow-empty="false"
    aria-label="Tipo de movimentação do relatório"
    @update:model-value="(v: TransactionType | null) => v && (model = v)"
  />
</template>
