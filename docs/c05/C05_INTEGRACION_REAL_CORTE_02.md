# C05 — Evidencia de integración operativa, corte 02

**Fecha:** 2026-11-15 (Honduras)  
**Repositorio:** crecisol-platform/CRECISOL  
**Rama:** feature/c05-identity-integration-gate-20261114  
**HEAD al registrar:** 4c6b04e55b16039810dd7ecbc3c6742099b99d81  
**Dictamen:** **C05 NO CERTIFICADA; C06 BLOQUEADO.** Este documento registra pruebas y límites del corte; no ratifica ni amplía el alcance institucional de C05-01..11.

## Cambios comprobables en este corte

- Se añadió una prueba de concurrencia al endpoint operativo de solicitud neutral: 24 solicitudes simultáneas en tenant D; la cuota persistente admite exactamente 8 y crea exactamente 8 intenciones. El test recolecta y espera solo los PID de los curl, evitando bloquearse por el Kestrel que queda activo.
- Se actualizó @angular/build de 22.2.4 a 22.2.7 y el lockfile. npm audit pasa de 2 avisos (1 bajo, 1 alto) a **0 vulnerabilidades**. Los demás paquetes Angular no se cambiaron.

## Evidencia ejecutada

Los siguientes resultados corresponden al código del PR y a los contenidos finales de las rutas afectadas. Las pruebas C05 de backend se completaron antes de los commits de dependencias Angular; esos commits solo modifican frontend/package.json y frontend/package-lock.json.

- Compilación Release con -warnaserror de todos los proyectos de prueba del backend: **PASS**.
- Lote database/tests/c05*.sh, MySQL 8.4 real descartable: **20/20 PASS** en una ejecución completa. Incluye registro neutral del API/Kestrel, Host y aislamiento A/B, rechazo de Host reenviado, cuota atómica y concurrente, invitaciones, pruebas/outbox de buzón, sesiones/revocación, recuperación, cambios de correo, MFA, perfiles privados, limitación y autorización del arnés integrado, más migración y rollback.
- Hubo una falla no reproducida en el primer intento del test de outbox (la verificación de un código recién enviado). El test pasó al repetirse de forma aislada y también en la segunda ejecución completa. No se identificó causa para esa falla transitoria; queda registrada, no se oculta.
- Con Node 24.15.0 y lockfile limpio: npm ci (**0 vulnerabilidades**), npm run check:types PASS y npm run build PASS con Angular 22.2.7.
- Chromium/Playwright: suite pública tests/c01-05.public-brand.spec.ts, **11/11 PASS**.
- Se intentó la suite general de Playwright (96 casos), pero no terminó en verde: algunos casos requieren el backend C01 real en 127.0.0.1:5184 y el preview aislado C02-06 usa su propia configuración. El backend requerido no estaba iniciado y hubo ECONNREFUSED; la ejecución se detuvo sin atribuir PASS a esa suite.
- No se ejecutaron GitHub Actions ni se usan como gate bajo la decisión de autonomía vigente.

## Estado de conexión del producto

La ruta de producto operativa en este HEAD es únicamente POST /api/v1/identity/registration-intents, registrada solo con CRECISOL_C05_REGISTRATION_ENABLED=true y DSN/llaves del servidor. La migración C05 se probó hacia delante y atrás. La ruta registra una intención neutral cifrada; no entrega una prueba de buzón ni crea cuenta o membresía.

Los proyectos y fixtures de prueba sí ejercitan componentes de buzón, invitaciones, Argon2id, MFA, sesiones, recuperación y autorización. **No equivalen a sus adaptadores operativos ni a endpoints conectados a la composición real.** La interfaz Angular /cuenta compila, pero sus solicitudes de login, MFA, sesión, logout y recurso privado no tienen endpoints de producto implementados; por ello no se certificó un recorrido Angular→API→MySQL de identidad.

## Auditoría y pendientes que impiden certificar

- **A — Funcional/trazabilidad:** PASS solo para la solicitud neutral persistida y la migración. Pendiente: prueba de buzón ligada al registro, cuenta pendiente, aprobación, autenticación, MFA, sesión, recuperación y navegación autenticada end-to-end.
- **B — Seguridad:** PASS en los negativos/concurrencia de las suites aisladas y de registro neutro descritas arriba. Pendiente: repetir controles sobre la API unificada y adaptadores operativos, incluyendo autoaprobación/elevación, BOLA/IDOR por tenant/sucursal/propietario, replay/revocación y fallos fail-closed del flujo completo.
- **C — Arquitectura/QA/UX:** builds backend y Angular PASS; 11 regresiones Chromium PASS. Pendiente: E2E del circuito real Angular→API→MySQL, suite Playwright integral con todos sus servicios requeridos, accesibilidad de los estados de identidad y auditoría A/B/C final sobre el mismo SHA.

No están conectados operativamente: outbox y consumo del proof de buzón, cuenta/credencial persistentes con canonización única, login Argon2id con limitación antes del hash, aprobación de membresía por una sesión MFA vigente, enrollment/verificación MFA, sesiones rotables/revocables, recuperación/cambios sensibles, endpoint privado deny-by-default y el recorrido autenticado de Angular. Persisten además COM-T08/T09/T10 y cualquier proveedor externo como **NO EJECUTADOS**.

**Decisión:** mantener PR #151 en DRAFT, sin fusionar; no declarar identidad confiable, no habilitar C06 y no escribir en dev, main, SOLQARYN o producción hasta cerrar los P0/P1 y obtener pruebas reales suficientes. No hubo cambios a producción, VPS, DNS, Cloudflare, pagos ni servicios externos.
