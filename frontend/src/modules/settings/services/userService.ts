import { http } from '@/services/http'
import type { ChangePasswordRequest, DeleteAccountRequest, UpdateProfileRequest, UserDto } from '@/types/api'

/** Usuário autenticado — docs/API_CONTRACTS.md §4 (Bearer). */
export const userService = {
  /** `GET /users/me` → `UserDto`. */
  async getMe(): Promise<UserDto> {
    const { data } = await http.get<UserDto>('/users/me')
    return data
  },

  /** `PUT /users/me` → `UserDto`. */
  async updateMe(request: UpdateProfileRequest): Promise<UserDto> {
    const { data } = await http.put<UserDto>('/users/me', request)
    return data
  },

  /** `PUT /users/me/password` → 204. Revoga os demais refresh tokens, mantendo a sessão atual. */
  async changePassword(request: ChangePasswordRequest): Promise<void> {
    await http.put('/users/me/password', request)
  },

  /** `DELETE /users/me` com corpo `{ password }` → 204. Anonimiza e desativa a conta (D-014). */
  async deleteMe(request: DeleteAccountRequest): Promise<void> {
    await http.delete('/users/me', { data: request })
  },
}
