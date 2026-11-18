import { http } from '@/services/http'
import type {
  AuthResponse,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
} from '@/types/api'

/** Rotas públicas de autenticação — docs/API_CONTRACTS.md §3. */
export const authService = {
  /** `POST /auth/register` → 201 `AuthResponse` + cookie `gf_refresh`. */
  async register(request: RegisterRequest): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/register', request)
    return data
  },

  /** `POST /auth/login` → 200 `AuthResponse` + cookie `gf_refresh`. */
  async login(request: LoginRequest): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/login', request)
    return data
  },

  /** `POST /auth/refresh` (sem corpo; lê o cookie) → 200 `AuthResponse`, rotaciona o token. */
  async refresh(): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/refresh')
    return data
  },

  /** `POST /auth/logout` → 204 sempre. */
  async logout(): Promise<void> {
    await http.post('/auth/logout')
  },

  /** `POST /auth/forgot-password` → 202 sempre (não revela se o e-mail existe). */
  async forgotPassword(request: ForgotPasswordRequest): Promise<void> {
    await http.post('/auth/forgot-password', request)
  },

  /** `POST /auth/reset-password` → 204; `400 invalid_token` se inválido/expirado. */
  async resetPassword(request: ResetPasswordRequest): Promise<void> {
    await http.post('/auth/reset-password', request)
  },
}
