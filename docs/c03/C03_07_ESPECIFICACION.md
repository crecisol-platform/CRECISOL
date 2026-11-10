# CRECISOL — C03-07 | Especificación de entrada: workers, outbox y eventos idempotentes

**Estado:** EN PREPARACIÓN TÉCNICA, NO IMPLEMENTADA, NO PROBADA, NO CERTIFICADA.  
**Repositorio exclusivo:** `solqaryn-platform/CRECISOL`. **Base de trabajo verificada:** `dev` SHA `101033422015e44c97cc68fa1389e72ad8adcf50`.  
**Precedencia:** C03-01..C03-06 certificadas (6/10); último SHA con 20/20 workflows POSTMERGE `push/completed/success`. C03 NO certificado; C04 bloqueado.
**Fuente de autoridad:** Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; `docs/MASTER_REFERENCE.md`, `docs/C03_PREPARACION.md`, `docs/PLAN_37_CAPITULOS.md`, `AGENTS.md` y `.agents/skills/crecisol-project-governance/SKILL.md`. Sin modificar el maestro.

## Objetivo y corte vertical seguro

Diseñar e implementar **solo una base técnica reusable** de ejecución asíncrona local, sin habilitar procesamiento comercial, autenticación privada, correo, cobros, servicios externos ni tenant routing aún inexistente. Los eventos no se publican externamente. El patrón outbox transaccional y la entrega at-least-once implican consumo idempotente, no promesas de exactly-once.

### Contratos exigibles

1. Sobre de evento inmutable con `eventId`, `eventType`, `schemaVersion` (entero >0), `occurredAtUtc`, `correlationId`, `causationId`, `scope` y payload tipado. Rechazar eventos desconocidos o versiones no admitidas; ningún ensamblado arbitrario ni deserialización polimórfica desde entrada cliente.
2. El scope de plataforma, cuando corresponda a datos globales públicos, es explícito. Todo evento privado futuro requiere `tenantId` validado del contexto de servidor, y `branchId`/recurso cuando apliquen; denegación por defecto si faltan. Ningún consumidor deriva tenant o DSN de payload, URL, cabecera ni claims no verificados.
3. Escritura de evento y agregado de negocio en una sola transacción MySQL; si falla el commit no debe quedar evento publicable. No introducir un falso outbox separado sin atomicidad. Hasta que exista caso de uso de escritura seguro, mantener infraestructura desactivada.
4. Estados persistidos `pending`, `leased`, `delivered`, `retryable`, `dead-letter`; lease con propietario, expiración y recuperación de worker caído. Reserva atómica y concurrencia comprobadas en MySQL 8.4. Prohibido afirmar FIFO global o exactly-once.
5. Clave idempotente duradera por consumidor + evento + tenant, aplicada en el mismo límite transaccional de su efecto, con pruebas de reinicio y duplicación. Las llamadas externas futuras necesitarán claves idempotentes del proveedor y reconciliación separada.
6. Retries con backoff acotado, límite de intentos y dead-letter no destructivo; preservación de trazabilidad y posibilidad de reintento administrativo futuro **sin endpoint activo**. Ningún loop infinito de errores.
7. Logs estructurados sin PII ni payload sensible, con `eventId`, `correlationId`, estado y contexto mínimo; scopes y recursos jamás mezclados en lecturas, métricas, cola, backups o exportaciones.
8. Composición DI explícita, cancellation tokens, shutdown cooperativo, límites de lotes y de paralelismo; ningún Service Locator, reflexión peligrosa, worker productivo por defecto ni conexiones a servicios reales.

## Arquitectura e impacto previsto

- **Domain:** tipos puros de identidad/versión/estado y validaciones invariantes; sin MySQL, ASP.NET, HTTP ni infraestructura.
- **Application:** puertos para outbox/lease/recepción idempotente y un caso de uso de procesamiento mínimo; ninguna dependencia directa de Infrastructure.
- **Infrastructure:** adaptador MySQL 8.4 bajo transacciones verificables; migración aditiva reversible y pruebas con MySQL efímero. Sin tocar tablas C01 salvo necesidad justificada y probada.
- **API/composición:** registro explícito de módulos; worker **opt-in para harness de prueba**, desactivado por defecto; sin ruta HTTP privada o pública nueva.
- **Frontend:** sin cambios.
- **Seguridad/PII:** tenant context de servidor será condición obligatoria antes de habilitar cualquier evento privado; no simular autorización todavía inexistente.
- **Producción:** ningún despliegue, VPS, DNS, servicios externos, cobros, automatizaciones o configuración productiva.

## Matriz mínima de pruebas y tres auditorías

- **Positivas:** serialización con versión soportada; transición válida; transacción agregada/outbox; claim atómico; lease expirado; recovery; reintento y dead-letter; duplicado inocuo; shutdown y límites de concurrencia.
- **Negativas P0:** tenant ausente/forjado/cruzado; sucursal/recurso ajenos; DSN del cliente; evento desconocido/versión inválida; payload hostil o demasiado grande; claim concurrente; ACK de propietario incorrecto; rollback parcial; error entre efecto y ACK; replay; workers reiniciados; datos privados en logs; acceso arbitrario a dead-letter.
- **Auditoría A:** rastreo a Word/maestro y frontera de C03; diff limitado, contratos/DTO/versiones documentados.
- **Auditoría B:** aislamiento multitenant y BOLA/IDOR donde exista superficie real, idempotencia, fallos cerrados, secretos/PII, 12+ mutaciones hostiles verificadas; si no hay auth privada, declararlo expresamente NO PROBADO.
- **Auditoría C:** referencias de proyectos acíclicas, .NET 10 Release, MySQL 8.4 real efímero, concurrencia reproducible, rendimiento basal y regresiones C01/C02/C03-01..06; Angular 22 si el gate histórico lo exige.
- **Gates:** PR técnico PREMERGE y POSTMERGE sobre SHA exacto; acta independiente y CI PREMERGE/POSTMERGE; aceptación humana específica para certificación. Un PASS estático no sustituye pruebas runtime.

## Condiciones de salida y límites

Esta especificación **solo abre el diseño** de C03-07. No constituye implementación, pruebas, aprobación de diseño cerrado ni certificación. Para declarar C03-07 CERTIFICADA deben existir código real, migraciones bajo control, pruebas positivas/negativas y A/B/C verificadas con IDs de CI, diff y SHA exactos, revisión de seguridad y acta aceptada. Hasta entonces conservar estado **C03-07 EN DESARROLLO / NO CERTIFICADA**, **C03 6/10**, **C04 BLOQUEADO**.

**Rollback documental:** revertir el commit específico mediante PR sobre `dev`; esta rama no altera datos ni servicios.
