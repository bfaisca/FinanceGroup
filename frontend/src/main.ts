import { createApp } from 'vue'
import { createPinia } from 'pinia'

import '@fontsource/inter/latin-400.css'
import '@fontsource/inter/latin-ext-400.css'
import '@fontsource/inter/latin-500.css'
import '@fontsource/inter/latin-ext-500.css'
import '@fontsource/inter/latin-600.css'
import '@fontsource/inter/latin-ext-600.css'
import '@fontsource/inter/latin-700.css'
import '@fontsource/inter/latin-ext-700.css'
import 'primeicons/primeicons.css'
import './assets/styles/main.css'

import App from './App.vue'
import { createAppRouter } from './router'
import { installPrimeVue } from './plugins/primevue'
import { installOverlayAlign } from './plugins/overlayAlign'
import { initTheme } from './composables/useTheme'
import { setupHttpSession } from './services/setupSession'

initTheme()

const app = createApp(App)
const pinia = createPinia()
const router = createAppRouter()

app.use(pinia)
installPrimeVue(app)
installOverlayAlign()
setupHttpSession(router)
app.use(router)

// O guard aguarda o bootstrap da sessão (refresh via cookie) antes da primeira navegação.
router.isReady().finally(() => {
  app.mount('#app')
})
