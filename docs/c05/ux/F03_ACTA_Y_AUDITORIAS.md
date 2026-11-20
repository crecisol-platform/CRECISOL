# CRECISOL C05 · Fase 03 — Acta de revisión y rectificación de alcance

**ESTADO VIGENTE: IMPLEMENTACIÓN Y AUDITORÍAS EN CURSO. CERTIFICACIÓN INTEGRAL NO OTORGADA.**  
**Alcance obligatorio:** rediseño de las seis vistas **Resumen, Perfil, Llaves, Seguridad, Historial y Aprobaciones**, con **9/10 mínimo individual y verificable**, conforme a la [especificación corregida](F03_CUENTA_AUTENTICADA_REDISENO_Y_CALIFICACION.md).

## I. Rectificación documentada — no borrar historia

El primer cierre de PR #176 (SHA `07b6768ee4265db9bfd4b095d3825c55cc5739b5`, run #44648444073 PASS) cubrió arquitectura de información y navegación contextual. **No cubría este entregable F03 de cuenta autenticada**. Las anteriores afirmaciones generales de «F03 certificada» se rectifican, sin manipular sus pruebas ni borrar el comentario previo. Constancia [#7026956082](https://github.com/crecisol-platform/CRECISOL/pull/176#issuecomment-7026956082). Esta acta **sustituye la conclusión** del registro anterior, no sus datos históricos de ejecución.

**Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592); rama `feature/c05-ux-f03-arquitectura-navegacion-20261119` (nombre histórico de rama, mismo PR #176 DRAFT). Base F02 `0361934286282ddb1b6ba94797125859d37464d7` (PR #175). Dependencia autenticación PR #172 de TOTP-SHA1 y sesión configurable, valor inicial 30 minutos de inactividad. PR #173 preservado sin escritura ni fusión.

## II. Corrección de código desarrollada

- `frontend/src/app/pages/identity-workspace.component.html`: seis vistas separadas, tarjeta de sesión, dispositivos, perfil y foto, passkeys, seguridad, historial nuevo, aprobaciones explícitas y avisos específicos.
- `frontend/src/app/pages/identity-flow-views.component.ts`: datos presentados con fechas locales, referencias recortadas y eventos semánticos; 8 eventos iniciales, hasta 50; no sustituye autorización backend.
- `frontend/src/app/identity/identity-presentation.ts`: formato de fechas, referencias, eventos, método de segundo factor y zona horaria con fallback seguro.
- `frontend/src/app/pages/identity-portal.component.{ts,html,css}`: ruta `historial`, título contextual individual, pestañas móviles accesibles y copia de confirmaciones por acción.
- `frontend/src/app/ui/institutional-modal/*`: aislamiento de feedback, foco en el título, scroll del cuerpo, encabezado/pie visibles, cancelación y foco de retorno; no envía credenciales ni aplica permisos.
- `frontend/src/app/pages/identity-flow.component.css`: composición responsive/densidad, estados vacíos F02, tarjetas de dispositivos/llaves, cronología, acciones secundarias progresivas.
- Workflows F03, auditorías y pruebas sintéticas 320/390/768/1024/1440; no se editan `backend/`, `database/`, `dev`, `main`, SOLQARYN ni producción.

## III. Auditorías y evidencia real observada

| Gate | Evidencia existente | Estado |
| --- | --- | --- |
| A — Contratos/rutas y construcción | Auditores A/B/C estáticos y TypeScript+Angular 22, en run [#44651963391](https://github.com/crecisol-platform/CRECISOL/actions/runs/44651963391) | **PASS** en SHA de código `1514fcc3...` |
| A — Navegación y regresiones | 18/18 F03 navegación, 11/11 F02 y 2/2 C05 modal en mismo run | **PASS** en SHA `1514fcc3...` |
| A — Seis superficies reales de frontend | Suite Chrome `c05-f03-account.spec.ts` con 30 estados cargados + negativos y axe | **PASS** en SHA `1514fcc3...` |
| B — Privacidad y controles negativos | Auditor B y Chrome: sesión ausente, UUID ocultos, evento técnico inerte, selector explícito de passkey, aprobación vacía y MFA | **PASS** en alcance UI sintético |
| C — Responsive y evidencias | 30 PNG + 30 JSON de seis vistas x 5 anchos, manifiesto vinculado a commit, axe y reflow de 200% | **PASS** en SHA `1514fcc3...` |
| C — Calidad estética ≥9/10 **por cada vista** | Capturas reales inspeccionadas, sin calificación individual aprobada de seis vistas | **PENDIENTE / NO-GO** |
| B/A — Bandeja autorizada de pendientes empresariales | No existe contrato actual de listado por tenant, solo `memberships/decide`; no inventar permisos o recursos | **NO-GO para Aprobaciones 9/10** |

La primera ejecución de seis vistas [#44650830227](https://github.com/crecisol-platform/CRECISOL/actions/runs/44650830227) pasó builds y la navegación pero tuvo **29/42 PASS** y **13 FAIL** en localizadores ambiguos de regiones/etiqueta; se corrigió la duplicación de landmarks y se usaron nombres accesibles exactos. Se conserva el fallo histórico, no se presenta como verde.

### Evidencia de imágenes anterior al HEAD documental

[Run #44651963391](https://github.com/crecisol-platform/CRECISOL/actions/runs/44651963391), **SUCCESS** sobre `1514fcc3b14a51906ca335215fdee0248cb03117`; artefacto `#14590854212`, `c05-f03-navigation-1514fcc3b14a51906ca335215fdee0248cb03117`, descargado e inspeccionado. Un ZIP de 127 entradas: 30 PNG/30 JSON de cuenta + manifiesto, 30 PNG/30 JSON previos de navegación + manifiesto, y cinco PNG F02. **Posteriores cambios de presentación y estas actas crean otro HEAD que requiere run nuevo.** No adjudicar pruebas de ese SHA a cambios posteriores.

## IV. Seguridad, privacidad y límites

Los controles JavaScript de deshabilitado, navegación, historial y tabla son **solo UX**. El backend verifica identidad, tenant, membresía, rol, sucursal, propiedad, MFA, CSRF y política real de sesión. No hay pruebas MySQL/IDOR con tenants reales en esta fase de frontend. La aplicación no debe exponer UUID completos o semillas MFA en contenido principal; referencias completas pueden permanecer únicamente como valores de comandos protegidos al backend.

**Aprobaciones:** su UI de formulario sigue solicitando `membershipId` manual porque no existe API autorizada de bandeja de expedientes pendientes. No se puede presentar una lista inventada ni calificarla 9/10 a partir de fixtures. La especificación de contrato y controles necesarios está en el documento corregido; extender el backend exige evaluación de seguridad y pruebas separadas.

## V. Requisitos de certificación pendiente

**La F03 real NO estará certificada** hasta que:
1. El **HEAD final** con código y acta tenga A/B/C, builds y regresiones PASS y capturas vinculadas al SHA.
2. Cada una de las seis vistas, con estados de carga, vacío, error, decisiones/confirmaciones y 320–1440, logre una **nota de revisión visual individual >=9,0/10** con evidencia suficiente.
3. Se cierre la carencia de bandeja segura de Aprobaciones o se apruebe un alcance funcional equivalente que cumpla realmente la revisión de expedientes sin exposición de otras empresas.
4. No existan fallos P0/P1, fugas tenant, contradicciones visuales ni cambios fuera de alcance.

**Resolución vigente: NO CERTIFICADA INTEGRALMENTE. PR #176 DRAFT, no fusionado. Fase 04 BLOQUEADA.** Las pruebas técnicas exitosas se registran sin elevarlas injustificadamente a certificación 9/10.
