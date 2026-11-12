# CRECISOL — C04-01 · Acta de preparación técnica y tres auditorías

**Fecha:** 12 de noviembre de 2026 (Honduras).  
**Proyecto:** CRECISOL; repositorio autorizado `crecisol-platform/CRECISOL` (ID `1450414592`), destino `dev`.  
**Microfase:** C04-01 — catálogo confiable de tenants. **Estado:** PREPARACIÓN DOCUMENTAL AUDITADA, NO IMPLEMENTADA, NO CERTIFICADA.  
**Autoridad de entrada:** C03 certificado institucionalmente 10/10 en [PR #81](https://github.com/crecisol-platform/CRECISOL/pull/81#issuecomment-6812682477), SHA `30c976e1db9add6ea00b07549c34f3c50d98dc31`, PREMERGE y POSTMERGE aprobadas.

## 1. Objetivo y alcance que se prepara

La fase define, sin activación de código de negocio, los contratos de identificación y ciclo de vida de tenant, nombres públicos, modalidades de dirección, privacidad de DTO y referencias opacas de ubicación de datos. Evita crear tenants desde `Host`, usar `tenant_id` de cliente como permiso o devolver DSN, secretos e identificadores de base de datos.

**Archivos del expediente:** `docs/C04_PREPARACION.md`, `docs/c04/C04_01_ESPECIFICACION.md`, `qa/c04_01_preparation_audits.py`, `.github/workflows/c04-01-preparation.yml`, más esta acta. No se modifica `frontend/`, `backend/`, `database/`, los contratos C03, Word/PDF canónicos, `main` o SOLQARYN.

## 2. Evidencia canónica original, comprobada en runner

El auditor leyó `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` real y verificó sus pins:
- Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`.
- PDF SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`.
- Capítulo 04 localizado entre párrafos `1735..1807`, `6 680` caracteres de texto normalizado, SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`.

Su contenido observado impone tres modalidades de dirección (ruta, subdominio, dominio propio); slug único, reserva sensible y redirección canónica; alta no parcial e idempotente con dueño/plan/contrato/defaults; login sin código de empresa y rechazo de `tenant_id` aportado por navegador; backend debe filtrar por tenant y sucursal. El desglose exacto normativo `MAE-C04` completo requiere la revisión restante de su tabla antes de aprobar **implementación**.

## 3. Auditoría A — trazabilidad: PASS PREPARATORIO

La ejecución [#43223824091](https://github.com/crecisol-platform/CRECISOL/actions/runs/43223824091) sobre el commit preparatorio `06b8f6becf060dc7c6aea40e313a1bb6e1ec4c30` mostró `PASS C04-01 A`: Word/PDF por SHA, límites del capítulo C04, C03 certificado y 18 negativas **definidas**. También pasó `qa/c03_10_integral_audits.py A`. La matriz `MAE-C04 -> C04-01` aún está sujeta a precisión línea por línea; por ello este PASS **no certifica la microfase funcional**.

## 4. Auditoría B — seguridad: PASS DOCUMENTAL / RUNTIME NO EJECUTADO

El mismo run registró `PASS C04-01 B`: **8/8 mutaciones deliberadas del texto de límites de confianza** fueron rechazadas por el validador. La matriz de 18 ataques (T01..T18) cubre Host/Forwarded, DSN, identidad, estados, duplicados, slug, suspensión, información pública, caché, roles, migraciones y separación comercial. No se realizaron pruebas de penetración ni acceso cross-tenant sobre un catálogo operativo: **todavía no existe catálogo implementado**. Los riesgos P0 y P1 de host, conexión, autorización y concurrencia siguen ABIERTOS para C04-01 y sucesoras.

## 5. Auditoría C — arquitectura y CI: PASS PREPARATORIO

El mismo run registró `PASS C04-01 C`, verificando evidencia A/B con `GITHUB_SHA/GITHUB_RUN_ID` de la misma ejecución, ausencia de endpoints tenant privados y separación del futuro Domain/Application/Infrastructure/API. [Foundation #43223824160](https://github.com/crecisol-platform/CRECISOL/actions/runs/43223824160) terminó SUCCESS. **No se construyó ni probó nuevo runtime C04, migración MySQL, aislamiento de tenants o interfaz Angular por esta preparación.**

### Hallazgo registrado y reparado, sin falsificar historial

El run inicial [#43223607802](https://github.com/crecisol-platform/CRECISOL/actions/runs/43223607802) falló correctamente porque la especificación todavía no contenía las huellas Word/PDF exigidas. Se añadieron esas huellas y una matriz de requisitos observados del capítulo 04. El nuevo run #43223824091 **SUCCESS** acredita la corrección; **no** se convierte el run fallido en aprobado.

## 6. Condiciones finales para integrar el expediente

- La versión FINAL del PR #82 debe completar su propia CI PREMERGE en SUCCESS, con revisión de diff, pruebas sin permisos de escritura y separación de ramas. Los checks omitidos por condición no equivalen a SUCCESS.
- Fusión controlada únicamente a `dev`, registrar SHA del merge y CI POSTMERGE del nuevo SHA. A/B/C preparatorias y Foundation deben aprobar; registrar run IDs exactos en comentario del PR.
- Aun después de fusionar, el dictamen será **C04-01 PREPARACIÓN TÉCNICA VALIDADA**, no **C04-01 CERTIFICADA** ni capítulo C04 implementado. C04-02..12 y C05 seguirán BLOQUEADAS.

## 7. Impacto, riesgos y reversibilidad

**Impacto de producto:** nulo en endpoints, tablas, migraciones, autenticación privada y producción. **Riesgos remanentes:** validación normativa íntegra de Word C04, decisión ADR-002/004, política de nombres/host, prueba concurrente e aislamiento MySQL; se prohíbe pasar a producción o crear tenants sin esas pruebas.

**Rollback:** abandonar PR, o revertir exclusivamente su merge documental y de CI de preparación por SHA si una verificación falla. Conservar intactos historial C03, maestros Word/PDF, main, SOLQARYN, despliegues y servicios.

**Aceptación:** el propietario autorizó continuar con la siguiente microfase y exigió prioridad a la calidad. Esa instrucción no sustituye pruebas inexistentes ni amplía por sí sola el alcance formal a C04-02.
