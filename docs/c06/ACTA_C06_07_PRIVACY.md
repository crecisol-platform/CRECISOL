# CRECISOL — Acta final C06-07: privacidad y minimización de DTO

**Fecha:** 25-11-2026, Honduras. **Dictamen: C06-07 CERTIFICADA EN SU ALCANCE TÉCNICO, 9.5/10. SÍ aceptaría/compraría esta microfase delimitada.** Visual N/A (sin nueva UI; regresiones UX existentes aprobadas). Eficacia institucional al integrar esta acta en dev, sin nuevas fallas esenciales conocidas. C06 7/11; capítulos completos 5/37; C06 global NO CERTIFICADO. C06-08 habilitada solo para su propio desarrollo secuencial; C07 bloqueado.

## 1. Autoridad y alcance
Repositorio original ID 1450414592, ubicación vigente crecisol-platform/CRECISOL. AGENTS.md, Skill local y Documento Maestro Único Word blob fe7aaab60b603f1844e18666e2f99c0ea97e86e9, SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. C06-07 del plan: privacidad y minimización de DTO; proyección estricta y retención definida; no serializar campos internos o PII extra. Word C06 líneas 15,17,18,28–33,41,61. C06-01/02 catálogo y roles, C06-03 contexto, C06-04 ABAC, C06-05 MFA fresco y C06-06 excepción preservados. Cierre precedente: 17 workflows POST de código y seis del acta, todos SUCCESS antes de abrir esta fase. No reescribir maestro, catálogos, reglas anteriores ni producción.

**Alcance técnico efectivo:** 75 operaciones dinámicas previas de identidad C05 y cuatro de acceso excepcional C06-06 (79), con proyecciones positivas recursivas y cobertura exacta comprobada al iniciar el módulo. Contratos HTTP independientes de permisos. Branding público ya tipado, tres rutas de registro con recibo neutral constante y ruta privada de harness tipada y no registrada en Program conservan sus contratos. No endpoints, roles de aplicación ni permisos nuevos, ni nuevos datos. No afirmar que los 79 contratos hayan sido ejecutados por HTTP como operaciones exitosas distintas: 79 ejercitados en .NET; 48 formas de operación exitosa distintas observadas por HTTP en la ejecución E.

## 2. Entrega funcional y seguridad
| Superficie | Resultado |
|---|---|
| Respuesta de operación | Proyección después de autorización; lista positiva exacta, tipos y límites. No serializa directamente objeto arbitrario del servicio, diccionario DB ni entidad con getter no aprobado. |
| Campos anidados | Forma cerrada en sesiones, membresías pendientes, eventos, passkeys y señales. Campos no permitidos, tipos incorrectos o excedentes provocan denegación; datos y metadatos de un resultado denegado se descartan. |
| WebAuthn | Selección explícita de propiedades estándar y anidadas usadas; extensiones/campos desconocidos omitidos; duplicados/tipos inválidos rechazados. Opciones reales Fido2 probadas; protocolo y criptografía existentes intactos. |
| Sesión y MFA | Recibos sellados; token explícito solo en emisión/rotación bearer autorizada; navegador utiliza transporte de cookie HttpOnly y CSRF existente, no token en JSON. Códigos de respaldo limitados a verificación/enrolamiento y confirmación correspondientes; URI/semilla TOTP solo en operación de enrolamiento prevista. |
| Perfil y solicitudes | Perfil/foto propios, referencias privadas y casos continúan sujetos a autorización anterior; correo de solicitud se limita a hint enmascarado, no mailbox completo. Recibos neutrales no exponen existencia ni resultado interno arbitrario. |
| Caché y logs | no-store/private y pragma en módulo; denegación sin payload de servicio, error genérico sin valores rechazados. E verificó logs sin contraseña/seed/token/clave/mailbox/recurso privado en los casos ejecutados. |
| Conservación | Rutina local de disposición lógica, dos clases de cargas sensibles, sin borrar registros de evidencias, claves o estados. |

Arquitectura: API concentra el contrato de transporte; Domain y Application mantienen las decisiones de autorización; Infrastructure y CredentialSecurity intactos. MySQL8.4 dispone carga cifrada en rutina independiente, no por endpoint. Frontend Angular22 y Node24 sin cambios. La proyección no es DLP semántico de cualquier cadena permitida, no sustituye ABAC, y no habilita nuevas capacidades.

## 3. Política local de retención y privilegios
Implementación `022_c06_privacy_disposition.sql`: c06_dispose_closed_sensitive_payloads(p_limit), SQL SECURITY DEFINER, un reloj UTC, transacción con rollback, 1..100 por tabla (hasta 200 filas por llamada), ACK neutro sin conteos ni datos. Solo protected_payload de c05_identity_recovery expirado o consumido y c05_identity_sensitive_outbox expirado o entregado. Preserva vigencia de payload vivo, claves, tenant, digest y metadatos de consumo/entrega. Idempotencia y rollback de rutina probados. El rol c06_privacy_local_worker es EXECUTE-only, offline, sin usuario sembrado, sin automatización ni acceso runtime. Pruebas D confirman que no puede leer payloads/cuentas, escribir cargas ni modificar/eliminar pruebas, ni que API runtime pueda invocar la rutina.

Decisión de diseño: limitar la disposición física fuera de alcance y realizar solo vaciado lógico de payloads cifrados cerrados/caducados en entorno local. Los plazos específicos de C05, sesiones/revocación y caducidad ya definidos se conservan; la especificación registra retención y disposición de otras categorías, sin afirmar eliminación implementada de todas las tablas. Auditoría y consentimiento conservados como evidencia de desarrollo. Retención jurídica/productiva, legal hold, derechos, programación automática, backups/réplicas/binlogs y borrado físico requieren política y autorización separadas; no se certifican aquí. No exponer selectores de tenant/usuarios ni resultado cuantitativo al navegador. No ejecución en datos reales.

## 4. Alcance exacto de archivos
20 archivos técnicos; 1,117 blobs anteriores intactos. Los cinco auditores precedentes solo admiten el alcance exacto de este sucesor y mantienen sus controles anteriores. No se desactiva ninguna regresión ni se modifica el maestro.

| Cambio | Archivo |
|---|---|
| added | `.github/workflows/c06-07-privacy-minimization.yml` |
| added | `backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs` |
| modified | `backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs` |
| added | `backend/tests/Crecisol.C06Privacy.ContractsTests/Crecisol.C06Privacy.ContractsTests.csproj` |
| added | `backend/tests/Crecisol.C06Privacy.ContractsTests/Program.cs` |
| added | `database/migrations/022_c06_privacy_disposition.down.sql` |
| added | `database/migrations/022_c06_privacy_disposition.sql` |
| added | `database/operations/c06_07_local_privacy_worker.sql` |
| added | `database/tests/c06_07_privacy_mysql.sh` |
| added | `docs/c06/C06_07_CONTRATOS_RESPUESTA.json` |
| added | `docs/c06/C06_07_ESPECIFICACION.md` |
| modified | `qa/c05_operational_flow_e2e.mjs` |
| modified | `qa/c06_02_role_model_audit.py` |
| modified | `qa/c06_03_context_audits.py` |
| modified | `qa/c06_04_abac_audits.py` |
| modified | `qa/c06_05_stepup_audits.py` |
| modified | `qa/c06_06_exceptional_scope.py` |
| added | `qa/c06_07_privacy_audits.py` |
| added | `qa/c06_07_privacy_response_checks.mjs` |
| added | `qa/c06_07_privacy_scope.py` |

## 5. Evidencia PRE reproducible por SHA
[PR técnico #208](https://github.com/crecisol-platform/CRECISOL/pull/208), base `e60c8a7642589f511b8beaa977f773524e963ece`, head `d741a9beb10dfdc7a9f11ce5d5193ffb776edb42`. Merge virtual ejecutado `e22610e86a672fcdf92481ea413283d62ca938f8`; commit real dev `b529fe6c66548d5e46e559d441573ae71f9f4cda`; ambos tienen árbol completo idéntico `1df494deaaacadfe4008ac92d0b615cb5adbf60f` y mismos padres. Verificado desde GitHub antes y después de merge. Cinco logs de checkout coinciden con merge virtual. [Workflow #45611001386](https://github.com/crecisol-platform/CRECISOL/actions/runs/45611001386), cinco trabajos concluidos SUCCESS.

| Auditoría | Job real | Evidencia ejecutada |
|---|---|---|
| A — requisitos y trazabilidad | 139194173117 | SUCCESS. Word/PDF SHA-256 y blob, 1,876 elementos del cuerpo original; catálogo y alcance exactos, 79 contratos. |
| B — seguridad y contratos | 139194173059 | SUCCESS. 255 comprobaciones .NET: 79 operaciones, positivos y negativos por operación, WebAuthn real, sobreexposición, getters, diccionario, correo no enmascarado, metadatos inválidos, recibos y límites. |
| C — arquitectura y compilación | 139194172978 | SUCCESS. .NET 10, 0 errores y 0 advertencias; Angular 22/Node 24, assets oficiales y alcance de capas. |
| D — persistencia y privacidad | 139194172467 | SUCCESS. MySQL 8.4 real, dos tenants, 10 operaciones SQL/privilegios rechazadas: tres límites inválidos, seis lecturas/escrituras indebidas y una invocación desde runtime. Disposición acotada, ACK neutro, idempotencia, evidencia y rollback. |
| E — integración real | 139194172947 | SUCCESS. Chromium/Angular/Kestrel/MySQL8.4; 406 comprobaciones de respuestas HTTP, 48 formas de operación exitosa distintas, 50 respuestas de navegador; 423 comprobaciones integradas del recorrido anterior. Los conteos HTTP y de aserciones son métricas diferentes, no una suma de casos únicos. Logs sin secretos/PII indicada en casos ejecutados. |

**5/5 SUCCESS**. Regresiones PRE independientes C05 #45611001325 (340 comprobaciones, 2/2 jobs); C06-04 #45611000816 (351 comprobaciones, 4/4 jobs); C06-05 #45611000958 (359 comprobaciones, 4/4 jobs); C06-06 #45611000916 (423 comprobaciones, 5/5 jobs). C06-01 #45611001083, C06-02 #45611001006, C06-03 #45611001087, C05 SHA1 #45611001333, C05 UX #45611001095, C03 integral #45611001278 y foundation #45611001085 SUCCESS. Total 12 workflows PRE aplicables SUCCESS; cuatro omitidos por condición, no contabilizados como aprobación.

## 6. POST, autonomía y cierre
Registro POST del mismo árbol real al redactar: no atribuir PASS a trabajos pendientes. La evidencia PRE corresponde exactamente al árbol integrado, no a una simulación; decisión de autonomía técnica permite aceptación propia sustentada en evidencia real y misma identidad de árbol, sin atribuir certificación productiva ni firma humana. Si una regresión POST esencial falla, detener siguiente fase, investigar, corregir y revalidar; no convertir fallo de infraestructura en PASS.

| Workflow | Nombre | Estado observado |
|---|---|---|
| 45613536752 | CRECISOL C06-02 prepared roles and scoped capabilities A-B-C | in_progress |
| 45613536961 | CRECISOL C05 institutional modals A-B-C UX security | in_progress |
| 45613536713 | CRECISOL C03-10 integral A-B-C chapter gate | in_progress |
| 45613536613 | CRECISOL C06-06 exceptional access five audits A-B-C-D-E | in_progress |
| 45613536801 | CRECISOL C03-06 explicit Branding DI A-B-C MySQL8.4 | in_progress |
| 45613536699 | CRECISOL foundation check | completed / success |
| 45613536580 | CRECISOL C06-04 ABAC ownership and same-branch IDOR A-B-C-E2E | queued |
| 45613537229 | CRECISOL C06-03 trusted actor tenant membership A-B-C | queued |
| 45613536702 | CRECISOL C06-07 privacy minimization five audits A-B-C-D-E | queued |
| 45613536733 | C05 operational identity Angular API MySQL | queued |
| 45613536814 | CRECISOL C03-05 public API DTO validation A-B-C MySQL8.4 | in_progress |
| 45613536682 | CRECISOL C05 SHA1 only cutover security A-B-C MySQL | in_progress |
| 45613536657 | CRECISOL C06-01 inventory and canonical permission boundaries | in_progress |
| 45613536782 | CRECISOL C05 identity transport preflight | completed / success |
| 45613536815 | CRECISOL C06-05 sensitive operation step-up A-B-C-E2E | in_progress |

## 7. Evaluación como comprador y límites
9.5/10 en alcance técnico, aceptación SÍ: contratos de salida cerrados y negativos efectivos, compilación limpia y regresiones reales, disposición local de payloads y privilegios estrictos, trazabilidad/rollback y segregación de arquitectura. Visual N/A porque no hubo interfaz nueva; regresión UX aprobada. No afirmar ausencia universal de vulnerabilidades, operación productiva ni borrado físico/legal. No contabilizar contratos unitarios como operaciones reales por HTTP. Revertir PR de código solo en dev, revocar EXECUTE/retirar rutina 022 en laboratorio, sin restaurar payload ya dispuesto ni destruir evidencias. No alteraciones a main (SHA b794181148d2c5e36c1c88127f19a8a9082acff5; protección de rama no activada por este trabajo), SOLQARYN, VPS, producción o cobros.

**C06 7/11; capítulos completos 5/37; C06 global NO CERTIFICADO; C06-08 — Aislamiento de listados y paginación, habilitada únicamente tras integración institucional de esta acta. C07 bloqueado.**

## 8. Ratificación tras revalidación de privacidad e ingreso HTTP (25-11-2026)

**C06-07 — REVALIDACIÓN APROBADA, 9.5/10 técnico, aceptación como comprador SÍ en el alcance local auditado.** No se elimina ni reinterpreta el historial. La regresión POST original obligó a detener C06-08. PR #210 corrige el contrato de caché de ingreso de ambos ámbitos de identidad sin cambiar autorización, rutas, MFA, sesiones, frontend, esquema MySQL ni proyección de datos; sustituye la captura Playwright tardía por observación HTTP acotada de las respuestas reales de Kestrel y distingue el recibo browser/restore del inventario de sesiones. La observación del relay no acredita por sí sola recepción en Chromium; el recorrido UI/E2E mantiene sus aserciones independientes.

| Revisión | Resultado histórico y resolución |
|---|---|
| POST original [45613536702](https://github.com/crecisol-platform/CRECISOL/actions/runs/45613536702) | E FAILURE en mfa/enroll; no se pudo determinar retrospectivamente si era captura o contrato. |
| PRE [45619768486](https://github.com/crecisol-platform/CRECISOL/actions/runs/45619768486) | E FAILURE en HTTP 400 de sessions por falta de private; 10 pruebas del oráculo aprobadas. |
| PRE [45621380168](https://github.com/crecisol-platform/CRECISOL/actions/runs/45621380168) | A/B/C/D SUCCESS; seis negativas de ingreso reales aprobadas, E CANCELLED después de logout. No se atribuye PASS a E; espera de captura sin plazo identificada como hipótesis consistente, no como causa probada retrospectivamente. |
| PRE [45630138784](https://github.com/crecisol-platform/CRECISOL/actions/runs/45630138784) | A/B/C/D/E 5/5 SUCCESS; revalidación integral definitiva del árbol exacto. |

### 8.1. Identidad del código y evidencia

PR [#210](https://github.com/crecisol-platform/CRECISOL/pull/210), base `f4383dcffa9f647756410e75bb14770c183361f9`, head `865138fa4c52eb74baede4c7786dd44a720f7912`, merge virtual PRE `750d53e6e26b10953b0f11612b784bc9da7b63b4`, merge real en dev `3789224e49f3183563a7f8491a3b3101f161a6a1`. Árbol completo idéntico `5bf350c9f38e314066890db5da191b437ed8062e`, mismos padres del merge comprobados. Diff técnico exacto: diez archivos (un Program.cs, documentación de especificación, un workflow y siete archivos QA), sin cambios de capas, permisos, datos o Maestro. A continuación los cinco jobs reales del PRE definitivo:

| Auditoría | Job | Evidencia |
|---|---|---|
| A — requisitos y trazabilidad | 139260804038 | SUCCESS; huellas Word/PDF y blob, 1,876 elementos originales intactos, catálogo y alcance. |
| B — seguridad y contratos | 139260804149 | SUCCESS; 255 comprobaciones .NET, 79 operaciones y WebAuthn real. |
| C — arquitectura y compilación | 139260804234 | SUCCESS; .NET 10 sin errores ni advertencias, Angular 22/Node 24 y 16/16 pruebas Node del verificador/relay. |
| D — persistencia y privilegios | 139260803641 | SUCCESS; MySQL 8.4 real, 10 operaciones indebidas rechazadas, disposición, idempotencia y rollback. |
| E — integración completa | 139260804007 | SUCCESS; seis negativas adicionales de ingreso; 506 comprobaciones HTTP, 70 formas exitosas distintas, 144 respuestas con cabecera browser (143 observadas en el relay de navegador), dos enrolamientos MFA y 429 comprobaciones integradas. Logs de API sin credenciales, correo o recurso privado en los casos verificados. |

11 workflows PRE aplicables SUCCESS: 45630138784 (privacidad), 45630138709 (acceso excepcional), 45630138473 (step-up), 45630138747 (ABAC), 45630138640 (C05 operativo), 45630138932 (catálogo), 45630138541 (SHA1), 45630138461 (UX), 45630138422 (C03 integral), 45630138725 (C03-08) y 45630138762 (foundation). Cuatro omitidos por condición: 45630138496, 45630138673, 45630138742, 45630138726; no se contabilizan como aprobación. Los conteos no representan casos únicos sumables.

### 8.2. Dictamen y límites

Ratifico 9.5/10 y aceptación SÍ por la cobertura real de la privacidad, negativa de ingreso, separación de arquitectura, estabilidad de contratos, pruebas de persistencia y regresiones aplicables. Evaluación visual N/A: no hay cambios de interfaz; la regresión UX sí pasó. No se identifica P0/P1 abierto en la evidencia ejecutada; no equivale a auditoría universal ni a certificación productiva. Los fallos históricos se conservan; la nueva evidencia resuelve el bloqueo técnico sin atribuir éxito a ejecuciones canceladas. Rollback: revertir únicamente PR #210 en dev y reabrir inmediatamente el bloqueo C06-08; no desactivar las políticas de privacidad para aprobar pruebas.

POST del merge técnico observado al redactar: 14 workflows registrados (2 SUCCESS, 0 FAILURE, 12 sin conclusión, 0 con otra conclusión); privacidad #45633914842. No se considera PASS una repetición pendiente; si falla una regresión esencial, detener la siguiente microfase y corregir. La ratificación técnica se apoya en PRE real del árbol integrado idéntico y el protocolo de autonomía del propietario. main permanece en `b794181148d2c5e36c1c88127f19a8a9082acff5`; no se activó protección de rama, ni se modificó SOLQARYN, Maestro, VPS, producción o cobros.

**C06 7/11; capítulos completos 5/37; C06 global NO CERTIFICADO. C06-08 habilitada para desarrollo propio secuencial tras integración del presente registro; C06-09 a C06-11 pendientes y C07 bloqueado.**
