<script setup lang="ts">
import PageHeader from '@/components/PageHeader.vue'
import ProfileForm from '../components/ProfileForm.vue'
import ChangePasswordForm from '../components/ChangePasswordForm.vue'
import DeleteAccountSection from '../components/DeleteAccountSection.vue'
</script>

<template>
  <div>
    <PageHeader title="Configurações" description="Gerencie seu perfil, sua senha e sua conta." />
    <div class="settings-grid">
      <ProfileForm />
      <ChangePasswordForm />
      <DeleteAccountSection />
    </div>
  </div>
</template>

<style scoped>
.settings-grid {
  display: grid;
  gap: 1.5rem;
  max-width: 48rem;
}
</style>
