<script setup lang="ts">
import { reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Message from 'primevue/message'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import {
  NAME_MAX,
  PASSWORD_MAX,
  normalizeEmail,
  validateEmail,
  validateName,
  validatePasswordConfirmation,
  validatePasswordPolicy,
} from '@/utils/validation'
import type { RegisterRequest } from '@/types/api'

const props = withDefaults(
  defineProps<{
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: RegisterRequest] }>()

const form = reactive({ name: '', email: '', password: '', confirmPassword: '' })
const { errors, clear, setLocal, setServer } = useFormErrors(['name', 'email', 'password', 'confirmPassword'] as const)

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

function onSubmit(): void {
  const valid = setLocal({
    name: validateName(form.name),
    email: validateEmail(form.email),
    password: validatePasswordPolicy(form.password),
    confirmPassword: validatePasswordConfirmation(form.password, form.confirmPassword),
  })
  if (!valid) return
  emit('submit', { name: form.name.trim(), email: normalizeEmail(form.email), password: form.password })
}
</script>

<template>
  <form class="form-stack" novalidate aria-label="Criar conta" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert">{{ errorMessage }}</Message>

    <FormField id="register-name" v-slot="{ id, describedBy, invalid }" label="Nome" :error="errors.name" required>
      <InputText
        :id="id"
        v-model="form.name"
        name="name"
        autocomplete="name"
        :maxlength="NAME_MAX"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('name')"
      />
    </FormField>

    <FormField id="register-email" v-slot="{ id, describedBy, invalid }" label="E-mail" :error="errors.email" required>
      <InputText
        :id="id"
        v-model="form.email"
        type="email"
        name="email"
        autocomplete="email"
        inputmode="email"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('email')"
      />
    </FormField>

    <FormField
      id="register-password"
      v-slot="{ id, describedBy, invalid }"
      label="Senha"
      hint="De 8 a 128 caracteres, com ao menos uma letra e um número."
      :error="errors.password"
      required
    >
      <Password
        v-model="form.password"
        :input-id="id"
        name="password"
        :feedback="false"
        toggle-mask
        :invalid="invalid"
        :input-props="{
          autocomplete: 'new-password',
          maxlength: PASSWORD_MAX,
          'aria-describedby': describedBy,
          'aria-invalid': invalid,
        }"
        fluid
        @input="clear('password')"
      />
    </FormField>

    <FormField
      id="register-confirm-password"
      v-slot="{ id, describedBy, invalid }"
      label="Confirmar senha"
      :error="errors.confirmPassword"
      required
    >
      <Password
        v-model="form.confirmPassword"
        :input-id="id"
        name="confirmPassword"
        :feedback="false"
        toggle-mask
        :invalid="invalid"
        :input-props="{ autocomplete: 'new-password', 'aria-describedby': describedBy, 'aria-invalid': invalid }"
        fluid
        @input="clear('confirmPassword')"
      />
    </FormField>

    <Button type="submit" label="Criar conta" icon="pi pi-user-plus" :loading="loading" fluid />
  </form>
</template>
