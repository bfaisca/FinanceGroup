import { computed, ref } from 'vue'
import { DARK_MODE_CLASS } from '@/plugins/primevue'

export type ThemeMode = 'light' | 'dark'

const STORAGE_KEY = 'gf-theme'
const mode = ref<ThemeMode>('light')
let initialized = false

function readStored(): ThemeMode | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

function writeStored(value: ThemeMode): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, value)
  } catch {
    // Armazenamento indisponível (modo privado, cota): mantém apenas em memória.
  }
}

function prefersDark(): boolean {
  try {
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false
  } catch {
    return false
  }
}

function apply(value: ThemeMode): void {
  document.documentElement.classList.toggle(DARK_MODE_CLASS, value === 'dark')
  document.documentElement.style.colorScheme = value
}

/** Inicializa o tema a partir da preferência salva ou do sistema operacional. */
export function initTheme(): void {
  if (initialized) return
  initialized = true
  mode.value = readStored() ?? (prefersDark() ? 'dark' : 'light')
  apply(mode.value)
}

export function useTheme() {
  initTheme()

  const isDark = computed(() => mode.value === 'dark')

  function setTheme(value: ThemeMode): void {
    mode.value = value
    apply(value)
    writeStored(value)
  }

  function toggleTheme(): void {
    setTheme(isDark.value ? 'light' : 'dark')
  }

  return { mode: computed(() => mode.value), isDark, setTheme, toggleTheme }
}
