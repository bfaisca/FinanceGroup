/** Opções simples de cor e ícone para categorias (ícones PrimeIcons; cores `#RRGGBB`). */
export interface ColorOption {
  value: string
  label: string
}

export interface IconOption {
  value: string
  label: string
}

export const CATEGORY_COLORS: readonly ColorOption[] = [
  { value: '#0f766e', label: 'Verde-azulado' },
  { value: '#2563eb', label: 'Azul' },
  { value: '#4f46e5', label: 'Índigo' },
  { value: '#9333ea', label: 'Roxo' },
  { value: '#db2777', label: 'Rosa' },
  { value: '#dc2626', label: 'Vermelho' },
  { value: '#ea580c', label: 'Laranja' },
  { value: '#d97706', label: 'Âmbar' },
  { value: '#16a34a', label: 'Verde' },
  { value: '#64748b', label: 'Cinza' },
]

export const CATEGORY_ICONS: readonly IconOption[] = [
  { value: 'pi-shopping-cart', label: 'Carrinho' },
  { value: 'pi-home', label: 'Casa' },
  { value: 'pi-car', label: 'Carro' },
  { value: 'pi-heart', label: 'Coração' },
  { value: 'pi-book', label: 'Livro' },
  { value: 'pi-ticket', label: 'Ingresso' },
  { value: 'pi-wallet', label: 'Carteira' },
  { value: 'pi-money-bill', label: 'Dinheiro' },
  { value: 'pi-gift', label: 'Presente' },
  { value: 'pi-briefcase', label: 'Maleta' },
  { value: 'pi-mobile', label: 'Celular' },
  { value: 'pi-wifi', label: 'Internet' },
  { value: 'pi-bolt', label: 'Energia' },
  { value: 'pi-globe', label: 'Viagem' },
  { value: 'pi-star', label: 'Estrela' },
  { value: 'pi-tag', label: 'Etiqueta' },
  { value: 'pi-credit-card', label: 'Cartão' },
  { value: 'pi-chart-line', label: 'Gráfico' },
  { value: 'pi-graduation-cap', label: 'Estudos' },
  { value: 'pi-shop', label: 'Loja' },
]

const COLOR_PATTERN = /^#[0-9a-fA-F]{6}$/

export function isValidColor(value: string): boolean {
  return COLOR_PATTERN.test(value)
}
