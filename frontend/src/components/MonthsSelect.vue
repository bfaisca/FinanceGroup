<script setup lang="ts">
import SelectButton from 'primevue/selectbutton'

/** Janela em meses dos relatórios (6/12/24/36). */
withDefaults(defineProps<{ label?: string; options?: readonly number[]; ariaLabel?: string }>(), {
  label: 'Janela',
  options: () => [6, 12, 24, 36],
  ariaLabel: 'Quantidade de meses',
})
const model = defineModel<number>({ required: true })
</script>

<template>
  <div class="months-select">
    <span class="months-select__label" aria-hidden="true">{{ label }}</span>
    <SelectButton
      :model-value="model"
      :options="options.map((m) => ({ value: m, label: `${m} meses` }))"
      option-label="label"
      option-value="value"
      :allow-empty="false"
      :aria-label="ariaLabel"
      @update:model-value="(v: number | null) => v !== null && (model = v)"
    />
  </div>
</template>

<style scoped>
.months-select {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 0.75rem;
  align-items: center;
}

.months-select__label {
  font-weight: 500;
}
</style>
