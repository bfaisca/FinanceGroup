import type { App } from 'vue'
import PrimeVue from 'primevue/config'
import ToastService from 'primevue/toastservice'
import ConfirmationService from 'primevue/confirmationservice'
import Tooltip from 'primevue/tooltip'
import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'

/** Classe aplicada em <html> para o modo escuro (ver composables/useTheme). */
export const DARK_MODE_CLASS = 'app-dark'

/** Preset Aura com cor primária própria (teal) — D-001. */
export const GroupFinancePreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{teal.50}',
      100: '{teal.100}',
      200: '{teal.200}',
      300: '{teal.300}',
      400: '{teal.400}',
      500: '{teal.500}',
      600: '{teal.600}',
      700: '{teal.700}',
      800: '{teal.800}',
      900: '{teal.900}',
      950: '{teal.950}',
    },
    colorScheme: {
      light: {
        // Primário mais escuro no claro para contraste AA de texto/botões (≥ 4.5:1 sobre branco).
        primary: {
          color: '{teal.700}',
          contrastColor: '#ffffff',
          hoverColor: '{teal.800}',
          activeColor: '{teal.900}',
        },
      },
      dark: {
        primary: {
          color: '{teal.300}',
          contrastColor: '{teal.950}',
          hoverColor: '{teal.200}',
          activeColor: '{teal.100}',
        },
      },
    },
  },
})

/** Textos dos componentes PrimeVue em pt-BR. */
const ptBR = {
  accept: 'Sim',
  reject: 'Não',
  choose: 'Escolher',
  upload: 'Enviar',
  cancel: 'Cancelar',
  clear: 'Limpar',
  apply: 'Aplicar',
  passwordPrompt: 'Digite uma senha',
  weak: 'Fraca',
  medium: 'Média',
  strong: 'Forte',
  emptyMessage: 'Nenhum resultado encontrado',
  emptyFilterMessage: 'Nenhum resultado encontrado',
  searchMessage: '{0} resultados disponíveis',
  selectionMessage: '{0} itens selecionados',
  emptySelectionMessage: 'Nenhum item selecionado',
  emptySearchMessage: 'Nenhum resultado encontrado',
  today: 'Hoje',
  dateFormat: 'dd/mm/yy',
  firstDayOfWeek: 0,
  dayNames: ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'],
  dayNamesShort: ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'],
  dayNamesMin: ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'],
  monthNames: [
    'Janeiro',
    'Fevereiro',
    'Março',
    'Abril',
    'Maio',
    'Junho',
    'Julho',
    'Agosto',
    'Setembro',
    'Outubro',
    'Novembro',
    'Dezembro',
  ],
  monthNamesShort: ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'],
  aria: {
    close: 'Fechar',
    showPassword: 'Mostrar senha',
    hidePassword: 'Ocultar senha',
  },
}

export function installPrimeVue(app: App): void {
  app.use(PrimeVue, {
    theme: {
      preset: GroupFinancePreset,
      options: {
        darkModeSelector: `.${DARK_MODE_CLASS}`,
        cssLayer: false,
      },
    },
    locale: ptBR,
    ripple: false,
  })
  app.use(ToastService)
  app.use(ConfirmationService)
  app.directive('tooltip', Tooltip)
}
