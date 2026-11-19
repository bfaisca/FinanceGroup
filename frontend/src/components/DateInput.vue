<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
import type { DateOnlyString } from '@/types/api'
import { parseDateOnly, toDateOnly } from '@/utils/format'

/**
 * Seletor de data com modelo "YYYY-MM-DD" (DateOnly). Converte de/para `Date` local,
 * sem passar por UTC, para nunca deslocar o dia.
 */
const props = withDefaults(
  defineProps<{
    id: string
    describedBy?: string
    invalid?: boolean
    min?: DateOnlyString | null
    max?: DateOnlyString | null
    disabled?: boolean
    placeholder?: string
  }>(),
  { describedBy: undefined, invalid: false, min: null, max: null, disabled: false, placeholder: 'dd/mm/aaaa' },
)

const model = defineModel<DateOnlyString | null>({ required: true })

const dateValue = computed<Date | null>({
  get: () => (model.value ? parseDateOnly(model.value) : null),
  set: (value) => {
    model.value = value instanceof Date ? toDateOnly(value) : null
  },
})

const minDate = computed(() => (props.min ? (parseDateOnly(props.min) ?? undefined) : undefined))
const maxDate = computed(() => (props.max ? (parseDateOnly(props.max) ?? undefined) : undefined))
const pt = computed(() => ({
  pcInputText: { root: { 'aria-describedby': props.describedBy, 'aria-invalid': props.invalid } },
}))
</script>

<template>
  <DatePicker
    v-model="dateValue"
    :input-id="id"
    date-format="dd/mm/yy"
    :placeholder="placeholder"
    :min-date="minDate"
    :max-date="maxDate"
    :invalid="invalid"
    :disabled="disabled"
    :pt="pt"
    show-icon
    icon-display="button"
    fluid
  />
</template>
