/** Ambiente de testes: jsdom não implementa `matchMedia` (usado pelo PrimeVue e por useMediaQuery). */
if (typeof window !== 'undefined' && typeof window.matchMedia !== 'function') {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false,
    }) as MediaQueryList
}

if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  } as unknown as typeof ResizeObserver
}

import { afterEach } from 'vitest'

// Diálogos/drawers do PrimeVue são teleportados para o <body>: limpa entre testes.
afterEach(() => {
  document.body.innerHTML = ''
})
