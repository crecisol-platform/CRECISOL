# CRECISOL — C04-12 · Matriz integral de riesgos P0/P1 y pruebas bloqueantes

**Estado al crear:** MATRIZ DE PREPARACIÓN, no acta de aprobación. Ninguna celda `PENDIENTE` es un `PASS`. Fecha 12-11-2026, `dev` entrada `90c63108f5d9b38dd57399395b88af215e08b6ec`.

## 1. Hallazgos, propietarios de barrera y obligación de evidencia

| ID | Prioridad / superficie | Fallo que debe impedir cualquier certificación | Evidencia real indispensable | Estado pre-gate |
| --- | --- | --- | --- | --- |
| R04-01 | P0 — catálogo/host | Host, slug o headers crean tenant o sustituyen catálogo | .NET C04-01 y host verificado C04-04; auditoría A/B | PENDIENTE de CI integral |
| R04-02 | P0 — shared | Tenant A lee/escribe B, FK no compuesta, lector DB global | MySQL C04-02 shared/application/backend reader, roles restringidos | PENDIENTE de CI integral |
| R04-03 | P0 — dedicated | DSN/slot suministrado por cliente decide ubicación de tenant | .NET C04-03 placement y negativas ofensivas C04-11 | PENDIENTE de CI integral |
| R04-04 | P0 — Host | Allowlist falsa, duplicate Host, Forwarded no autorizado | .NET HTTP/request adapter C04-04 y falsificación C04-11 | PENDIENTE de CI integral |
| R04-05 | P0 — identidad | Claims tenant_id/role SuperAdmin sustituyen membresía | .NET C04-05, C04-11; no endpoints privados productivos | PENDIENTE de CI integral |
| R04-06 | P0 — IDOR | Actor de otra sucursal obtiene grant, lee/escribe recurso ajeno | .NET C04-06 y ofensiva C04-11 con mismo resource_id válido A/B | PENDIENTE de CI integral |
| R04-07 | P0 — provisioning | Duplicación de empresas, replay con identidad/huella distinta | .NET C04-07, MySQL 8.4 12-way idempotencia/rollback | PENDIENTE de CI integral |
| R04-08 | P0 — lifecycle | Suspensión deja sesiones/cachés válidas; reactivación conserva epoch viejo | .NET C04-08, MySQL 8.4 suspensión/concurrencia y reversión | PENDIENTE de CI integral |
| R04-09 | P0 — migración | Shared/Dedicated errado, respaldo de B restaura A o trabajo reaplica | .NET C04-09, MySQL 8.4 compatibilidad, fencing, dump/restore y auditorías D/E/F | PENDIENTE de CI integral |
| R04-10 | P0 — cache/job/media/export | Key o receipt de B autoriza acceso A; carrera genera doble job | .NET C04-10, MySQL 8.4 12-way (1 ganador), variantes/épocas | PENDIENTE de CI integral |
| R04-11 | P0 — ofensiva | BOLA/IDOR, Host, claims, DSN o SQL directo leen/escriben entre tenants | .NET C04-11, MySQL 8.4 A/B con datos víctima intactos | PENDIENTE de CI integral |
| R04-12 | P0 — puerta capítulo | C05 activado sin 12/12 y A/B/C/CI/acta/aceptación | CI C04-12 mismo SHA para todas las once fases + registro final | PENDIENTE de CI integral |
| R04-13 | P1 — evidencia | Prueba `skipped`, fallida, simulada o histórica tratada como PASS | Evidencia GitHub de SHA y run real; 0 failures/pending; auditoría C | PENDIENTE de CI integral |
| R04-14 | P1 — secretos/operación | Contratos introducen rutas, credenciales o worker productivo | Diff exacto, clean layers, docs Maestro hashes, red/puertos efímeros | PENDIENTE de CI integral |
| R04-15 | P1 — compatibilidad | Angular/Chromium o API pública se rompen al cerrar C04 | CI Angular 22/Node24, Chromium, .NET10, MySQL8.4 C03 y C04 | PENDIENTE de CI integral |

## 2. Riesgos de integración productiva: expresamente NO certificables en C04

| Ámbito | Estado e implicación de seguridad |
| --- | --- |
| JWT/MFA real, sesiones/claims firmado y rol SuperAdmin | **NO IMPLEMENTADO/NO PROBADO productivamente**. C05/C06/C07 deben entregar auth de verdad, MFA excepcional, razón/caducidad/auditoría y pruebas ofensivas sobre endpoints existentes. |
| Router físico de Dedicated DB | **NO CONECTADO**. Un scope protegido y slot tipado no validan DSN, permisos reales de cuentas, rotación ni transacciones de clientes. |
| Redis/CDN, jobs/colas reales, outbox tenant, medios, Cloudflare R2/Stream, exportaciones y enlaces firmados | **NO CONECTADOS**. Revalidar autorización, revocación física, atomicidad, URLs temporales, versionado y observabilidad en integración futura. |
| Backup de clientes y recuperaciones reales | **NO EJECUTADOS**. Las pruebas de C04-09 son contenedores desechables, no restauración de clientes ni autorización de almacenamiento. |
| DNS, slug comercial, contratos COM-001..014, paquetes, cancelaciones/pagos/activaciones automáticas | **NO IMPLEMENTADOS** y COM-T06/07/09/17 **NO EJECUTADAS**. C08+ definirán controles comerciales y tests reales. |
| Producción/VPS/DNS, Cloudflare y SOLQARYN | **PROHIBIDOS** en esta etapa. No desplegar, comprar, activar cobros ni modificar `solqaryn/Solqaryn`. |

## 3. Umbrales NO-GO y procedimiento de cierre

- Si R04-01..15 no se verifica **en el ámbito de contratos/fixtures** mediante pruebas A/B/C reales, permanecerá `NO_GO`; reportar archivo y SHA del defecto, remediarlo, repetir CI en SHA nuevo y conservar histórico de fallos.
- R04-01..12 son una matriz de barreras, no una promesa de que todas las superficies estén conectadas. Un éxito sintético no cambia a `PASS` las integraciones enumeradas en 2.
- Aceptación humana/delegación y acta institucional efectivas **solo después** de PREMERGE/POSTMERGE técnicos, PREMERGE/POSTMERGE documentales y ratificación si es necesaria; todas las pruebas obligatorias `completed/success`; no reetiquetar `skipped`.
- **C04 NO CERTIFICADO** hasta que todos los gates y comentarios institucionales estén completos. **C05 BLOQUEADO** mientras haya un solo riesgo P0/P1 aplicable o puerta pendiente. Su habilitación posterior será SOLO para preparación, jamás autorización de producción.
- La certificación del capítulo, si procede, indicará explícitamente `C04 CERTIFICADO ÚNICAMENTE EN ARQUITECTURA/CONTRATOS INTERNOS Y FIXTURES SINTÉTICOS`, con 12/12 en esos alcances, y sin instalar ninguna capacidad productiva nueva.
