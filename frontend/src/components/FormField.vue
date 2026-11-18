<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    /** `id` do controle; o label aponta para ele. */
    id: string
    label: string
    error?: string | string[]
    hint?: string
    required?: boolean
  }>(),
  { error: undefined, hint: undefined, required: false },
)

const errorMessage = computed(() => (Array.isArray(props.error) ? props.error[0] : props.error))
const errorId = computed(() => `${props.id}-error`)
const hintId = computed(() => `${props.id}-hint`)
const describedBy = computed(
  () =>
    [props.hint ? hintId.value : null, errorMessage.value ? errorId.value : null].filter(Boolean).join(' ') ||
    undefined,
)
const invalid = computed(() => Boolean(errorMessage.value))
</script>

<template>
  <div class="form-field" :class="{ 'form-field--invalid': invalid }">
    <label class="form-field__label" :for="id">
      {{ label }}<span v-if="required" class="form-field__required" aria-hidden="true"> *</span>
    </label>
    <slot :id="id" :described-by="describedBy" :invalid="invalid" />
    <small v-if="hint" :id="hintId" class="form-field__hint">{{ hint }}</small>
    <small v-if="errorMessage" :id="errorId" class="form-field__error" aria-live="polite">
      <i class="pi pi-exclamation-circle" aria-hidden="true" /> {{ errorMessage }}
    </small>
  </div>
</template>

<style scoped>
.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.form-field__label {
  font-weight: 500;
  font-size: 0.9375rem;
}

.form-field__required {
  color: var(--gf-danger);
}

.form-field__hint {
  color: var(--gf-text-muted);
}

.form-field__error {
  color: var(--gf-danger);
  display: flex;
  align-items: center;
  gap: 0.25rem;
}
</style>
