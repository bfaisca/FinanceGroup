import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { computeOverlayPosition, findOverlayAnchor, installOverlayAlign, type AnchorRect } from '../overlayAlign'

const rect = (left: number, top: number, width: number, height: number): AnchorRect => ({
  left,
  top,
  width,
  height,
  right: left + width,
  bottom: top + height,
})

const viewport = { width: 1000, height: 800, scrollX: 0, scrollY: 0 }

describe('computeOverlayPosition', () => {
  it('abre logo abaixo do campo, alinhado à esquerda, com min-width = largura do campo', () => {
    const pos = computeOverlayPosition({
      anchor: rect(120, 200, 300, 40),
      overlay: { width: 300, height: 200 },
      viewport,
    })
    expect(pos).toMatchObject({ left: 120, top: 240, minWidth: 300, maxWidth: null, placement: 'below' })
  })

  it('soma o scroll da página (coordenadas de documento)', () => {
    const pos = computeOverlayPosition({
      anchor: rect(120, 200, 300, 40),
      overlay: { width: 300, height: 200 },
      viewport: { ...viewport, scrollX: 15, scrollY: 400 },
    })
    expect(pos.left).toBe(135)
    expect(pos.top).toBe(640)
  })

  it('alinha à direita do campo quando não cabe à direita', () => {
    const pos = computeOverlayPosition({
      anchor: rect(800, 100, 180, 40),
      overlay: { width: 320, height: 150 },
      viewport,
    })
    // right do campo (980) - largura do overlay (320)
    expect(pos.left).toBe(660)
  })

  it('nunca fica à esquerda de 0, mesmo com overlay mais largo que o espaço', () => {
    const pos = computeOverlayPosition({
      anchor: rect(10, 100, 100, 40),
      overlay: { width: 500, height: 150 },
      viewport: { ...viewport, width: 400 },
    })
    expect(pos.left).toBeGreaterThanOrEqual(0)
    expect(pos.left).toBe(0)
    expect(pos.maxWidth).toBe(400)
  })

  it('usa a largura do campo no cálculo quando o overlay é mais estreito que o campo (min-width)', () => {
    const pos = computeOverlayPosition({
      anchor: rect(700, 100, 290, 40),
      overlay: { width: 100, height: 100 },
      viewport,
    })
    // o overlay terá 290px: 700 + 290 = 990 ≤ 1000 → alinhado à esquerda do campo
    expect(pos.left).toBe(700)
    expect(pos.minWidth).toBe(290)
  })

  it('abre acima quando não cabe embaixo e há mais espaço em cima', () => {
    const pos = computeOverlayPosition({
      anchor: rect(100, 700, 300, 40),
      overlay: { width: 300, height: 300 },
      viewport,
    })
    expect(pos.placement).toBe('above')
    expect(pos.top).toBe(400)
  })

  it('fica embaixo quando não cabe em nenhum dos lados mas há mais espaço embaixo', () => {
    const pos = computeOverlayPosition({
      anchor: rect(100, 100, 300, 40),
      overlay: { width: 300, height: 2000 },
      viewport,
    })
    expect(pos.placement).toBe('below')
    expect(pos.top).toBe(140)
  })

  it('acima nunca passa do topo da página', () => {
    const pos = computeOverlayPosition({
      anchor: rect(100, 450, 300, 40),
      overlay: { width: 300, height: 500 },
      viewport: { ...viewport, height: 600 },
    })
    expect(pos.top).toBeGreaterThanOrEqual(0)
  })
})

describe('findOverlayAnchor', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('encontra o campo pelo aria-controls do combobox aberto (Select)', () => {
    document.body.innerHTML = `
      <div class="p-select" id="field"><span role="combobox" aria-expanded="true" aria-controls="s1_list"></span></div>
      <div class="p-select" id="other"><span role="combobox" aria-expanded="false" aria-controls="s2_list"></span></div>
      <div class="p-select-overlay" id="ov"><ul id="s1_list"></ul></div>`
    const anchor = findOverlayAnchor(document.getElementById('ov')!)
    expect(anchor?.id).toBe('field')
  })

  it('DatePicker: aria-controls aponta para o próprio painel', () => {
    document.body.innerHTML = `
      <span class="p-datepicker" id="dp"><input aria-expanded="true" aria-controls="panel1" /></span>
      <div class="p-datepicker-panel" id="panel1"></div>`
    expect(findOverlayAnchor(document.getElementById('panel1')!)?.id).toBe('dp')
  })

  it('alternativa: elemento ativo dentro de um campo', () => {
    document.body.innerHTML = `
      <div class="p-select" id="field"><input id="inner" /></div>
      <div class="p-select-overlay" id="ov"></div>`
    document.getElementById('inner')!.focus()
    expect(findOverlayAnchor(document.getElementById('ov')!)?.id).toBe('field')
  })

  it('sem âncora devolve null', () => {
    document.body.innerHTML = '<div class="p-select-overlay" id="ov"></div>'
    expect(findOverlayAnchor(document.getElementById('ov')!)).toBeNull()
  })
})

describe('installOverlayAlign (jsdom)', () => {
  let uninstall: () => void

  beforeEach(() => {
    // Dois quadros de animação "duplos" executam na hora, de forma determinística.
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
      cb(0)
      return 0
    })
    document.body.innerHTML = ''
    Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: 1000 })
    Object.defineProperty(document.documentElement, 'clientHeight', { configurable: true, value: 800 })
    uninstall = installOverlayAlign()
  })

  afterEach(() => {
    uninstall()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  function mountField(anchorRect: AnchorRect) {
    const field = document.createElement('div')
    field.className = 'p-select'
    field.innerHTML = '<span role="combobox" aria-expanded="true" aria-controls="sel_list"></span>'
    field.getBoundingClientRect = () => anchorRect as DOMRect
    document.body.appendChild(field)
    return field
  }

  function openOverlay(size = { width: 300, height: 200 }, style = 'position:absolute;left:0px;top:240px;') {
    const overlay = document.createElement('div')
    overlay.className = 'p-select-overlay'
    overlay.setAttribute('style', style)
    overlay.innerHTML = '<ul id="sel_list"></ul>'
    Object.defineProperty(overlay, 'offsetWidth', { configurable: true, value: size.width })
    Object.defineProperty(overlay, 'offsetHeight', { configurable: true, value: size.height })
    overlay.getBoundingClientRect = () => rect(0, 240, size.width, size.height) as DOMRect
    document.body.appendChild(overlay)
    return overlay
  }

  const tick = () => new Promise<void>((resolve) => setTimeout(resolve, 0))

  it('corrige o overlay colado na borda esquerda (left 0) para logo abaixo do campo', async () => {
    mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    await tick()

    expect(overlay.style.left).toBe('120px')
    expect(overlay.style.top).toBe('240px')
    expect(overlay.style.minWidth).toBe('300px')
  })

  it('não mexe em um overlay que já está no lugar certo', async () => {
    mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay({ width: 300, height: 200 }, 'position:absolute;left:120px;top:240px;min-width:300px;')
    const before = overlay.getAttribute('style')
    await tick()
    expect(overlay.getAttribute('style')).toBe(before)
  })

  it('reaplica quando o PrimeVue reposiciona depois (observador de style)', async () => {
    mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    await tick()
    expect(overlay.style.left).toBe('120px')

    overlay.style.left = '0px'
    await tick()
    expect(overlay.style.left).toBe('120px')
  })

  it('reaplica no resize/scroll enquanto aberto, usando a nova posição do campo', async () => {
    const field = mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    await tick()

    field.getBoundingClientRect = () => rect(50, 300, 300, 40) as DOMRect
    window.dispatchEvent(new Event('resize'))
    expect(overlay.style.left).toBe('50px')
    expect(overlay.style.top).toBe('340px')
  })

  it('sem laço: as escritas da própria correção não disparam novas correções infinitas', async () => {
    mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    const spy = vi.spyOn(overlay.style, 'left', 'set')
    await tick()
    await tick()
    expect(spy.mock.calls.length).toBeLessThanOrEqual(1)
  })

  it('ao fechar (overlay removido) para de acompanhar: scroll não altera mais nada', async () => {
    const field = mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    await tick()
    overlay.remove()
    await tick()

    const removeSpy = vi.spyOn(window, 'removeEventListener')
    field.getBoundingClientRect = () => rect(10, 10, 300, 40) as DOMRect
    window.dispatchEvent(new Event('resize'))
    expect(overlay.style.left).toBe('120px')
    removeSpy.mockRestore()
  })

  it('sem campo âncora identificável não altera o overlay', async () => {
    const overlay = openOverlay()
    await tick()
    expect(overlay.style.left).toBe('0px')
  })

  it('o desinstalador desfaz o observador do body', async () => {
    uninstall()
    mountField(rect(120, 200, 300, 40))
    const overlay = openOverlay()
    await tick()
    expect(overlay.style.left).toBe('0px')
    uninstall = installOverlayAlign()
  })
})
