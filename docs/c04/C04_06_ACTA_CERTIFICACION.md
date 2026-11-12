# CRECISOL — C04-06 · Acta institucional de contexto de sucursal y recurso (eficacia condicionada)

**Fecha:** 12-11-2026 (Honduras). **Repositorio:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). **Rama receptora:** `dev`. **Microfase:** C04-06, número 6 de 12. **Estado de esta versión:** ACTA PROPUESTA, INEFICAZ sin las cuatro puertas de CI y dictamen permanente. Hasta entonces **C04-06 PENDIENTE, C04-07 BLOQUEADA**.

## 1. Autoridad, secuencia y dependencia anterior

Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Texto normativo C04: **72 párrafos**, huella `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. Las 8 puertas `MAE-C04` del documento no son las 12 microfases técnicas del capítulo.

**C04-05 certificada exclusivamente en contexto interno actor-host-membresía-tenant NO EXPUESTO**, conforme a [acta final #93](https://github.com/crecisol-platform/CRECISOL/pull/93#issuecomment-6834180210), `dev` SHA `148ca91e62ac893629ca89c2515d2285db6e6eef`, 4/4 CI documental POSTMERGE en SUCCESS. Capítulos institucionales completos **3/37**; capítulo C04 todavía NO CERTIFICADO.

Alcance normativo de `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`: **C04-06 Contexto de sucursal y recurso**; positiva **Scopes por branch/owner en backend**; negativa **No heredar permisos de otra sucursal**.

## 2. Cambios realmente integrados, sin exponer tráfico privado

[PR técnico #94](https://github.com/crecisol-platform/CRECISOL/pull/94) HEAD PREMERGE `7a11263543f2c931c9c64f4d1917ee9adfce6b6b`, fusionado exclusivamente a `dev` SHA **`3be65c835cce1cbe35813ccd4ccde4270571cbba`**. Diff revisado **7 archivos nuevos**, todos de C04-06. No se modificaron `Program.cs`, DI, Infrastructure, `frontend/`, `database/`, migraciones, Word/PDF, `main` ni recursos de producción.

- `backend/src/Crecisol.Domain/Tenancy/TenantBranchResourceScope.cs`: `TenantBranchId`/`TenantResourceId` opacos (GUID no vacío, sin permisos), `TenantRequiredAction` parametrizable y canónico ASCII de 2–4 segmentos (`cases.read` o `cases.item.update`; **definido solo por política backend**, jamás por parámetro de navegador), snapshot confiable de recurso + tenant/branch/owner + estado/revisión de sucursal y recurso con UTC; snapshot de concesión estricta actor+tenant+branch+acción, modo OwnerOnly o BranchWide, revisión/estado Pending/Active/Suspended/Revoked y ventana UTC; evidencia resultante tipada con revisiones y diagnostics redactados.
- `backend/src/Crecisol.Application/Tenancy/ResolveCurrentBranchResourceScope.cs`: C04-05 autentica CONTEXTUALMENTE primero mediante puertos asumidos confiables. Consulta recurso por **TenantId autorizado + ResourceId** (ID de recurso = selector NO permiso), deriva branch únicamente del catálogo del recurso, exige recurso **Y branch Active**, concesión única exacta por actor+tenant+branch+requiredAction, modo OwnerOnly obliga match con owner; BranchWide requiere grant explícito de la sucursal. Relee recurso, grant e identidad al final y verifica vigencia/UTC para evitar decisiones de contexto sobre estados cambiantes durante consultas.
- `backend/tests/Crecisol.BranchResource.ContractsTests/`: pruebas `.NET10` reales de lógica Domain/Application con catálogos, identidad y reloj **sintéticos explícitos**; no ejecuta un endpoint privado.
- `qa/c04_06_scope_audits.py`, `.github/workflows/c04-06-branch-resource-scope.yml`, `docs/c04/C04_06_ESPECIFICACION.md`: auditorías A/B/C, mutaciones de guard y evidencia SHA/run de GitHub Actions; runner read-only y sin despliegue.

**No existe implementación productiva de autenticación/JWT/MFA, membresías persistentes, grants de sucursal en DB, estado transaccional real de branch, roles de aplicación, control de recursos/owner por SQL de punta a punta ni API privada.** La evidencia devuelta **NO ES** token, autorización para leer datos o selector de base; solo es prueba interna de correlación de snapshots sintéticos de backend. La relectura no evita carreras posteriores al retorno. C04-11 e integración posterior exigirán autorización atómica por operación y ofensivas HTTP BOLA/IDOR.

## 3. Evidencia de las tres auditorías y PREMERGE técnico

HEAD `7a11263543f2c931c9c64f4d1917ee9adfce6b6b`: **5/5 workflows aplicables `completed/success`**. Otros **3 `skipped`** por filtros condicionales se excluyen del cómputo; no son aprobaciones.

| Control PREMERGE | Run real | Dictamen |
| --- | --- | --- |
| C04-06 A/B/C y .NET10 BranchResource | [43297580984](https://github.com/crecisol-platform/CRECISOL/actions/runs/43297580984) | SUCCESS |
| C04-01 contratos TenantCatalog | [43297580531](https://github.com/crecisol-platform/CRECISOL/actions/runs/43297580531) | SUCCESS |
| C04-02 Shared/.NET/MySQL8.4 | [43297580443](https://github.com/crecisol-platform/CRECISOL/actions/runs/43297580443) | SUCCESS |
| C03-10 integral Angular22/Node24/.NET10/Chromium/MySQL | [43297580534](https://github.com/crecisol-platform/CRECISOL/actions/runs/43297580534) | SUCCESS |
| Foundation | [43297580427](https://github.com/crecisol-platform/CRECISOL/actions/runs/43297580427) | SUCCESS |

**Auditoría A — requisito funcional y trazabilidad:** Word/PDF canónicos, 72 párrafos C04, fila C04-06 original (branch/owner en backend y NO heredar de otra sucursal), dominio de recurso y branch activa, Application sin endpoints privados ni DSN.  
**Auditoría B — seguridad P0/P1:** **122 mutaciones de límites de confianza rechazadas**, **166 aserciones positivas y negativas .NET10**. Tenant A/B, sucursales A/B de misma empresa y empresas separadas, recurso de ID ajeno, owner A/B, modo OwnerOnly vs BranchWide explícito, acción `read` frente a `update`, branch suspendida/desactivada, recurso suspendido/deleted, duplicados/missing/errores en grants, grant suspendido/revocado/caducado/futuro, cambios de owner/branch/revisión/permiso/identidad entre lecturas, UTC inválido, cancelación, error de puertos, pruebas de no filtrado por host/ID/rol del cliente. Los grants de otra sucursal no sirven para la solicitada, **sin herencia ni SuperAdmin implícito**.  
**Auditoría C — arquitectura y ejecución:** compilación real `.NET 10 Release -warnaserror` sin errores, tres auditorías y pruebas en mismo SHA/run; contratos Domain/Application limpios, `Program.cs` intacto, cero nuevas rutas/DSN. Regresiones .NET C04-05 (110 aserciones), C04-04 (128), C04-03 (42), C04-02 (38), C04-01 (88), C03-05 (57) ejecutadas dentro de C04-06; MySQL8.4 y C03-10/Angular/Chromium por workflows propios.

**Fallo de CI C04-06 PREMERGE vigente en HEAD verificado:** ninguno. Ninguna ejecución anterior de otro SHA se reetiqueta. El repositorio mantiene su historial íntegro.

## 4. Puerta POSTMERGE técnica — PENDIENTE de conclusión al redactar

El squash técnico integrado tiene SHA `3be65c835cce1cbe35813ccd4ccde4270571cbba`. Los 7 workflows esperados son:

| Control técnico POSTMERGE | Run iniciado |
| --- | --- |
| C04-06 .NET 10 + A/B/C | [43298613355](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613355) |
| C04-01 Domain/Application | [43298613258](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613258) |
| C04-02 Shared/MySQL8.4 | [43298613707](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613707) |
| C03-02 Domain | [43298613549](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613549) |
| C03-03 Application | [43298613488](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613488) |
| C03-10 Integral | [43298613687](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613687) |
| Foundation | [43298613343](https://github.com/crecisol-platform/CRECISOL/actions/runs/43298613343) |

**No se presume que estos runs estén aprobados.** Deben confirmarse `completed/success` en SHA exacto en GitHub, uno por uno. No se admiten `queued`, `in_progress`, `skipped`, `failure`, `cancelled` como SUCCESS.

## 5. Riesgos P0/P1 diferidos, NO_GO y reversibilidad

La superficie implementada es **interna no conectada**, con fakes de backend; en ella A/B/C y pruebas de los escenarios exigidos no identificaron P0/P1 vigentes. **No extrapolar** esa evidencia a una API real, bases con cambios concurrentes ni producción.

**P0 pendientes ANTES de habilitar datos privados:** validar tokens/sesiones firmadas/revocables, persistencia verificada de membresías, ACL/grants/estado real de sucursal, scoping de operación por tenant+branch+resource+owner+acción en transacción o query, revocación atómica, logs sin PII, medios/caches/colas/exportaciones tenant scoped, host ownership DNS real, BOLA/IDOR HTTP, y excepciones SuperAdmin con MFA+motivo+plazo+auditoría. Esos controles no están certificados por C04-06 y permanecen expresamente bloqueados.

Reversión si hay una incidencia: revertir SOLO el squash técnico `3be65c835cce1cbe35813ccd4ccde4270571cbba` y documentación C04-06 tras revisar dependencias, con regresión real; no existe migración ni dato productivo que revertir. No alterar `main`, Documento Maestro Word/PDF, Cloudflare, VPS, DNS, cobros, facturación o `solqaryn/Solqaryn`.

## 6. Condiciones de eficacia del acta

La presente acta ADQUIERE EFICACIA solamente después de: **(1)** confirmar 7/7 (todos los aplicables) CI POSTMERGE técnica sobre `3be65c83...` SUCCESS; **(2)** aprobar todos los PREMERGE aplicables del PR documental que contiene esta acta, sobre su HEAD exacto; **(3)** fusionar acta exclusivamente a `dev`; **(4)** aprobar TODOS los POSTMERGE documentales del nuevo SHA final de dev; **(5)** publicar comentario institucional permanente con URLs, identificadores de runs/SHAs y revisión de cero hallazgos vigentes del alcance interno NO EXPUESTO. `skipped` no cuenta como aprobado.

**Solo entonces:** `C04-06 CERTIFICADA EN ALCANCE EXCLUSIVO DE CONTRATOS INTERNOS DE SUCURSAL, RECURSO, OWNER Y ACCIÓN` (6/12), `C04-07 HABILITADA SOLO PARA PREPARACIÓN`; capítulo `C04 NO CERTIFICADO`, capítulos completos `3/37`. Mientras falte cualquier puerta: **C04-06 PENDIENTE; C04-07 BLOQUEADA**. No equivale a firma criptográfica ni revisión de seguridad de producción.
