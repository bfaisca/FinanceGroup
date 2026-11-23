<script setup lang="ts">
import PageHeader from '@/components/PageHeader.vue'
import ProfileForm from '../components/ProfileForm.vue'
import ChangePasswordForm from '../components/ChangePasswordForm.vue'
import TelegramSection from '../components/TelegramSection.vue'
import DeleteAccountSection from '../components/DeleteAccountSection.vue'
</script>

<template>
  <div>
    <PageHeader title="Configurações" description="Gerencie seu perfil, sua senha, o Telegram e sua conta." />
    <div class="settings-grid">
      <ProfileForm />
      <ChangePasswordForm />
      <TelegramSection />
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
