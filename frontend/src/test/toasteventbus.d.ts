/** O PrimeVue não publica tipos para o barramento de toasts; os testes o usam para observar `toast.add`. */
declare module 'primevue/toasteventbus' {
  interface ToastMessage {
    severity: string
    summary: string
    detail?: string
  }
  const ToastEventBus: {
    on(type: 'add', handler: (message: ToastMessage) => void): void
    off(type: 'add', handler: (message: ToastMessage) => void): void
  }
  export default ToastEventBus
}
