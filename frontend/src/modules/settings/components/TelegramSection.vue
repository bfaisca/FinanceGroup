<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { toAppError } from '@/utils/problem'
import { telegramService, type TelegramLinkCode, type TelegramLinkStatus } from '../services/telegramService'

const toast = useToast()

const status = ref<TelegramLinkStatus | null>(null)
const code = ref<TelegramLinkCode | null>(null)
const loading = ref(true)
const busy = ref(false)
const loadError = ref<string | null>(null)
const now = ref(Date.now())
let timer: ReturnType<typeof setInterval> | undefined

const secondsLeft = computed(() => {
  if (!code.value) return 0
  return Math.max(0, Math.floor((new Date(code.value.expiresAt).getTime() - now.value) / 1000))
})
const codeActive = computed(() => code.value !== null && secondsLeft.value > 0)
const countdown = computed(() => {
  const minutes = Math.floor(secondsLeft.value / 60)
  const seconds = String(secondsLeft.value % 60).padStart(2, '0')
  return `${minutes}:${seconds}`
})
const linkedAtText = computed(() =>
  status.value?.linkedAt ? new Date(status.value.linkedAt).toLocaleDateString('pt-BR') : '',
)

async function load(): Promise<void> {
  loading.value = true
  loadError.value = null
  try {
    status.value = await telegramService.status()
    if (status.value.linked) code.value = null
  } catch (error) {
    loadError.value = toAppError(error).message
  } finally {
    loading.value = false
  }
}

async function generate(): Promise<void> {
  busy.value = true
  try {
    code.value = await telegramService.createCode()
    now.value = Date.now()
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível gerar o código',
      detail: toAppError(error).message,
      life: 5000,
    })
  } finally {
    busy.value = false
  }
}

async function unlink(): Promise<void> {
  busy.value = true
  try {
    await telegramService.unlink()
    code.value = null
    toast.add({ severity: 'success', summary: 'Telegram desvinculado', life: 4000 })
    await load()
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível desvincular',
      detail: toAppError(error).message,
      life: 5000,
    })
  } finally {
    busy.value = false
  }
}

onMounted(() => {
  void load()
  timer = setInterval(() => {
    now.value = Date.now()
    // Depois que o código é usado no bot, o vínculo aparece ao recarregar o estado.
  }, 1000)
})
onBeforeUnmount(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <section class="surface-card" aria-labelledby="telegram-title" data-testid="telegram-section">
    <h2 id="telegram-title" class="settings-section__title">Telegram</h2>
    <p class="settings-section__description">
      Lance despesas pessoais pelo Telegram: envie o valor e a descrição (por exemplo, “45,90 mercado”) e escolha a
      categoria. A despesa entra na data de hoje, como pendente e sem conta vinculada.
    </p>

    <p v-if="loading" class="settings-section__description" role="status">Carregando…</p>

    <div v-else-if="loadError" role="alert">
      <p class="settings-section__description">Não foi possível carregar o estado do Telegram: {{ loadError }}</p>
      <div class="form-actions">
        <Button label="Tentar novamente" icon="pi pi-refresh" outlined @click="load" />
      </div>
    </div>

    <p v-else-if="status && !status.configured" class="settings-section__description" data-testid="telegram-disabled">
      O bot do Telegram não está habilitado neste ambiente.
    </p>

    <template v-else-if="status?.linked">
      <p class="settings-section__description" data-testid="telegram-linked">
        Telegram vinculado desde {{ linkedAtText }}. Envie uma despesa ao bot para lançá-la.
      </p>
      <div class="form-actions">
        <Button
          label="Desvincular Telegram"
          icon="pi pi-link"
          severity="danger"
          outlined
          :loading="busy"
          @click="unlink"
        />
      </div>
    </template>

    <template v-else-if="status">
      <ol class="telegram-steps">
        <li>Gere um código de vínculo (vale por 10 minutos e só pode ser usado uma vez).</li>
        <li>
          Abra o bot <strong>@{{ status.botUsername }}</strong> no Telegram e envie <code>/vincular CÓDIGO</code>, ou
          use o botão “Abrir no Telegram”.
        </li>
        <li>Volte aqui e atualize para ver o vínculo.</li>
      </ol>

      <div v-if="codeActive && code" class="telegram-code" data-testid="telegram-code">
        <span class="telegram-code__value" aria-label="Código de vínculo">{{ code.code }}</span>
        <span class="telegram-code__expires">expira em {{ countdown }}</span>
      </div>
      <p v-else-if="code" class="settings-section__description" role="status">O código expirou. Gere um novo.</p>

      <div class="form-actions telegram-actions">
        <Button
          :label="codeActive ? 'Gerar novo código' : 'Gerar código'"
          icon="pi pi-key"
          :loading="busy"
          @click="generate"
        />
        <a
          v-if="codeActive && code"
          :href="code.deepLink"
          target="_blank"
          rel="noopener noreferrer"
          class="p-button p-component p-button-outlined telegram-open"
        >
          <span class="p-button-label">Abrir no Telegram</span>
        </a>
        <Button label="Já vinculei" icon="pi pi-refresh" text :disabled="busy" @click="load" />
      </div>
    </template>
  </section>
</template>

<style scoped>
.telegram-steps {
  margin: 0 0 1rem;
  padding-left: 1.25rem;
  display: grid;
  gap: 0.375rem;
}

.telegram-code {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.telegram-code__value {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 1.75rem;
  font-weight: 700;
  letter-spacing: 0.15em;
}

.telegram-code__expires {
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}

.telegram-actions {
  flex-wrap: wrap;
}

.telegram-open {
  text-decoration: none;
}
</style>
