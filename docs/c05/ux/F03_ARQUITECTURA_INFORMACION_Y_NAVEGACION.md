# CRECISOL C05 · Programa visual · Fase 03 — Arquitectura de información y navegación

**Fecha:** 19 de noviembre de 2026 (horario América/Tegucigalpa). **Estado:** implementación de F03 sujeta a auditoría y pruebas.  
**Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592).  
**Rama de F03:** `feature/c05-ux-f03-arquitectura-navegacion-20261119`.  
**Base inmutable:** F02 `0361934286282ddb1b6ba94797125859d37464d7` (PR #175, técnicamente certificada en alcance).  
**Fuente funcional anterior al rediseño:** `f61a79d242288c686a88a7c3dd711f2be9a48c18` (PR #172, TOTP-SHA1/inactividad).  
**Dependencias históricas:** F01 PR #174 (65 superficies, línea base 4,02 provisional); F02 PR #175 (nueve primitivas y tokens). PR #173 es un borrador visual independiente con trabajo anticipado: **no se modifica ni fusiona durante F03**.

## Delimitación y justificación del alcance

La documentación vigente del repositorio no contenía una especificación autónoma titulada «Fase 03». Ante la autorización expresa de desarrollar F03, se adoptó **la fase conservadora siguiente a los tokens de F02: jerarquía de información, navegación contextual y continuidad entre vistas**. Se dejan fuera las migraciones generales de formularios, historiales, 27 modales y rediseños de negocios/operadores que corresponden a fases posteriores. Esta definición operativa **no reemplaza el Documento Maestro Único**.

**No cambiar:** backend, MySQL, control de autenticación, CAPTCHA, MFA TOTP-SHA1, WebAuthn, autorización, cookies, CSRF, expiración de sesión administrable (valor vigente 30 minutos), registro/invitaciones, perfiles, contratos API, rutas públicas C01/C02 ni infraestructura de producción.

## Estructura de navegación

### A — Cuenta sin sesión

- Marco editorial C01/C02 e identidad publicada validada por `PublicBrandService`, bajo `csInstitutionalTheme` de F02; la marca colorea **solo decoración**, no seguridad ni acciones.
- Miga de pan accesible `Inicio / Cuenta / [etapa]` y acceso directo a contenido mediante skip-link.
- Primera decisión en 2 acciones principales: **Iniciar sesión** y **Solicitar cuenta**.
- Etapas ocasionales tras un `<details>` nativo con etiqueta clara: **Confirmar correo**, **Recuperar acceso**, **Aceptar invitación** y **Ya tengo cuenta**. Al ingresar por vínculo directo a una de ellas, la sección queda desplegada.
- `?vista` permanece con `RouterLink`, conserva historial del navegador y enlaces compartibles; los estados desconocidos o exclusivos de la cuenta autenticada se muestran como ingreso, **sin tocar URL o sesión**, y sin conceder acceso.

### B — Desafío MFA

- Mantener el formulario técnico `cs-identity-mfa` y su temporizador/sesión intactos.
- Mostrar contexto específico «segundo paso» con título `h1`, sin presentar un desafío incompleto como autenticación concedida.
- El bloque se rige exclusivamente por `identity.state() === 'challenge'`.

### C — Cuenta autenticada

- Contexto de sesión protegida y `h1` propio. Cinco grupos directos: **Resumen**, **Mi perfil**, **Llaves**, **Seguridad** y **Aprobaciones**.
- Navegación por enlaces reales con `RouterLink`, `aria-current=page` y aviso accesible de sección; scroll **horizontal interno** de pestañas en móvil, sin scroll horizontal del documento.
- Normalización de presentación: una URL de invitación/recuperación abierta con sesión activa muestra **Resumen**, nunca una pestaña inexistente, sin redirección de permisos ni llamada nueva a API.
- La consulta de membresías y las acciones siguen sujetas exclusivamente al backend y al estado de la aplicación; mostrar un enlace de navegación **no equivale** a otorgar permisos.

### D — Operadores de plataforma

- Separación de ámbito visible y auditable. Reutilizar `cs-ui-navigation` de F02, sin nueva biblioteca ni pestañas de tipo CSS-clon.
- Cinco áreas existentes: Casos, Sesiones, Perfil, Seguridad y Monitor; mostrar el propósito del área activa sin etiquetas técnicas innecesarias.
- Mantener `openSecurity()` para que **Seguridad siga cargando la política de sesiones**. Cambio de área bloqueado durante acción asíncrona, limpieza de confirmaciones anteriores, IDs allowlist y regreso a «Casos» tras cierre confirmado de sesión.
- El selector de áreas existe únicamente cuando `PlatformIdentityApiService.state() === 'authenticated'`; ninguna cuenta empresarial obtiene un privilegio por cambiar de pestaña.

## Capa de implementación

| Archivo | Responsabilidad |
| --- | --- |
| `frontend/src/app/pages/c05-navigation.model.ts` | Listas allowlist, títulos, etiquetas y normalización pure local |
| `identity-portal.component.{ts,html,css}` | Marco, breadcrumbs, progresión de etapa, navegación responsive de cuenta y branded tokens F02 |
| `platform-identity-panel.component.{ts,html}` | Navegación compartida F02 y contexto de consola con carga preexistente |
| `identity-flow.component.css` | Diseño del contexto de operador, CTA de salida y separación visual |
| `frontend/tests/c05-f03-navigation.spec.ts` | Capturas sintéticas, viewport, keyboard, deep-links, MFA realm, lectura policy y axe |
| `frontend/playwright.c05-f03.config.ts` | Chromium en loopback aislado 127.0.0.1:4203, sin backend verdadero |
| `qa/c05_ux_f03_navigation_audits.py` | Auditorías A/B/C estáticas y guardas de alcance |
| `.github/workflows/c05-ux-f03-navigation.yml` | Node24/Angular22, F03, F02, modales C05 y evidencias por SHA |

## Riesgos de seguridad y datos

Los mocks de restauración devuelven una sesión **ficticia** con campo `idleMinutes=30`; no prueban una expiración real de 30 minutos en .NET/MySQL. El color de tema publicado es validado; no hay base de datos, secretos, QR reales ni cuentas reales en capturas. No se manipulan `URL.hash` de pairing, JWT, cookies, credenciales o solicitudes de recuperación. Un cambio de sección de plataforma no lleva IDs de casos ni detalles del ámbito a la URL.

**Fallo de autorización real:** nunca se admite por tener una pestaña activa o una ruta `?vista`. La comprobación de tenant/membresía/rol/propiedad/step-up ocurre en backend; F03 solo modifica presentación.

## Aceptación y rigor de F03

- **A funcional:** lista de vistas intacta, rutas profundas, estados de invitado/MFA/autenticado/operador, formulario y carga de política de Seguridad intactos; builds Angular y regresiones F02/C05.
- **B privacidad y seguridad:** un usuario sin sesión no ve workspace autenticado, una cuenta de negocio no obtiene consola por navegar, HTML interpolado, datos ficticios, ninguna API/DB cambiada ni secret leakage.
- **C calidad/arquitectura/UX:** contrastes, foco, `aria-current`, skip-link, navegación por teclado, ancho 320/390/768/1024/1440, escalado 200%, `forced-colors`, movimiento reducido y capturas enlazadas a SHA.

**Umbral visual:** la intención de diseño es premium ≥9/10 al finalizar el programa; **F03 no realiza la calificación final de las 65 superficies**, ni disfraza un CI verde como evaluación humana. No avanzar a F04 sin acta F03, A/B/C verificadas y no P0/P1.

**Rollback:** descartar rama/PR F03 y restaurar exclusivamente los archivos editados en F03. No hay migraciones, proveedores, endpoints ni datos que revertir.
