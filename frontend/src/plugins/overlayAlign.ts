/**
 * Correção global do posicionamento dos overlays do PrimeVue (Select, MultiSelect, DatePicker…).
 *
 * MOTIVO: em alguns navegadores/ambientes de usuários a lista de um Select/DatePicker abre colada na BORDA
 * ESQUERDA da tela (`left: 0`) com o `top` correto, em vez de logo abaixo do campo (print enviado pelo usuário
 * na tela de movimentações do grupo). O PrimeVue anexa o overlay ao `body` e calcula `left/top` uma única vez
 * (`absolutePosition`); se essa medição falhar naquele ambiente, o overlay fica mal posicionado e nada o corrige.
 * Não reproduzimos o defeito no ambiente de teste, então esta camada é defensiva e independente da causa.
 *
 * COMO FUNCIONA: um `MutationObserver` no `body` detecta overlays recém-anexados, descobre o campo âncora
 * (via `aria-controls`/`aria-expanded`, com a seleção ativa como alternativa) e, após o layout (dois
 * `requestAnimationFrame`), reposiciona `left/top` (e `min-width`) quando o resultado estiver fora do esperado.
 * Reaplica quando o PrimeVue reposiciona (observador de `style`, com proteção contra laço), no scroll e no
 * resize enquanto o overlay estiver aberto, e libera tudo quando ele é removido.
 *
 * Não usamos `appendTo="self"`: o overlay seria cortado dentro de Dialog/Drawer. Overlays de diálogos também
 * são anexados ao `body`, então a mesma lógica vale para eles.
 */

/** Overlays de campos (anexados ao `body`) que esta correção trata. */
export const OVERLAY_SELECTOR = [
  '.p-select-overlay',
  '.p-multiselect-overlay',
  '.p-datepicker-panel',
  '.p-autocomplete-overlay',
  '.p-treeselect-overlay',
  '.p-cascadeselect-overlay',
].join(',')

/** Raiz do campo: usada como âncora (largura do campo inteiro, não só do input interno). */
export const FIELD_ROOT_SELECTOR =
  '.p-select, .p-multiselect, .p-datepicker, .p-autocomplete, .p-treeselect, .p-cascadeselect'

/** Diferença (px) tolerada antes de considerar o overlay mal posicionado. */
export const POSITION_TOLERANCE = 2

export interface AnchorRect {
  left: number
  top: number
  right: number
  bottom: number
  width: number
  height: number
}

export interface OverlayPositionInput {
  /** Retângulo do campo em coordenadas da janela (`getBoundingClientRect`). */
  anchor: AnchorRect
  /** Tamanho medido do overlay (sem transformações). */
  overlay: { width: number; height: number }
  viewport: { width: number; height: number; scrollX: number; scrollY: number }
  /** Espaço vertical entre o campo e o overlay. */
  gap?: number
}

export interface OverlayPosition {
  /** Coordenadas de DOCUMENTO (somam o scroll), como `position: absolute` em um `body` estático. */
  left: number
  top: number
  minWidth: number
  /** Só quando o overlay seria mais largo que a janela. */
  maxWidth: number | null
  placement: 'below' | 'above'
}

/**
 * Posição esperada do overlay (função pura):
 * - horizontal: alinhado à esquerda do campo; se não couber, alinhado à direita do campo; nunca fora da janela à esquerda;
 * - vertical: logo abaixo do campo; se não couber embaixo e houver mais espaço em cima, logo acima;
 * - `min-width` = largura do campo.
 */
export function computeOverlayPosition({ anchor, overlay, viewport, gap = 0 }: OverlayPositionInput): OverlayPosition {
  const minWidth = Math.max(0, Math.round(anchor.width))
  const tooWide = overlay.width > viewport.width
  const width = tooWide ? viewport.width : Math.max(overlay.width, minWidth)
  const minLeft = Math.max(viewport.scrollX, 0)

  let left = anchor.left + viewport.scrollX
  if (left + width > viewport.scrollX + viewport.width) left = anchor.right + viewport.scrollX - width
  left = Math.max(left, minLeft)

  const spaceBelow = viewport.height - anchor.bottom - gap
  const spaceAbove = anchor.top - gap
  const fitsBelow = overlay.height <= spaceBelow
  const placement = !fitsBelow && spaceAbove > spaceBelow ? 'above' : 'below'
  const top =
    placement === 'below'
      ? anchor.bottom + viewport.scrollY + gap
      : Math.max(anchor.top + viewport.scrollY - overlay.height - gap, Math.max(viewport.scrollY, 0))

  return { left, top, minWidth, maxWidth: tooWide ? viewport.width : null, placement }
}

/** Campo âncora do overlay: o controle com `aria-expanded="true"` cujo `aria-controls` aponta para o overlay. */
export function findOverlayAnchor(overlay: HTMLElement, doc: Document = document): HTMLElement | null {
  for (const candidate of Array.from(doc.querySelectorAll<HTMLElement>('[aria-expanded="true"][aria-controls]'))) {
    const target = doc.getElementById(candidate.getAttribute('aria-controls') ?? '')
    if (target && (target === overlay || overlay.contains(target))) {
      return candidate.closest<HTMLElement>(FIELD_ROOT_SELECTOR) ?? candidate
    }
  }
  const active = doc.activeElement
  return active instanceof HTMLElement ? active.closest<HTMLElement>(FIELD_ROOT_SELECTOR) : null
}

function pixel(value: string): number | null {
  const n = Number.parseFloat(value)
  return Number.isFinite(n) && /px$|^-?\d+(\.\d+)?$/.test(value.trim()) ? n : null
}

interface Tracked {
  overlay: HTMLElement
  schedule: (frames: 1 | 2) => void
  dispose: () => void
}

export interface InstallOptions {
  /** Janela/documento alvo (testes). */
  win?: Window & typeof globalThis
}

/**
 * Instala a correção. Retorna a função que desfaz tudo (observadores, listeners e overlays acompanhados).
 */
export function installOverlayAlign(options: InstallOptions = {}): () => void {
  const win = options.win ?? window
  const doc = win.document
  const tracked = new Map<HTMLElement, Tracked>()

  function track(overlay: HTMLElement): void {
    if (tracked.has(overlay)) return
    let pending = 0
    let disposed = false

    // O PrimeVue reposiciona (alterando `style`) ao abrir e em resize/scroll: reaplicamos depois dele.
    const styleObserver = new win.MutationObserver(() => schedule(1))
    styleObserver.observe(overlay, { attributes: true, attributeFilter: ['style'] })

    function apply(): void {
      if (disposed || !overlay.isConnected) return
      const anchorEl = findOverlayAnchor(overlay, doc)
      if (!anchorEl) return

      const anchorRect = anchorEl.getBoundingClientRect()
      const overlayRect = overlay.getBoundingClientRect()
      const size = {
        // `offsetWidth/Height` ignoram transformações (animação de entrada do PrimeVue).
        width: overlay.offsetWidth || overlayRect.width,
        height: overlay.offsetHeight || overlayRect.height,
      }
      const viewport = {
        width: doc.documentElement.clientWidth || win.innerWidth,
        height: doc.documentElement.clientHeight || win.innerHeight,
        scrollX: win.scrollX || 0,
        scrollY: win.scrollY || 0,
      }
      const expected = computeOverlayPosition({ anchor: anchorRect, overlay: size, viewport })

      const currentLeft = pixel(overlay.style.left) ?? overlayRect.left + viewport.scrollX
      const currentTop = pixel(overlay.style.top) ?? overlayRect.top + viewport.scrollY
      const misplaced =
        Math.abs(currentLeft - expected.left) > POSITION_TOLERANCE ||
        Math.abs(currentTop - expected.top) > POSITION_TOLERANCE
      const narrow = overlay.style.minWidth !== `${expected.minWidth}px` && expected.minWidth > 0

      if (!misplaced && !narrow) return

      overlay.style.position = 'absolute'
      overlay.style.left = `${Math.round(expected.left)}px`
      overlay.style.top = `${Math.round(expected.top)}px`
      overlay.style.minWidth = `${expected.minWidth}px`
      if (expected.maxWidth !== null) overlay.style.maxWidth = `${expected.maxWidth}px`
      // Descarta o registro das nossas próprias escritas: evita laço com o observador de `style`.
      styleObserver.takeRecords()
    }

    function schedule(frames: 1 | 2): void {
      if (disposed) return
      const token = ++pending
      const run = () => {
        if (token === pending) apply()
      }
      if (frames === 2) win.requestAnimationFrame(() => win.requestAnimationFrame(run))
      else win.requestAnimationFrame(run)
    }

    const onViewportChange = () => schedule(1)
    win.addEventListener('scroll', onViewportChange, { capture: true, passive: true })
    win.addEventListener('resize', onViewportChange, { passive: true })

    function dispose(): void {
      if (disposed) return
      disposed = true
      styleObserver.disconnect()
      win.removeEventListener('scroll', onViewportChange, { capture: true })
      win.removeEventListener('resize', onViewportChange)
      tracked.delete(overlay)
    }

    tracked.set(overlay, { overlay, schedule, dispose })
    schedule(2)
  }

  function scan(node: Node): void {
    if (!(node instanceof HTMLElement)) return
    if (node.matches(OVERLAY_SELECTOR)) track(node)
    else node.querySelectorAll<HTMLElement>(OVERLAY_SELECTOR).forEach(track)
  }

  const bodyObserver = new win.MutationObserver((records) => {
    for (const record of records) record.addedNodes.forEach(scan)
    // Overlay removido do DOM (fechou): libera observadores e listeners.
    for (const [overlay, entry] of Array.from(tracked)) if (!overlay.isConnected) entry.dispose()
  })
  bodyObserver.observe(doc.body, { childList: true })

  return () => {
    bodyObserver.disconnect()
    for (const entry of Array.from(tracked.values())) entry.dispose()
  }
}
