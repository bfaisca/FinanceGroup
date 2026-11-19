import { nextTick, watch, type Ref } from 'vue'

/**
 * Devolve o foco ao elemento que abriu um diálogo/drawer quando ele fecha
 * (o PrimeVue prende o foco enquanto aberto, mas não o restaura).
 */
export function useRestoreFocus(visible: Ref<boolean>): void {
  let previous: HTMLElement | null = null

  watch(visible, (isVisible) => {
    if (isVisible) {
      previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
      return
    }
    const element = previous
    previous = null
    if (element) {
      void nextTick(() => {
        if (element.isConnected) element.focus()
      })
    }
  })
}
