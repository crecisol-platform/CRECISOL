# CRECISOL — Acta final C06-06: acceso excepcional del SuperAdmin

**Fecha:** 25-11-2026, Honduras. **Dictamen: C06-06 CERTIFICADA EN SU ALCANCE TÉCNICO, 9.5/10. SÍ aceptaría/compraría esta microfase delimitada.** Visual N/A (sin UI nueva; regresiones UX aprobadas). Eficacia institucional al integrar este acta en dev, sin nuevas fallas esenciales conocidas; C06 6/11, capítulos completos 5/37, C06 global NO CERTIFICADO. C06-07 habilitada únicamente para su propio desarrollo secuencial tras el cierre; C07 bloqueado.

## 1. Autoridad, requisito y alcance
Repositorio original ID 1450414592, ubicación vigente crecisol-platform/CRECISOL, dev. AGENTS.md, Skill de gobernanza y Documento Maestro Único Word blob fe7aaab60b603f1844e18666e2f99c0ea97e86e9, SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. Plan secuencial C06 y controles C06-01/02 (catálogo/roles), C06-03 (contexto), C06-04 (ABAC), C06-05 (MFA fresco). SuperAdmin no tiene acceso privado permanente por su rol. Procedimiento separado, denegación por defecto, justificación, MFA, plazo y auditoría.

**Alcance real:** una referencia privada del recurso C05 existente, en composición local aislada, no acceso general a PII ni panel de gestión productivo. Decisiones de diseño adoptadas: doble control, 10 minutos absolutos desde apertura, sin renovación, nueva prueba MFA por lectura. Elegibilidades REQUEST/REVIEW independientes y exactas, máximo 30 días, revocables; no son permisos de lectura. Emisor offline confiable distinto del beneficiario, registro institucional de incidente fuera de banda, sin semilla ni emisión desde runtime. No se atribuye MFA HTTP al emisor offline. Cierre desde la sesión original sin nuevo código para facilitar revocación.

## 2. Funcionamiento entregado
| Transición | Control / resultado |
|---|---|
| Abrir | Operador verificado, elegibilidad REQUEST exacta, recurso/tenant/sucursal activos, motivo 12–512 caracteres y MFA fresco; devuelve solicitud pendiente sin referencia privada. |
| Aprobar | Otro operador con elegibilidad REVIEW exacta, confirma tres selectores y motivo original por HMAC, justifica revisión y presenta MFA fresco; aprobación no entrega datos ni extiende plazo. |
| Leer | Actor, sesión y revisión originales; ambas elegibilidades y sesiones vigentes, recurso activo, aprobación independiente, no cierre, UTC y MFA fresco; devuelve exclusivamente display_reference con no-store/private. |
| Cerrar | Solicitante en sesión original; clausura irreversible incluso si expiró elegibilidad, si su sesión continúa válida. |
| Denegación | Contexto ajeno, ID arbitrario, motivo inválido, falta de MFA, replay, ausencia de aprobación, expiración, revocación o fallo de auditoría bloquean respuesta. |

Cuatro POST bajo /api/v1/platform-identity/breakglass/ (open, approve, read, close), realm PLATFORM de C05, opt-in independiente CRECISOL_C06_BREAKGLASS_ENABLED=true, verificado en API y servicio, además de composición local C05. No flag, no registro ni autorización. Catálogo original 80 HTTP/12 internos/28 acciones normativas preservado; overlay cuatro acciones explícitas (tres normativas más revisión independiente); sin concesión por rol general ni activación de otras 25 futuras. Sin token excepcional reutilizable. Procedimiento de provisión/revisión/lectura/cierre/auditoría offline en C06_06_ESPECIFICACION.md.

## 3. Arquitectura, seguridad y archivos
Domain puro; Infrastructure ejecuta transacción, guardas, consultas parametrizadas FOR SHARE, verificación MFA C05 y reloj UTC MySQL; API valida contrato y no-store; database aporta FK y restricciones; QA independiente. Consulta final vuelve a verificar sesiones, actor/revisión, ambas elegibilidades, recurso, plazo y cierre. Cuotas heredadas y cuota específica de excepción, criptografía y política de inactividad C05 sin cambios.

Cinco tablas c06_exception_entitlement, c06_exception_request, c06_exception_approval, c06_exception_close, c06_exception_audit y 14 triggers. FK por tenant/sucursal/recurso, no autoaprobación ni ampliación, revocación irreversible, auditoría atribuible actor/sesión/transición e inmutable frente a DML comprobado. Runtime SELECT-only para elegibilidad e INSERT-only para auditoría; no leer/modificar/eliminar ledger. Razones en HMAC, códigos y motivos sin texto claro; motivo original custodiado en incidente institucional. Digest no verifica veracidad semántica. La respuesta privada depende de transacción y ambos inserts de auditoría C05/C06. Petición malformada/anónima no se presenta como evento autenticado.

**22 archivos técnicos:**
| Capa | Ruta |
|---|---|
| API | backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs |
| Domain | backend/src/Crecisol.Domain/Authorization/C06ExceptionalAccessPolicy.cs |
| Infrastructure | backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs |
| Infrastructure | backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs |
| .NET QA | backend/tests/Crecisol.C06ExceptionalAccess.ContractsTests/Program.cs |
| .NET QA | backend/tests/Crecisol.C06ExceptionalAccess.ContractsTests/Crecisol.C06ExceptionalAccess.ContractsTests.csproj |
| DB | database/migrations/021_c06_exceptional_access.sql |
| DB | database/migrations/021_c06_exceptional_access.down.sql |
| DB | database/operations/c06_06_local_runtime_role.sql |
| DB QA | database/tests/c06_06_exceptional_mysql.sh |
| Docs | docs/c06/C06_06_CATALOGO_ACTIVACION.json |
| Docs | docs/c06/C06_06_ESPECIFICACION.md |
| E2E | qa/c05_operational_flow_e2e.mjs |
| E2E | qa/c06_06_exceptional_access_checks.mjs |
| Audits | qa/c06_06_exceptional_audits.py |
| Scope | qa/c06_06_exceptional_scope.py |
| Regresión | qa/c06_01_permissions_catalog_audit.py |
| Regresión | qa/c06_02_role_model_audit.py |
| Regresión | qa/c06_03_context_audits.py |
| Regresión | qa/c06_04_abac_audits.py |
| Regresión | qa/c06_05_stepup_audits.py |
| CI | .github/workflows/c06-06-exceptional-access.yml |

1101 blobs previos idénticos, incluidos 217 protegidos (maestro, frontend, criptografía, migraciones anteriores, catálogo y contexto/ABAC). Se reconstruyó byte a byte la fuente anterior de los dos archivos de producto existentes y cinco auditores históricos al retirar exclusivamente las adiciones C06-06. No se eliminó un control anterior. El cierre documental no modifica producto.

## 4. Identidad exacta y evidencia
[PR #206](https://github.com/crecisol-platform/CRECISOL/pull/206), base 50d880ed5817a6b48cd112ad6f65f4ae581b8761, HEAD 17d7dc5cb430c40843debfb3d9231b7a5f75395d. Merge virtual ejecutado 3862456be2dd01d7e7955f6fbc45b7daf678fbe6; merge real en dev 37370beebbce8188f426df6a11e610615e5df665. **Árbol Git completo idéntico 3ae2797f91d73617af6ef4fbc5836b7d57ef8fc4, mismos padres base y HEAD.** Logs de los cinco jobs y tres regresiones integradas confirman checkout del merge virtual exacto; comparación por API Git antes/después de fusionar. [PRE #45553531887](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553531887) 5/5 SUCCESS. 12 workflows aplicables SUCCESS; 6 SKIPPED no contados como PASS.

Repetición POST C06-06 [#45607921102](https://github.com/crecisol-platform/CRECISOL/actions/runs/45607921102): in_progress, intento 1 al redactar. Regresión C06-02 POST [#45607920983](https://github.com/crecisol-platform/CRECISOL/actions/runs/45607920983): primer intento B falló con ERROR 2002 de socket MySQL durante arranque; reejecución del trabajo fallido sobre el mismo SHA solicitada, in_progress, intento 2. No se atribuye PASS a una ejecución pendiente ni se confunde el fallo de conexión con una aserción de seguridad superada. Conforme a delegación técnica documentada, la certificación se apoya en las pruebas reales del árbol completo exacto integrado, no en una promesa de repetición POST. Una falla esencial posterior exige reabrir y corregir antes de otro cierre. No se inventan resultados POST ni firma humana. La palabra CANDIDATA en la especificación conserva su instantánea histórica, requerida por el auditor de código; el acta y estado vigente determinan el dictamen efectivo.

## 5. Cinco auditorías reales
| Auditoría | Evidencia |
|---|---|
| A — Requisitos y trazabilidad | SUCCESS job 139000043152; Word original, 70 párrafos C06, inventario histórico + overlay y alcance exacto. |
| B — Seguridad y autorización | SUCCESS job 139000043060; 64 contratos .NET10 (política pura; no son todos pruebas criptográficas). |
| C — Arquitectura y calidad | SUCCESS job 139000043029; capas separadas, .NET10 sin errores/advertencias de compilador, Angular22/Node24 tipos/build, JS syntax, 217 archivos protegidos intactos. |
| D — Persistencia y privacidad | SUCCESS job 139000042822; MySQL8.4 independiente, 34 operaciones SQL/privilegios hostiles rechazadas por códigos esperados, integridad, positivos y rollback. |
| E — Integración real y regresiones | SUCCESS job 139000042944; Chromium/Angular/Kestrel/MySQL8.4, 423 comprobaciones integradas, 64 específicas C06-06. |

Regresiones del mismo merge virtual: [C06-05 #45553532195](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532195) 4/4, 359 E2E; [C06-04 #45553532045](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532045) 4/4, 351 E2E; [C05 #45553532189](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532189) 2/2, 340 E2E. [C06-01 #45553532209](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532209), [C06-02 #45553532286](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532286), [C06-03 #45553532178](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553532178) 3/3 cada una. C05 SHA1 #45553531851 SUCCESS; C05 UX #45553532112 5/5; fundamento #45553532005, C03 integral #45553531865, C04-02 #45553532074 SUCCESS. Recorridos con solapamiento: no sumar como pruebas únicas. Entorno CI real aislado y descartable, no runtime ejecutado por el chat ni producción.

## 6. Casos ejecutados y resultados
| Caso | Resultado real |
|---|---|
| Anónimo, rol global sin elegibilidad, ID arbitrario, sesión/host tenant | Denegados |
| Empresa, sucursal, recurso ajenos | Denegados |
| Motivo inválido, MFA ausente o inválido | Denegados |
| Apertura autorizada | Pendiente, sin dato privado |
| Pendiente sin aprobación, autoaprobación, revisor sin grant | Denegados |
| Motivo/selector alterado, MFA de revisión ausente | Denegados |
| Aprobación independiente | Aprobada, sin dato privado |
| Aprobación duplicada, revisor intenta leer, otra sesión del mismo solicitante, selector extra | Denegados |
| Replay prueba de apertura | Denegado |
| Fallo real de permiso INSERT de auditoría | Denegado; tras restaurar permiso, prueba no consumida permite lectura autorizada |
| Lectura legítima TOTP SHA1 real | Referencia única, no-store/private, contador consumido |
| Replay de TOTP de lectura | Denegado |
| Cierre ajeno, cierre legítimo y lectura posterior | Denegado, permitido, denegado |
| Cuatro transiciones positivas y rechazos autenticados | Auditados |
| Fixture aprobado realmente vencido, no cerrado | Denegado |
| Revocación sesión revisor, elegibilidad REVIEW/REQUEST | Denegadas lecturas/apertura posteriores |
| SQL ampliación, borrado, reactivación o mutación de bitácora | Rechazados |
| Runtime emite/altera elegibilidad o enumera/elimina bitácora | Rechazados |
| Rollback 021 antes de 020/C05 | Tablas excepcionales retiradas, recursos/operadores/membresías anteriores preservados |

D incluye autoemisión, FK cross-tenant, capacidad inválida, vigencia excesiva, sesión ajena, confusión REQUEST/REVIEW, autoaprobación incluso con grant de revisión, revisor ajeno, extensión de un microsegundo, falsos eventos positivos, cierre ajeno, duplicado, inmutabilidad/revocación y siete rechazos por privilegios reales. Las 64 específicas de E incluyen pruebas SQL y rollback; D es recorrido separado.

## 7. Incidencias y correcciones
- [#45551835457](https://github.com/crecisol-platform/CRECISOL/actions/runs/45551835457): dos UTC_TIMESTAMP(6) del fixture histórico diferían por microsegundos; MySQL rechazó la extensión correctamente. Se usó un único reloj/vencimiento y comprobación de aprobado, expirado y no cerrado. Sin relajar trigger.
- Refuerzo de atribución actor/sesión/transición del ledger, TOTP RFC6238 SHA1 real, no-store y auditoría D con códigos SQL específicos.
- [#45553396751](https://github.com/crecisol-platform/CRECISOL/actions/runs/45553396751): delimitador de nuevo trigger incorrecto; corregido y guarda de 14 triggers/14 terminadores. No se cuentan rondas fallidas como aprobadas.
- POST C06-02 primer intento de B encontró ERROR 2002 durante arranque de MySQL. Se reejecutó solo B, mismo código; el registro de resultado debe conservarse sin atribuir éxito anticipado.

## 8. Dictamen de calidad y límites
**9.5/10 técnico; SÍ compraría esta microfase delimitada para integración de desarrollo.** Cinco gates reales, ausencia de P0/P1 identificados en alcance auditado, positivos/negativos y trazabilidad exacta. Calificación es juicio técnico de aceptación, no garantía absoluta ni medición estadística. Visual N/A: ninguna UI nueva, C05 UX aprobada. No rendimiento productivo ni estrés concurrente exhaustivo ni Dedicated DB; no se certifican UI de incidentes, gestor productivo de elegibilidades, acceso masivo a PII ni datos reales. Autoridad offline y custodia de motivo original requieren procedimiento organizacional; inmutabilidad no cubre a un DBA que cambie esquema/triggers. Máximo 10 minutos centralizado en política/DB; no configurable desde UI. Decisiones de diseño de doble control y plazos documentadas.

## 9. Reversión y estado
Desactivar opt-in; revert controlado de #206 en dev y rollback 021 antes de 020/C05 exclusivamente en DB descartable, conservar evidencias y repetir pruebas. Nunca DROP de datos reales. main b794181148d2c5e36c1c88127f19a8a9082acff5 intacta, protección API false; no alterar configuración. Sin escritura a SOLQARYN, VPS, producción, cobros ni credenciales reales.

**C06-06 CERTIFICADA EN SU ALCANCE TÉCNICO, 9.5/10; C06 6/11, capítulos 5/37; C06 global NO CERTIFICADO.** Siguiente C06-07 — Privacidad y minimización de DTO: proyección estricta, retención definida, no serializar PII extra ni campos internos. Todavía no desarrollada. Aceptación técnica delegada, no firma humana. Cada microfase debe completar implementación, cinco auditorías y cierre detallado antes de avanzar.
