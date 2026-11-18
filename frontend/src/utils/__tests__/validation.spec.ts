import { describe, expect, it } from 'vitest'
import {
  normalizeEmail,
  validateEmail,
  validateName,
  validatePasswordConfirmation,
  validatePasswordPolicy,
  validateRequiredPassword,
} from '../validation'
import { safeRedirect } from '../redirect'

describe('validação (espelho do contrato)', () => {
  it('nome 2–100 com trim', () => {
    expect(validateName('')).toBeDefined()
    expect(validateName(' a ')).toBeDefined()
    expect(validateName('Al')).toBeUndefined()
    expect(validateName('a'.repeat(100))).toBeUndefined()
    expect(validateName('a'.repeat(101))).toBeDefined()
  })

  it('e-mail', () => {
    expect(validateEmail('')).toBe('Informe o e-mail.')
    expect(validateEmail('maria')).toBe('Informe um e-mail válido.')
    expect(validateEmail(' maria@exemplo.com ')).toBeUndefined()
    expect(validateEmail(`${'a'.repeat(250)}@x.com`)).toBeDefined()
    expect(normalizeEmail(' Maria@Exemplo.COM ')).toBe('maria@exemplo.com')
  })

  it('senha: 8–128, letra e número', () => {
    expect(validatePasswordPolicy('')).toBeDefined()
    expect(validatePasswordPolicy('Abc123')).toMatch(/8 caracteres/)
    expect(validatePasswordPolicy('abcdefgh')).toMatch(/letra e um número/)
    expect(validatePasswordPolicy('12345678')).toMatch(/letra e um número/)
    expect(validatePasswordPolicy('Senha123')).toBeUndefined()
    expect(validatePasswordPolicy('ção12345')).toBeUndefined()
    expect(validatePasswordPolicy(`a1${'x'.repeat(127)}`)).toMatch(/128/)
    expect(validateRequiredPassword('')).toBeDefined()
    expect(validateRequiredPassword('x')).toBeUndefined()
    expect(validatePasswordConfirmation('Senha123', 'Senha124')).toBe('As senhas não conferem.')
    expect(validatePasswordConfirmation('Senha123', 'Senha123')).toBeUndefined()
  })
})

describe('safeRedirect', () => {
  it('aceita apenas caminhos internos', () => {
    expect(safeRedirect('/accounts?x=1')).toBe('/accounts?x=1')
    expect(safeRedirect(['/groups'])).toBe('/groups')
    expect(safeRedirect('//evil.com')).toBe('/')
    expect(safeRedirect('/\\evil.com')).toBe('/')
    expect(safeRedirect('https://evil.com')).toBe('/')
    expect(safeRedirect(undefined, '/x')).toBe('/x')
  })
})
