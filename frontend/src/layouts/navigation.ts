/** Itens do menu principal (CLAUDE.md §14). Todos devem estar disponíveis em qualquer tamanho de tela. */
export interface NavItem {
  label: string
  icon: string
  routeName: string
}

export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Dashboard', icon: 'pi pi-home', routeName: 'dashboard' },
  { label: 'Contas', icon: 'pi pi-building-columns', routeName: 'accounts' },
  { label: 'Cartões', icon: 'pi pi-credit-card', routeName: 'credit-cards' },
  { label: 'Movimentações', icon: 'pi pi-arrow-right-arrow-left', routeName: 'transactions' },
  { label: 'Categorias', icon: 'pi pi-tags', routeName: 'categories' },
  { label: 'Parcelamentos', icon: 'pi pi-list-check', routeName: 'installments' },
  { label: 'Recorrências', icon: 'pi pi-sync', routeName: 'recurrences' },
  { label: 'Grupos', icon: 'pi pi-users', routeName: 'groups' },
  { label: 'Relatórios', icon: 'pi pi-chart-bar', routeName: 'reports' },
  { label: 'Configurações', icon: 'pi pi-cog', routeName: 'settings' },
]
