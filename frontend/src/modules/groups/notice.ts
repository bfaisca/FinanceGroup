/**
 * Avisos estáticos (regras, privacidade, grupo arquivado) não devem ser anunciados como alerta
 * assertivo por leitores de tela: o `Message` do PrimeVue usa `role="alert"` por padrão.
 * Erros dinâmicos continuam como `alert`.
 */
export const NOTE_PT = { root: { role: 'note', 'aria-live': 'off' } } as const
