<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import { useToast } from 'primevue/usetoast'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { formatDateTime } from '@/utils/format'
import { NAME_MAX, validateName } from '@/utils/validation'

const auth = useAuthStore()
const toast = useToast()

const form = reactive({ name: auth.user?.name ?? '' })
const { errors, clear, setLocal, setServer } = useFormErrors(['name'] as const)
const loading = ref(false)

watch(
  () => auth.user?.name,
  (name) => {
    if (name !== undefined) form.name = name
  },
)

async function onSubmit(): Promise<void> {
  if (!setLocal({ name: validateName(form.name) })) return
  loading.value = true
  try {
    await auth.updateProfile({ name: form.name.trim() })
    toast.add({ severity: 'success', summary: 'Perfil atualizado', detail: 'Seu nome foi alterado.', life: 3000 })
  } catch (error) {
    const appError = toAppError(error)
    setServer(appError.fieldErrors)
    toast.add({ severity: 'error', summary: 'Não foi possível salvar', detail: appError.message, life: 5000 })
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <section class="surface-card" aria-labelledby="profile-title">
    <h2 id="profile-title" class="settings-section__title">Perfil</h2>
    <p class="settings-section__description">Atualize como seu nome aparece no GroupFinance.</p>

    <form class="form-stack" novalidate @submit.prevent="onSubmit">
      <FormField id="profile-name" v-slot="{ id, describedBy, invalid }" label="Nome" :error="errors.name" required>
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

      <FormField
        id="profile-email"
        v-slot="{ id, describedBy }"
        label="E-mail"
        hint="A alteração de e-mail ainda não está disponível."
      >
        <InputText :id="id" :model-value="auth.user?.email ?? ''" readonly :aria-describedby="describedBy" fluid />
      </FormField>

      <p v-if="auth.user" class="settings-section__meta">Conta criada em {{ formatDateTime(auth.user.createdAt) }}</p>

      <div class="form-actions">
        <Button type="submit" label="Salvar alterações" icon="pi pi-check" :loading="loading" />
      </div>
    </form>
  </section>
</template>

<style src="../settings-section.css" scoped></style>
