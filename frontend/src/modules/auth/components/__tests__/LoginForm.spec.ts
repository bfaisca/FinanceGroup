import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import { RouterLinkStub } from '@vue/test-utils'
import LoginForm from '../LoginForm.vue'

function mountForm(props: Record<string, unknown> = {}) {
  return mount(LoginForm, {
    props,
    attachTo: document.body,
    global: {
      plugins: [[PrimeVue, { theme: 'none' }]],
      stubs: { RouterLink: RouterLinkStub },
    },
  })
}

describe('LoginForm', () => {
  it('valida campos obrigatórios e não emite submit', async () => {
    const wrapper = mountForm()

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('Informe o e-mail.')
    expect(wrapper.text()).toContain('Informe a senha.')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('valida formato de e-mail', async () => {
    const wrapper = mountForm()
    await wrapper.find('#login-email').setValue('maria')
    await wrapper.find('#login-password').setValue('Senha123')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.text()).toContain('Informe um e-mail válido.')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('emite submit com e-mail normalizado quando válido', async () => {
    const wrapper = mountForm()
    await wrapper.find('#login-email').setValue('  Maria@Exemplo.com ')
    await wrapper.find('#login-password').setValue('Senha123')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([[{ email: 'maria@exemplo.com', password: 'Senha123' }]])
    expect(wrapper.text()).not.toContain('Informe')
    wrapper.unmount()
  })

  it('exibe erros do backend por campo, ligados via aria-describedby', async () => {
    const wrapper = mountForm()
    await wrapper.setProps({ fieldErrors: { email: ['E-mail inválido.'], password: ['Senha obrigatória.'] } })

    const emailError = wrapper.find('#login-email-error')
    expect(emailError.exists()).toBe(true)
    expect(emailError.text()).toContain('E-mail inválido.')

    const emailInput = wrapper.find('#login-email')
    expect(emailInput.attributes('aria-describedby')).toContain('login-email-error')
    expect(emailInput.attributes('aria-invalid')).toBe('true')

    const passwordInput = wrapper.find('#login-password')
    expect(wrapper.find('#login-password-error').text()).toContain('Senha obrigatória.')
    expect(passwordInput.attributes('aria-describedby')).toContain('login-password-error')
    wrapper.unmount()
  })

  it('limpa o erro do campo ao digitar', async () => {
    const wrapper = mountForm()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#login-email-error').exists()).toBe(true)

    await wrapper.find('#login-email').setValue('m')

    expect(wrapper.find('#login-email-error').exists()).toBe(false)
    wrapper.unmount()
  })

  it('exibe mensagem geral e estado de carregamento', async () => {
    const wrapper = mountForm({ errorMessage: 'E-mail ou senha inválidos.', loading: true })

    expect(wrapper.find('[data-testid="form-error"]').text()).toContain('E-mail ou senha inválidos.')
    const button = wrapper.find('button[type="submit"]')
    expect(button.attributes('disabled')).toBeDefined()
    wrapper.unmount()
  })
})
