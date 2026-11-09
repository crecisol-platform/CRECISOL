# CRECISOL — Frontend público (C01-05)

Este proyecto contiene la primera interfaz Angular 22 standalone para la **identidad pública de plataforma**, no un portal de administración ni un SaaS desplegado. El Documento Maestro Único y el contrato C01-04 rigen su comportamiento; C01-06 incorporará el **archivo de logo oficial aprobado**.

## Entorno de desarrollo exclusivamente local

- Node.js **24.15.0 o superior de la serie 24**, npm incluido; Angular **22.2.4**, TypeScript **6.0.3**.
- Instalar: `npm ci`. Compilar: `npm run check:types && npm run build`.
- UI local: `npm run start` (127.0.0.1:4200). Proxy del entorno de desarrollo: `proxy.conf.json` hacia **127.0.0.1:5184**, exclusivamente API C01-04 local con su propia configuración segura y base efímera; no conectar servicios externos.
- Sin API de marca publicada, la UI **debe mostrar un estado de no disponibilidad**. No se usa fixture/demo en producción ni se almacenan datos de identidad en localStorage.
- Ejecutar Playwright Chromium: `npx playwright install chromium` y `npm run test:e2e:ci`. El test intercepta exclusivamente el endpoint fijo público de C01-04 con datos sintéticos (no abre conexiones MySQL).

Las rutas principales utilizan carga lazy. Se validan inputs del API antes de mostrarlos y se limpian metadatos/favicons al fallar. Angular no autoriza tenants. No hay editor, login, pagos, rutas privadas, CSR/SSR productivo ni dominios reales.

## Identidad y SEO

Los textos/lema/colores/SEO los suministra solo el snapshot **PLATFORM Published** de C01-04. La presentación incluye únicamente un wordmark tipográfico temporal; no redibuja isotipos. El favicon es referenciado solo si llega como ruta local permitida; **el binario debe verificarse en C01-06**. El HTML está protegido con noindex/nofollow hasta una futura puerta de SEO/producción; no implica que la indexación SPA esté resuelta.

## Seguridad y verificaciones

- Respuestas desconocidas, con campos tenant o contenido XSS no se renderizan.
- Sin CDN, fuentes activas, HTML inyectado, CSS arbitrario, CORS abierto, sessionStorage ni cookies propias.
- WCAG 2.2 AA automatizado, navegación/teclado, viewport móvil y reducción de movimiento se ejecutan en CI.
- [Contrato C01-05](../docs/c01/C01_05_INTERFAZ_ANGULAR.md) registra riesgos y límites. C01 permanece **0/37 capítulos certificados**; la microfase no se cierra sin compilación, pruebas, triple auditoría y acta.
