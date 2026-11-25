# CRECISOL — C06-05: acta final de reautenticación sensible

**Fecha:** 24-11-2026 (America/Tegucigalpa). **Dictamen técnico: C06-05 CERTIFICADA EN SU ALCANCE, 9.5/10; visual N/A.** Aceptación técnica/comercial delegada del asistente: **sí aceptaría esta microfase para su integración de desarrollo**, no una certificación de CRECISOL productivo ni una firma humana del propietario. **C06: 5/11 microfases certificadas; capítulos completos: 5/37; C06 global NO CERTIFICADO; C06-06 únicamente habilitada para su propia preparación secuencial.**

## 1. Autoridad y objetivo

Repositorio original ID 1450414592 (actual `crecisol-platform/CRECISOL`, redirigido desde `solqaryn-platform/CRECISOL`). Exclusivamente rama dev. AGENTS.md, Skill de gobernanza del repositorio, Documento Maestro Único Word blob fe7aaab60b603f1844e18666e2f99c0ea97e86e9 y SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c, catálogo C06-01 (80 HTTP, 12 internos, 28 acciones futuras no concedibles), matriz C06-02 (10 roles, 108 acciones), C06-03 identidad y pertenencia y C06-04 ABAC. Plan operativo de 11 microfases: step-up, finalidad justificada y no bypass por petición API directa. Aplicar la decisión de autonomía técnica de 11-11-2026 en lo relativo a aceptación delegada y suficiencia de evidencia real; no exigir una nueva firma humana ni un estado verde de GitHub como sustituto de pruebas reales.

**Entregable real:** lista cerrada de 19 operaciones autenticadas que consumen una prueba fresca de C05 y validación del propósito en servidor. Una sesión válida no sustituye MFA, una forma de entrada válida no constituye prueba, ni MFA sustituye autorización RBAC/ABAC. No añadir un grant, ticket reutilizable o token step-up. No cambiar el protocolo TOTP RFC6238 SHA1 ni las llaves WebAuthn de C05; los flujos de desafío y confirmación existentes mantienen caducidad, ámbito y consumo. El motivo libre solo se exige en las tres operaciones que ya lo contemplan en su contrato; las otras 16 tienen propósito fijo de acción. No activar los 28 permisos normativos futuros.

## 2. Integración y evidencia por identidad exacta

- Base dev: `9bcd3fca482ef49df12b602e3641b1fc08a1994f`.
- [PR técnico #204](https://github.com/crecisol-platform/CRECISOL/pull/204), HEAD probado `83193eeb16538b57b2bd8d2b23fead9bb6b97220`.
- Merge virtual realmente ejecutado en CI PRE: `943a4b3b035e1f6e7e5a2649f62efcdfddab0684`, padres base y HEAD anteriores.
- Merge real, solo dev: `73feeacbac3afa5afc7198c32330c189ed9f25d6`, ambos padres idénticos a los del merge virtual.
- **Árbol Git completo idéntico PRE y dev: `3ef4f92b487a557e5027ab3dbb49ed7e82e609f6`**, comprobado mediante API Git de los dos commits. No se está equiparando un diff aproximado a una prueba: el árbol completo, incluidos fuente, pruebas, lockfiles, configuración y maestro, es el mismo objeto. Los resultados de ejecución pertenecen al checkout PRE exacto; no se reetiquetan como ejecuciones POST.
- PRE C06-05 [#45548455540](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455540) **4/4 SUCCESS**; C06-04 [#45548455429](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455429) **4/4 SUCCESS**; C05 operativa [#45548455441](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455441) **2/2 SUCCESS**. Leídos logs de los jobs completos y confirmados los 359, 351 y 340 resultados integrados.
- PRE adicional sobre HEAD: C06-01 [#45548455426](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455426) 3/3; C06-02 [#45548455410](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455410) 3/3; C06-03 [#45548455618](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455618) 3/3; C05 SHA1 [#45548455442](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455442) 4/4; C05 UX [#45548455484](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455484) 5/5; fundamento [#45548455511](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455511) SUCCESS; integral C03 [#45548455562](https://github.com/crecisol-platform/CRECISOL/actions/runs/45548455562) SUCCESS. Total: 10 workflows aplicables SUCCESS, 3 SKIPPED (no se cuentan como aprobados).
- La repetición automática POST de C06-05 [#45550628504](https://github.com/crecisol-platform/CRECISOL/actions/runs/45550628504) se observó con A/B aprobadas y C/E2E aún en curso al redactar. **No se atribuye aprobación a resultados pendientes.** El dictamen se fundamenta en ejecución integral real del árbol exacto integrado, tres auditorías y verificación posterior de identidad del árbol, no en suponer que otra ejecución futura pasará. Se aplica la delegación técnica documentada en DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md; una falla esencial posterior obliga a reabrir y corregir antes del siguiente cierre. El encabezado NO CERTIFICADA de C06_05_ESPECIFICACION.md conserva la instantánea histórica de candidatura y no prevalece sobre esta acta.

## 3. Cobertura y arquitectura

| Ámbito | Acción de servidor | Finalidad |
|---|---|---|
| TENANT | passkeys/register-options | Alta de llave propia, desafío ligado al contexto |
| TENANT | passkeys/revoke | Revocación de llave propia |
| TENANT | invitations/issue | Invitación con permiso previo |
| TENANT | sessions/revoke-all | Revocación de sesiones propias |
| TENANT | pairings/approve | Aprobación de vinculación exacta |
| TENANT | factor/replace | Cambio de factor |
| TENANT | password/change | Cambio de contraseña |
| TENANT | mailbox/request | Solicitud de cambio de correo |
| TENANT | mailbox/complete | Confirmación del cambio |
| TENANT | memberships/decide | Motivo explícito, 8–240 caracteres |
| PLATFORM | platform/sessions/revoke-all | Revocación de sesiones propias |
| PLATFORM | platform/cases/find | Motivo explícito, 12–512 caracteres |
| PLATFORM | platform/password/change | Cambio de contraseña de operador |
| PLATFORM | platform/factor/replace | Cambio de factor de operador |
| PLATFORM | platform/mailbox/request | Solicitud de correo de operador |
| PLATFORM | platform/mailbox/complete | Confirmación de correo de operador |
| PLATFORM | platform/review | Motivo explícito, 12–512 caracteres |
| PLATFORM | platform/release-share | Emisión de parte autorizada |
| PLATFORM | platform/session-policy/update | Cambio de política, permiso específico y revisión |

- Domain: C06SensitiveOperationPolicy, sin HTTP, SQL ni dependencias de framework. Listas y ámbitos exactos, forma de TOTP SHA1 o código de respaldo C05, motivo con al menos letra o dígito, sin caracteres de control. No equivale a comprobar la veracidad de un motivo ni a autorizar.
- Infrastructure TENANT: contexto C06-03, actor verificado, factor activo bloqueado, cuota, AcceptTotp y controles específicos de cada handler. Se mantiene ABAC C06-04 para lectura privada, no se confiere acceso por step-up.
- Infrastructure PLATFORM: principal verificado por la sesión y su revisión, comprobación de igualdad actor/principal, propósito antes del dispatch, PlatformTotp en los handlers directos y delegados. No se confunden los ámbitos. HMAC del propósito en el audit existente; la búsqueda conserva su HMAC C05; aprobación TENANT conserva digest de motivo. Nunca registrar código MFA ni razón en claro mediante estos cambios.
- Reutiliza transacciones, consumo irreversible de códigos de un uso y rechazo de contador repetido, cuotas, caducidad y revocación C05. No se crea autorización temporal reutilizable; el factor se verifica para la operación efectiva. Los siete flujos distintos de preautenticación/recuperación/confirmación de desafío no se clasifican como estas 19 acciones.
- Frontend, API pública, esquema, migraciones, grants y primitivas criptográficas: sin modificación. Política de inactividad administrable C05 conservada; regresión comprueba 30 minutos, descenso a 5, restitución a 30 y no resurrección de sesión vencida. Sin cambio de UI; nota visual N/A del diff.

### 12 archivos del PR técnico

| Capa | Archivo |
|---|---|
| Domain | backend/src/Crecisol.Domain/Authorization/C06SensitiveOperationPolicy.cs |
| Infrastructure | backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs |
| Infrastructure | backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs |
| Contratos | backend/tests/Crecisol.C06StepUp.ContractsTests/Program.cs |
| Contratos | backend/tests/Crecisol.C06StepUp.ContractsTests/Crecisol.C06StepUp.ContractsTests.csproj |
| QA real | qa/c05_operational_flow_e2e.mjs |
| QA A/B/C | qa/c06_05_stepup_audits.py |
| Regresión scope | qa/c06_02_role_model_audit.py |
| Regresión scope | qa/c06_03_context_audits.py |
| Regresión scope | qa/c06_04_abac_audits.py |
| CI read-only | .github/workflows/c06-05-sensitive-step-up.yml |
| Especificación | docs/c06/C06_05_ESPECIFICACION.md |

El cierre documental solo añade esta acta, antepone estado vigente y plan y adapta el scope exacto del auditor institucional C06-04 a esos cuatro archivos de sucesión. No cambia sus comprobaciones de seguridad ni un archivo de producto. Se conserva la evidencia histórica.

## 4. Pruebas reales y adversariales

| Evidencia | Resultado |
|---|---|
| Contratos de entrada .NET10, ámbito/formato/motivo | 308 PASS (no equivalen a 308 pruebas criptográficas) |
| Compilación .NET 10 | 0 errores y 0 advertencias de compilador |
| Angular 22, Node 24 | typecheck/build PASS |
| Chromium → Angular → Kestrel → MySQL 8.4 desechable | 359 aserciones integradas PASS |
| Regresión ABAC C06-04 aislada | 351 aserciones PASS |
| Regresión operativa C05 | 340 aserciones PASS |
| C06-02 MySQL | 23 ataques SQL negativos y rollback PASS |
| C05 SHA1 y modales | workflows aplicables completos SUCCESS |

**Ocho comprobaciones E2E añadidas:** (1) fixture aislado de respaldo fresco disponible, (2) sesión sola no aprueba membresía, (3) campo browser stepUpVerified rechazado, (4) motivo solo puntuación denegado aun con prueba fresca, (5) código de formato correcto pero inválido denegado, (6) membresía queda pendiente tras rechazos, (7) prueba válida no consumida por motivo inválido permite exactamente la aprobación autorizada, (8) prueba consumida no se reutiliza en invitación. Son parte de las 359 aserciones, no se suman otra vez.

**Regresiones positivas/negativas reales:** invitación con MFA y aprobación legítima; actor sin permiso no administra política; tenant no suplanta operador ni lee sus datos; operador no ingresa como tenant; contraseña/correo/factor/llave/sesión/vinculación; reautenticación con TOTP y códigos de respaldo; revocación y replay; autorización específica de recuperación en dos operadores; motivo de búsqueda guardado como digest; DB sin permiso falla cerrada; ABAC IDOR intraempresa e intertenant, revocación y rollback 020 antes de C05. Las 19 operaciones se revisaron individualmente en la fuente y se contrastaron con el catálogo y los recorridos E2E aplicables. No afirmar que cada una recibió una matriz exhaustiva individual de ataques HTTP nuevos: ocho comprobaciones nuevas específicas y regresión integral heredada.

## 5. Auditorías independientes

**A — Requisitos, funcionalidad y trazabilidad: PASS.** Verificación Word original por el auditor (70 párrafos C06), catálogo 80/12/28, lista cerrada 10+9 y contrato de motivos, delta de 12 archivos, HEAD/merge/árbol exactos; revisiones C06-01 y C06-02. La sesión sola falla; los casos autorizados funcionan.

**B — Seguridad, privacidad y tenant: PASS en alcance.** Actor/empresa y ámbito separados, propósito validado antes de handler, autorización previa conservada, prueba fresca criptográfica C05 y consumo, rechazos negativos reales de HTTP, separación de operador/tenant, rollback, pruebas SQL e IDOR heredadas. Revisión cruzada del código de los 19 handlers; HMAC sin razón/código en claro. Sin hallazgo P0/P1 atribuible al cambio en la superficie auditada. No es garantía de ausencia de vulnerabilidades del SaaS completo.

**C — Arquitectura, compilación y regresiones: PASS en alcance.** Domain/Infrastructure separados; compilador .NET 10, Angular22/Node24, Chromium/API/MySQL8.4, 359+351+340 por sus recorridos independientes (no sumar como pruebas únicas), regresiones C05 UX y funcionales, aislamiento SQL y reversión. Frontend y DB no cambiados, sin nota visual nueva, sin rendimiento productivo medido. Avisos de deprecación de Node20 en actions fijadas son del runner, no advertencias del compilador; quedan registrados, sin actualizar dependencias fuera de alcance.

## 6. Incidencias y correcciones

1. Primer workflow #45548250872 no ejecutó jobs por sintaxis YAML; se corrigió comillando nombre con dos puntos, commit 5e1dad6b73d2c2247335fea12d7f51a032f56946.
2. Auditor histórico C06-02 rechazó rutas nuevas #45548290891 y #45548318691. Se amplió únicamente el scope exacto C06-05 en C06-02/03/04; no se deshabilitaron guardas de producto. Commit final 83193eeb16538b57b2bd8d2b23fead9bb6b97220; repetición C06-02 3/3 y C06-05 4/4 SUCCESS.
3. E2E que figuraba pendiente al informar anteriormente terminó correctamente, sin nuevo cambio de código; se verificaron los logs finales antes de integrar. El alcance de la corrección documental posterior también es exacto y preserva auditorías anteriores.

## 7. Dictamen como comprador

**9.5/10 técnico — SÍ aceptaría/compraría esta microfase delimitada**, porque ya verifica prueba fresca efectiva, finalidad y permisos en las acciones existentes, conserva separación multitenant, rechaza intentos de bypass/replay, compila y completa integración real y regresiones con evidencia reproducible y código integrado idéntico. La nota es un juicio técnico de aceptación de este alcance, no una métrica estadística, ni prueba de todo el producto. **Visual N/A**: no se cambiaron interfaces y se conservaron las regresiones UX C05, sin inventar otra nota visual. No hay P0/P1 identificado pendiente en la modificación auditada.

## 8. Límites, riesgos, reversión y estado de ramas

- No es una firma criptográfica de los parámetros de la intención, ni verificación semántica de la justificación. No se introducen tickets reutilizables. No se certifica un administrador de permisos futuro ni acceso excepcional SuperAdmin a PII: corresponde a C06-06.
- No se midió capacidad de producción. Se verificó el sistema en entorno CI aislado y con datos sintéticos; el entorno de chat no ejecutó por sí mismo .NET/MySQL.
- API informó main no protegida; no se afirma protección inexistente ni se cambian permisos, visibilidad o configuración del repositorio. main permanece b794181148d2c5e36c1c88127f19a8a9082acff5. Sin VPS, producción, cobros, datos reales ni cambios a SOLQARYN.
- Reversión del PR #204 mediante revert controlado de su merge en dev, sin migración nueva ni modificación de datos. Reejecutar regresiones de seguridad si se revierte; rollback de dependencias C06-04/C05 se verificó en DB descartable, no efectuarlo en datos reales. Conservar acta histórica y registrar una nueva si se reabre.
- **C06-05 CERTIFICADA EN SU ALCANCE técnico, 9.5/10.** C06-01..05 5/11; C06 global NO CERTIFICADO; capítulos 5/37; C07 bloqueado. Siguiente microfase C06-06, acceso excepcional temporal SuperAdmin, con alcance y auditorías propias, no implementada ni certificada aquí.

La revisión documental se acredita por comparación de archivos y árboles, correspondencia de resultados con los logs y confirmación del estado de dev. La certificación se apoya en la ejecución real sobre el árbol completo integrado, no se inventa un PASS POST ni un acta humana adicional.
