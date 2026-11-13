# CRECISOL — C04-12 · Especificación del cierre integral del capítulo 04

**Fecha de preparación:** 12-11-2026 (Honduras). **Estado:** PREPARACIÓN; C04-12 **NO CERTIFICADA**; capítulo C04 **NO CERTIFICADO**. **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592), base `dev` `90c63108f5d9b38dd57399395b88af215e08b6ec`. Ramas de trabajo: `feature/c04-12-integral-chapter-gates-20261112` (técnica) y `feature/c04-12-preparacion-integral-20261112` (documental).

## 1. Autoridad y límite de la certificación

- **Único Documento Maestro:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`. Su edición vigente tiene **Git blob SHA-1 `fe7aaab60b603f1844e18666e2f99c0ea97e86e9` y SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`**; PDF derivado Git blob `93c8412b2450e85a882b622e7a012477c61c2359`, SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`, conforme a `docs/MASTER_REFERENCE.md` y árbol GitHub de `dev`. Los dos binarios se conservarán exactamente.
- Normativa oficial `docs/PLAN_37_CAPITULOS.md` y `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`: **C04-12 «Auditorías integrales y acta C04»**, positivo **«A/B/C, matriz de fallos P0 y aceptación»**, negativo bloqueante **«No habilitar C05 con fugas abiertas»**.
- La [ratificación institucional de C04-11](https://github.com/crecisol-platform/CRECISOL/pull/108#issuecomment-6842233364) acredita **22/22 SUCCESS** sobre seis puertas, únicamente para pruebas ofensivas de contratos y MySQL sintético. Las C04-01..10 disponen de actas en sus respectivos alcances; deben correlacionarse con la matriz y ejecutarse de nuevo las regresiones antes de cerrar C04.
- Las etiquetas `CERTIFICADO`, `SINTÉTICO` y `IMPLEMENTADO EN PRODUCCIÓN` **no son equivalentes**. C04-12 permite **exclusivamente** dictaminar los fundamentos/contratos backend del capítulo y los fixtures de seguridad, sin afirmar autenticación HTTP real, rutas privadas, DSN de clientes, DNS real, Cloudflare, backups de clientes ni autorización administrativa excepcional aún inexistentes. Si el cierre completo del capítulo pretende incluir esas capacidades, el dictamen deberá permanecer **NO CERTIFICADO / pendiente de integración futura** hasta aportar pruebas reales.

## 2. Matriz de microfases auditables y barreras negativas

| Fase | Control de la matriz | Evidencia positiva en `dev` y en CI C04-12 | Negativo crítico |
| --- | --- | --- | --- |
| C04-01 | Catálogo confiable de tenants | Contratos `.NET10`, catálogo sin DSN, estado, nombre/host autorizados | Host no crea empresa ni revela DSN |
| C04-02 | Shared DB | Esquema MySQL 8.4 e índices/FK compuestas, lector restringido | No cruzar FK y no lectura SQL ajena |
| C04-03 | Dedicated | Puertos de placement y slot tipados elegidos por el servidor | No usar DSN/slot/Host aportado por usuario |
| C04-04 | Hosts verificados | Allowlist, unicidad, ciclo de validación | Forwarded sin validar, wildcard, host duplicado denegados |
| C04-05 | Identidad tenant | Sesión verificada, membership, Host y catálogo | tenant_id/claims de cliente no son permiso |
| C04-06 | Branch y resource | Acción/owner/grant exactos y revalidación | BOLA/IDOR; no heredar permiso de otra sucursal |
| C04-07 | Provisión idempotente | `.NET10` y MySQL 8.4, reintentos y carreras | Doble empresa, replay con identidad distinta |
| C04-08 | Lifecycle | Transiciones, suspensión, reactivación, epoch/retención | Revocación no conserva permisos ni borra datos prematuramente |
| C04-09 | Migraciones | Shared/Dedicated, fencing, rollback, dump/restore sintético | No migrar/restaurar tenant equivocado |
| C04-10 | Artefactos privados | Keys HMAC, cache/job/media/export, MySQL 8.4, 12 carreras | No contaminar cache, colisionar variantes o reusar recibos de B |
| C04-11 | Ofensiva | .NET y SQL sobre dos tenants válidos, Host/claims/DSN falsificados | No leer, cambiar o borrar filas de B |
| C04-12 | Cierre integral | QA independiente A/B/C sobre las once fases + builds/regresiones completas | No cerrar C04 ni habilitar C05 con P0/P1 o CI pendiente |

## 3. Clasificación de riesgos transversales

**P0-BLOCK:** escalación de tenant, consulta o escritura cruzada; Host/claims/DSN seleccionan identidad o ubicación; reactivación conserva acceso; jobs/media/export cruzados; provisión duplicada; backups del ámbito incorrecto; SuperAdmin ve datos privados sin step-up. **P1-BLOCK:** FK sin tenant, permisos DB amplios, datos secretos/PII en claves/logs, doble job, parser host ambiguo, replays caducados, contador de pruebas no ejecutado, dependencia de producción o cambio de rama no autorizado.

**Ámbito auditado ahora (APLICA):** solo contratos Domain/Application y pruebas integradas sintéticas reales, con catálogo confiable simulado, SQL MySQL 8.4 desechable y APIs públicas de C03. Debe haber **0 P0/P1 vigentes** dentro de ese ámbito tras A/B/C y suites completas. **Ámbito NO APLICABLE A CERTIFICACIÓN PRODUCTIVA:** login/MFA JWT reales, consola privada, autorización transaccional física en DB/cola/archivo, integraciones DNS/Cloudflare, despliegue/VPS, medios y backups reales, pagos, comercio y controles posteriores C05-C08. Sus riesgos continúan declarados como **pendientes de implementación/prueba futura**, no como `PASS`. El cierre sintético NO autoriza un endpoint privado.

**Comercial COM-001..014:** la ampliación del Maestro aporta diseños pero COM-T06/07/09/17 se mantienen **DEFINIDAS / NO EJECUTADAS**. No habilitar slugs comerciales, facturación/contratos, altas de pago, reservas de DNS ni reactivación por cobros.

## 4. Ejecución integral y tres auditorías

**A — cobertura/funcional:** verificar el SHA-256 de Word+PDF contra `MASTER_REFERENCE` y estructura C04 del Word, existencia y trazabilidad de las 11 actas, tabla del plan, proyectos/fixtures y pasos de pruebas; ejecutar QA A de C04-01..11, compilación API .NET 10 Release `-warnaserror`, pruebas de contratos y regresiones C03-10.

**B — seguridad multitenant:** ejecutar QA B de las once fases y auditorías D/E/F adicionales C04-09; pruebas negativas reales `.NET10` y **todos los fixtures aislados MySQL 8.4** para Shared, Dedicated, aprobación/provisión, suspensión, migraciones, cache/job/media/export y ofensiva; exigir cero filtraciones, cero mutaciones entre A/B, carreras idempotentes y revocación real de test. No confundir mutaciones estáticas con penetración productiva.

**C — arquitectura/rendimiento/documentación:** verificar código Domain/Application/Infrastructure/API, no introducir adaptadores ni dependencias fuera de alcance; regression Python/Node/Angular 22 con Chromium del gate C03-10, comprobar recursos desechables, secrets, SHA exacto, esquema sin migración productiva, rollback y límites formales; verificar QA C de C04-01..11, además de la C04-12.

**Nuevos archivos autorizados:** `qa/c04_12_integral_audits.py`, `.github/workflows/c04-12-integral.yml`, esta especificación, matriz de hallazgos y futura acta/ratificación institucional. `frontend/`, `backend/src/`, `database/` productiva, DOCX/PDF Maestro, `main`, VPS, cuentas Cloudflare y SOLQARYN intocables. No se ejecutan referencias de Skills externas.

## 5. Puertas y aceptación condicionada

1. CI PREMERGE técnica sobre SHA final: A/B/C y todas las regresiones C04-01..11, más C03-10 integral, `completed/success`, ninguna `skipped` obligatoria.
2. Revisar SHA/diff limitado, secretos/impacto/rollback y casos negativos. Si hay error, corregir y repetir CI nueva sobre SHA nuevo preservando fallos pasados. Integrar técnicamente **solo a `dev`**; exigir CI POSTMERGE de todas las puertas aplicables.
3. Incorporar SHA/runs reales y clasificación de cada P0/P1 al acta humana; PR documental con CI PREMERGE, fusión solo a `dev`, CI documental POSTMERGE y registro institucional de aprobación.
4. Ratificar aparte el estado del capítulo para no dejar estados `CONDICIONADOS` sin sustituir; publicar comentario institucional final con **SHA exacto, runs, 12/12, limitaciones y aceptación**. C04 no se considera CERTIFICADO hasta que todas esas puertas sean eficaces. C05 sigue **BLOQUEADO** y solo podrá prepararse después del dictamen de cierre acotado.

**No certificar el capítulo como plataforma productiva** sin los controles privados reales; la aceptación prevista por gobierno se aplicará solo al alcance verificable. Cualquier P0/P1 vivo, prueba obligatoria no ejecutada, Word/PDF no verificable o dependencia rota => NO-GO, NO CERTIFICADA, C05 BLOQUEADO.

## 6. Reversión

Revertir el squash técnico/documental y eventual ratificación únicamente en `dev` tras análisis de dependencias; no hay DDL nuevo ni datos persistentes. Mantener fallos históricos; no editar `main`, el Documento Maestro Único, DNS, Cloudflare, servidores productivos, cobros ni `solqaryn/Solqaryn`.
