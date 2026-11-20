# CRECISOL · C05 · Fase 03 — Rediseño integral de cuenta autenticada

**Especificación corregida y obligatoria recibida del propietario (19-11-2026):**

> Fase 03 — Rediseño de la cuenta autenticada. Reconstruir resumen de sesión, perfil, llaves, seguridad, historial y aprobaciones. Sustituir listas de datos crudos por información legible y acciones contextualizadas. Entregable: todas las vistas de negocio y cuenta con calificación individual mínima de 9/10.

## I. Rectificación de alcance y decisiones

La entrega histórica del PR #176, SHA `07b6768ee4265db9bfd4b095d3825c55cc5739b5` y CI #44648444073, certificó **solo navegación**. Por consiguiente, **esa certificación era insuficiente** y **no se reconoce como F03 integral**. Se preserva como subresultado técnico trazable; la rectificación consta en [comentario #7026956082](https://github.com/crecisol-platform/CRECISOL/pull/176#issuecomment-7026956082).

El presente documento define la F03 real y sustituye la interpretación anterior de F03 = navegación. El documento `F03_ARQUITECTURA_INFORMACION_Y_NAVEGACION.md` queda como **registro de la subentrega inicial**, no como el contrato de F03 completa.

**Repositorio autorizado:** `crecisol-platform/CRECISOL`, PR #176 DRAFT, basado en PR #175 F02 y dependiente de PR #172 TOTP-SHA1 y política configurable de inactividad (30 minutos iniciales). PR #173 aporta un borrador visual antecedente: se seleccionaron sus cambios de cuenta para revisión, sin modificar ni fusionar PR #173. No tocar `dev`, `main`, `solqaryn/Solqaryn`, VPS ni producción.

## II. Alcance entregado en código de cuenta

| Vista obligatoria | Cambios concretos de la corrección | Pruebas necesarias |
| --- | --- | --- |
| **Resumen de sesión** | Tarjeta de sesión, minutos recibidos del servidor, dispositivos con nombre, último uso y vencimiento legible, referencia corta no reversible, revocación contextual, acciones de vinculación/revocación masiva bajo divulgación progresiva | Datos actuales, vacío/error, 320-1440, revocación solo del elemento elegido, caducidad backend independiente |
| **Perfil** | Cabecera/avatar con iniciales derivadas, nombre, idioma y zona horaria legibles, recarga explícita, formulario precargado y restitución al cancelar, foto con operación de eliminación separada | Datos actuales/vacíos, controles, cancelación, foto sintética, 320-1440 |
| **Llaves** | Tarjetas con fecha local y referencia corta, registros vacíos, refresco, incorporación y selector obligatorio de llave autorizada para revocar, sin teclear UUID | Sin llaves, múltiples llaves, selección obligatoria, MFA, 320-1440 |
| **Seguridad** | Acciones de contraseña, segundo factor TOTP-SHA1, recuperación con llave y cambio de correo agrupadas con contexto propio; modales de scroll seguro, feedback separado, foco correcto | HTML/forms/step-up inalterados, teclas/diálogos, carga/error, 320-1440 |
| **Historial** | **Sexta ruta de cuenta** `?vista=historial`, cronología ordenada de eventos, fecha local, etiquetas allowlist, resultados comprensibles, 8 registros primero y hasta 50 bajo demanda, sin códigos técnicos | Varios resultados, eventos desconocidos, estado vacío, 320-1440 |
| **Aprobaciones** | Aviso de permisos backend, decisión sin valor predeterminado, referencia recibida por canal administrativo, motivo de 8-240 caracteres y MFA; invitación contextual y aviso sobre permiso real | Negativas con decisión vacía, motivo y MFA; autorización y aislamiento real deben probarse en servidor |

La implementación preserva los comandos existentes del servicio de identidad y sus contratos, sin alterar endpoints, MySQL, MFA/CSRF, miembros/roles, cookies ni temporizador. **Las autorizaciones se deciden siempre en backend, jamás en la UI.** Las capturas son estrictamente sintéticas.

## III. Hallazgo estructural abierto: bandeja de pendientes de aprobaciones

El servicio actual `IdentityApiService` expone `decideMembership(membershipId,decision,reason,code)`, pero **no una consulta autorizada de membresías pendientes por tenant**, ni un listado de revisión con datos limitados para el aprobador. El módulo API expone `memberships/decide` y `memberships/request`; no hay un contrato existente verificable de bandeja de pendientes. Por ello, la UI mantiene una entrada de **referencia del expediente**: segura si el backend valida permiso, contexto y propiedad, pero inferior a una experiencia premium de revisión contextual. No inventar un endpoint, una lista o un permiso.

**Para aspirar de forma legítima a 9/10 en Aprobaciones:** diseñar y probar contrato de lectura solo de pendientes con filtro tenant derivado del contexto del backend, permiso `MEMBERSHIP_APPROVE`, límites de datos expuestos, paginación, filtros, concurrencia, redacción de PII y pruebas adversariales IDOR/BOLA entre empresas. Esta dependencia no se declara cubierta ni puede resolverse solo pintando tarjetas ficticias. Como involucra cambios de backend, requiere decisión arquitectónica documentada y CI .NET/MySQL de seguridad, no mera prueba en Chromium.

## IV. Umbral 9/10 — revisión visual individual y no promedio

La calificación exige captura real de cada vista en al menos 320, 390, 768, 1024 y 1440 px, estados cargado, vacío, error y diálogo crítico. Rúbrica de cada pantalla (10 puntos):

- Identidad/composición y ausencia de saturación: **2,0**
- Tipografía/jerarquía y legibilidad: **1,5**
- Utilidad de acciones/contexto y prevención de errores: **2,0**
- Estados, copy y respeto a datos/privacidad: **1,5**
- Responsive, teclado y accesibilidad AA: **2,0**
- Consistencia con C01/C02 y tokens F02: **1,0**

**NO-GO** si una sola vista obtiene menos de **9,0/10**, si falta un estado esencial, o si hay P0/P1, texto técnico crudo, permisos aparentes, fuga de datos, duplicación de foco, regresiones de modal o overflow. **CI SUCCESS no convierte automáticamente una pantalla en 9/10.** El acta debe registrar nota y evidencia individual por pantalla; no basta la media.

### Valoración provisional de capturas sintéticas — no certificación

Las capturas iniciales posteriores al rediseño (run #44651963391, SHA `1514fcc3b14a51906ca335215fdee0248cb03117`) fueron inspeccionadas como referencia visual y para detectar problemas. Es una **apreciación editorial provisional**, no una evaluación humana de datos reales ni nota final. Se observaron mejoras significativas respecto de F01 (4,02/10 inicial), pero todavía es necesaria la verificación del último SHA sobre los ajustes posteriores y la aceptación individual:

| Vista | Observación provisional | Gate 9/10 |
| --- | --- | --- |
| Resumen | Jerarquía mucho más clara y dispositivo principal visible; verificar densidad móvil, estado sin sesiones y detalles expandidos | **PENDIENTE** |
| Perfil | Datos ya son legibles, formulario precargado; completar evaluación de foto y estados alternativos | **PENDIENTE** |
| Llaves | Método y fecha legibles, selección segura; evaluar múltiples dispositivos y acciones de riesgo | **PENDIENTE** |
| Seguridad | Mejor agrupación y modales; evaluar la claridad final de cada una de las cuatro operaciones delicadas | **PENDIENTE** |
| Historial | Cronología comprensible y separada; evaluar más de ocho eventos, ausencia de datos y fechas inusuales | **PENDIENTE** |
| Aprobaciones | Sigue sin bandeja autorizada de pendientes y requiere introducir la referencia | **NO-GO para 9/10 integral** |

**La F03 completa no puede calificarse ni certificarse como 9/10 mientras exista un NO-GO.**

## V. Matriz A/B/C y evidencia obligatoria

**Auditoría A — funcional:** seis rutas, estado de sesión, perfil precargado, listado de llaves, cronología de eventos, decisiones administrativas explícitas, regresiones F02/C05, builds Angular 22.

**Auditoría B — seguridad/privacidad:** ninguna referencia completa de llave/sesión ni evento técnico en lectura principal, XSS inerte, pasos de MFA, selección de recurso, mención explícita de límites de autorización frontend, sin cambios en .NET/MySQL sin pruebas separadas.

**Auditoría C — arquitectura/experiencia:** tokens F02, modal accesible, foco/teclado/escape, scrolling, WCAG axe, zoom 200%, forced-colors/reduced-motion, 30 capturas exactas por SHA de los seis espacios en cinco anchos, estados alternativos, UX humana pantalla por pantalla.

QA nuevo `frontend/tests/c05-f03-account.spec.ts`, config `playwright.c05-f03-account.config.ts`, auditores `qa/c05_ux_f03_account_audits.py` y `qa/c05_ux_f03_account_evidence.py` y el workflow F03 existente. Evidencias únicamente válidas si `GITHUB_SHA` coincide con el HEAD entregado y no existen fallos activos. Las pruebas de navegador usan API sintética, **no certifican** .NET 10, MySQL 8.4, aislamiento de tenants real ni 30 min físicos de inactividad.

## VI. Resolución de esta corrección

**Estado F03:** REDISEÑO DE CUENTA EN IMPLEMENTACIÓN Y VALIDACIÓN; NOTA 9/10 **NO CERTIFICADA**. La subfase anterior de navegación conserva su evidencia pero no reemplaza el entregable. No se inicia F04 ni se fusiona el PR. No desplegar producción.
