<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import { formatDate } from '@/utils/format'
import { formatMonthLong, isFullMonth, monthPeriod, shiftMonth, validatePeriod, type Period } from '@/utils/period'

const props = defineProps<{ modelValue: Period; today: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: Period] }>()

const fullMonth = computed(() => isFullMonth(props.modelValue))
const label = computed(() =>
  fullMonth.value
    ? formatMonthLong(props.modelValue.from.slice(0, 7))
    : `${formatDate(props.modelValue.from)} a ${formatDate(props.modelValue.to)}`,
)
const isCurrentMonth = computed(() => {
  const current = monthPeriod(props.today)
  return props.modelValue.from === current.from && props.modelValue.to === current.to
})

const customOpen = ref(false)
const draft = ref<{ from: string | null; to: string | null }>({ from: props.modelValue.from, to: props.modelValue.to })
const customError = ref<string | undefined>()

watch(
  () => props.modelValue,
  (value) => {
    draft.value = { from: value.from, to: value.to }
  },
)

function step(delta: number): void {
  // Navegação por mês parte do início do período atual.
  emit('update:modelValue', shiftMonth(props.modelValue, delta))
}

function goToCurrent(): void {
  emit('update:modelValue', monthPeriod(props.today))
}

function applyCustom(): void {
  const error = validatePeriod({ from: draft.value.from ?? undefined, to: draft.value.to ?? undefined })
  customError.value = error
  if (error) return
  emit('update:modelValue', { from: draft.value.from as string, to: draft.value.to as string })
}
</script>

<template>
  <section class="period" aria-label="Período">
    <div class="period__nav">
      <Button
        type="button"
        icon="pi pi-chevron-left"
        severity="secondary"
        outlined
        rounded
        aria-label="Mês anterior"
        :disabled="!fullMonth"
        @click="step(-1)"
      />
      <p class="period__label" aria-live="polite" data-testid="period-label">{{ label }}</p>
      <Button
        type="button"
        icon="pi pi-chevron-right"
        severity="secondary"
        outlined
        rounded
        aria-label="Próximo mês"
        :disabled="!fullMonth"
        @click="step(1)"
      />
      <Button
        type="button"
        label="Mês atual"
        severity="secondary"
        text
        :disabled="isCurrentMonth"
        @click="goToCurrent"
      />
      <Button
        type="button"
        label="Personalizado"
        icon="pi pi-calendar"
        severity="secondary"
        text
        aria-controls="period-custom"
        :aria-expanded="customOpen"
        @click="customOpen = !customOpen"
      />
    </div>

    <form v-if="customOpen" id="period-custom" class="period__custom" novalidate @submit.prevent="applyCustom">
      <FormField id="period-from" v-slot="{ id, describedBy, invalid }" label="De" :error="customError">
        <DateInput :id="id" v-model="draft.from" :described-by="describedBy" :invalid="invalid" />
      </FormField>
      <FormField id="period-to" v-slot="{ id }" label="Até">
        <DateInput :id="id" v-model="draft.to" :min="draft.from" />
      </FormField>
      <Button type="submit" label="Aplicar" icon="pi pi-check" />
    </form>
  </section>
</template>

<style scoped>
.period__nav {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.period__label {
  margin: 0;
  min-width: 10rem;
  text-align: center;
  font-weight: 600;
  text-transform: lowercase;
}

.period__label::first-letter {
  text-transform: uppercase;
}

.period__custom {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.period__custom > :last-child {
  align-self: flex-end;
}
</style>
