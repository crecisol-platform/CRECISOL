# CRECISOL — C04-10 · Ratificación institucional de certificación (alcance sintético)

**Fecha de ratificación propuesta:** 12 de noviembre de 2026, Honduras. **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592), exclusivamente rama `dev`. **Microfase:** C04-10 — Aislamiento de caché y trabajos, con ensayos de claves, jobs, referencias de medios y exportaciones. **Estado normativo del capítulo:** C04 NO CERTIFICADO. **Eficacia:** esta ratificación documental entra en vigor únicamente tras la CI PREMERGE/POSTMERGE de su propio PR, la fusión exclusiva a `dev` y un comentario institucional permanente que coteje el SHA final y las cuatro puertas anteriores; mientras falte algo, C04-10 permanece PENDIENTE. Las evidencias y afirmaciones históricas contenidas a continuación no certifican integración productiva.

## 1. Autoridad funcional, dependencia y trazabilidad

- Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, cuya huella SHA-256 canónica registrada en `docs/MASTER_REFERENCE.md` es `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`. No se ha modificado. Referencias: `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md` y `docs/ADR-015_CLOUDFLARE_ALMACENAMIENTO_MULTIMEDIA.md`.
- Puerta precedente: **C04-09 CERTIFICADA en alcance interno/sintético** por [acta permanente #6840753129](https://github.com/crecisol-platform/CRECISOL/pull/100#issuecomment-6840753129), 20/20 workflows aplicables; `dev` anterior `ba08137fc4ea1d39f21e63ce0cd9e3d309f585fb`.
- Alcance exigido para C04-10: **«Aislamiento de caché y trabajos»**, positivo **«Tests de keys, jobs, medios y exportaciones»**, negativo **«No contaminar caché cruzada»**. No adelantar C04-11 ni C04-12.
- Especificación de entrada histórica: [`C04_10_ESPECIFICACION.md`](C04_10_ESPECIFICACION.md). Acta de puertas históricas, deliberadamente condicional antes de las ejecuciones: [`C04_10_ACTA_CONDICIONADA.md`](C04_10_ACTA_CONDICIONADA.md). Esta ratificación no elimina ni reescribe esos estados pasados.

## 2. Alcance técnico integrado, archivos y barreras

PR técnico [#104](https://github.com/crecisol-platform/CRECISOL/pull/104): HEAD final `8b8763c85ff3bef6a47393e488bd6dfd993c50e4`, 8 archivos exclusivos de C04-10, squash a `dev` SHA **`8e63c8974d57dff083a61df2c07e5c9965bb1eae`**.

- `backend/src/Crecisol.Domain/Tenancy/TrustedTenantArtifactScope.cs`: propósito de artefacto `Cache/Job/Media/Export`, entorno y variante canónica aprobada por el servidor; aislamiento por tenant, branch, actor, recurso, acción, época/revisiones, modo Shared/Dedicated y slot; claves opacas HMAC-SHA256 con material de alta entropía suministrado solo por un futuro puerto protegido; ninguna clave o GUID equivale a permiso.
- `backend/src/Crecisol.Application/Tenancy/ApplyScopedTenantArtifact.cs`: puertos sin adaptador productivo, operación ligada al scope verificado, lectura de decisión antes de operar, al revalidar y después; recibo exacto y cercado por revisión. La autorización transaccional física debe revalidarse en un futuro gateway antes de cada efecto real; esta clase NO puede certificar su comportamiento futuro.
- `backend/tests/Crecisol.TenantArtifacts.ContractsTests/`: .NET 10, pruebas positivas y negativas de identidad, propósito, variante de caché, expiración/revocación, actor, tenant, branch, recurso, revisiones, placement, firmas/recibos y reintentos.
- `database/tests/c04_10_isolation_fixture.sql` y `c04_10_isolation_smoke.sh`: MySQL 8.4/InnoDB en contenedor **desechable y sin red**, FK compuestas tenant/branch/resource, procedimientos de prueba por identidad de cuenta SQL sintética, prohibición de tablas directas para los roles de ensayo; 12 claims reales con un ganador y once `BUSY`; invalidación por época de acceso y revisiones; medios y exports solo referencias/metadata opacas.
- `qa/c04_10_artifact_audits.py` y `.github/workflows/c04-10-isolation.yml`: auditorías A/B/C, compilación Release `-warnaserror`, regresiones del capítulo y verificación de evidencias en el mismo SHA/run, sin despliegue. Tres workflows condicionales `skipped` solo corresponden a PREMERGE técnico y **no** se contabilizan como éxitos.

**Impacto de arquitectura:** `frontend/` sin cambios; `backend/` solo contratos y pruebas; `database/` solo fixtures temporales, **sin DDL del producto**; `qa/` y `.github/workflows/` control de CI. No se habilitaron rutas HTTP privadas, adaptadores de DI, trabajadores productivos, Redis, Cloudflare R2/Stream, uploads, URL firmadas, exportadores ni acceso a archivos de clientes. La cuenta SQL del fixture no equivale a JWT/MFA ni a autorización productiva.

## 3. Seis ámbitos de comprobación: A/B/C y resultados observados

**A — trazabilidad y funcionalidad:** comprobación de la matriz oficial; claves y cuatro propósitos diferenciados; representación de resumen y detalle no comparten entrada aun para el mismo tenant/recurso; fuente maestra intacta.

**B — seguridad y tenancy:** rechazo de permisos y estados caducados, pertenencia y sesión revocadas, Tenant A y B con el mismo resource_id, sucursales/recurso/actor cruzados, cache poisoning, claves y recibos adulterados, revision/epoch/placement obsoletos, replay a otro tenant, accesos directos de cuentas SQL sintéticas y doble reclamación de trabajo. **Seis mutaciones estáticas de guardas verificadas**; no equivalen a prueba ofensiva de servicios de producción.

**C — arquitectura, regresiones y evidencia:** Domain puro, Application por puertos; MySQL real 8.4 sin red, SQL con FK compuestas, cinco procedimientos aislados, Release .NET 10, regresión integral C03-10 con Angular 22/Chromium/MySQL, verificación del Documento Maestro y controles Foundation/C04 previos.

**Resultado concreto observado en los cuatro runs específicos A/B/C de C04-10:** **149 aserciones positivas y 139 negativas .NET 10**, **27 positivas y 43 negativas MySQL 8.4**, 12 workers SQL sintéticos concurrentes para la misma operación (**1 `CLAIMED`, 11 `BUSY`**, sin doble ejecución). Total de pruebas específicas mostradas: **358 comprobaciones** (288 .NET + 70 MySQL), independientemente de las regresiones del resto del sistema.

## 4. Cuatro puertas de la microfase — evidencia directa, 20/20 SUCCESS

### 4.1. Técnica PREMERGE — PR #104, SHA `8b8763c85ff3bef6a47393e488bd6dfd993c50e4`

**5/5 workflows aplicables `completed/success`, 0 fallos vigentes, 0 pendientes:** [C04-10 A/B/C + .NET/MySQL — 43345769770](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345769770); [C03-10 integral — 43345769841](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345769841); [C04-02 shared MySQL — 43345769864](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345769864); [C04-01 catálogo — 43345769731](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345769731); [Foundation — 43345769732](https://github.com/crecisol-platform/CRECISOL/actions/runs/43345769732). **Tres condicionales `skipped` no son PASS y quedan excluidos.**

### 4.2. Técnica POSTMERGE — SHA `8e63c8974d57dff083a61df2c07e5c9965bb1eae`

**7/7 `completed/success`, 0 fallos, 0 pendientes:** [C04-10 — 43346310053](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346310053); [C03-10 integral — 43346310075](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346310075); [C04-02 — 43346309967](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346309967); [C04-01 — 43346310079](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346310079); [C03-02 — 43346309987](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346309987); [C03-03 — 43346310049](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346310049); [Foundation — 43346310123](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346310123).

### 4.3. Documental PREMERGE — PR #103, HEAD `caaf757aa4a39acb97c1a5fd8af4d89095e31740`

**4/4 `completed/success`:** [C04-10 — 43346391099](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346391099); [C04-02 — 43346391100](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346391100); [C03 institucional — 43346391106](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346391106); [Foundation — 43346391179](https://github.com/crecisol-platform/CRECISOL/actions/runs/43346391179). PR documental [#103](https://github.com/crecisol-platform/CRECISOL/pull/103) fusionado exclusivamente en `dev`, squash `45ad14991d045b9a7470237f4401e0e1e4ce0856`; afecta solo tres archivos documentales.

### 4.4. Documental POSTMERGE — SHA `45ad14991d045b9a7470237f4401e0e1e4ce0856`

**4/4 `completed/success`:** [C04-10 — 43347050986](https://github.com/crecisol-platform/CRECISOL/actions/runs/43347050986); [C04-02 — 43347050853](https://github.com/crecisol-platform/CRECISOL/actions/runs/43347050853); [C03 institucional — 43347051012](https://github.com/crecisol-platform/CRECISOL/actions/runs/43347051012); [Foundation — 43347050880](https://github.com/crecisol-platform/CRECISOL/actions/runs/43347050880).

**Acumulado de puertas originales C04-10:** **5 + 7 + 4 + 4 = 20/20 SUCCESS**. Las ejecuciones con fallos de SHAs anteriores se conservan sin alterar; su corrección se probó en nuevos SHAs y nuevas ejecuciones. No asignar a un `skipped` el valor de aprobado.

## 5. Riesgos no habilitados, reversibilidad y condiciones formales finales

**Solo certificable como contrato interno y prueba sintética:** un scope restaurado o una clave HMAC no son autorización de usuarios; el fixture de cuentas SQL no reemplaza el identity provider. El backend aún debe incorporar autorización verdadera por identidad/membresía/rol/sucursal/propiedad; aprobación excepcional SuperAdmin con MFA, razón y caducidad; invalidación real de CDN, cachés y objetos; ejecución física atómica con fencing robusto; envío seguro de archivos/medios y descargas con firmas temporales; exportación sin filtración; gestión de fallos y observabilidad; y pruebas ofensivas de C04-11. **Todo eso está fuera de alcance y sigue BLOQUEADO.** Ningún hallazgo P0/P1 permanece abierto en el **alcance sintético probado**; esto NO es afirmación de riesgo cero global.

**Prohibiciones:** `main`, Word/PDF del Documento Maestro, producción, VPS, DNS, Cloudflare, cobros y `solqaryn/Solqaryn` permanecen sin modificación. **No se ejecutó despliegue**. Reversibilidad: revertir los squashes técnicos/documentales exactos `8e63c8974d57dff083a61df2c07e5c9965bb1eae` y `45ad14991d045b9a7470237f4401e0e1e4ce0856` según evaluación de dependencias; el fixture temporal se destruye y no hay migración del producto.

**Efectividad institucional de esta ratificación:** falta completar su propio ciclo de PR documental (auditorías/CI PREMERGE, fusión exclusiva a `dev`, CI POSTMERGE) y publicar el comentario permanente con el SHA resultante y URLs reales. **Solo cuando sucedan TODOS:** dictamen **C04-10 CERTIFICADA, exclusivamente contratos y ensayos sintéticos; 10/12 microfases de C04**; C04-11 **HABILITADA SOLO PARA PREPARACIÓN**; C04 completo **NO CERTIFICADO**; capítulos completos **3/37**. Si alguno falla o sigue pendiente: **C04-10 NO CERTIFICADA; C04-11 BLOQUEADA**.
