import 'vue-router'

export {}

declare module 'vue-router' {
  interface RouteMeta {
    /** Título exibido em `document.title`. */
    title?: string
    /** Exige sessão autenticada. */
    requiresAuth?: boolean
    /** Somente para visitantes (login, cadastro…): usuário logado vai para o dashboard. */
    guestOnly?: boolean
  }
}
