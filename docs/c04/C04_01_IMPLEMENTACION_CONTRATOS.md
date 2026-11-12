# CRECISOL — C04-01 · Contratos internos implementados del catálogo confiable de tenants

**Fecha:** 12 de noviembre de 2026 (Honduras).  
**Repositorio:** `crecisol-platform/CRECISOL`, ID `1450414592`; base `dev` `1c13591c43ca85cc3bc55ba0753be3be25a5f60c`.  
**Rama:** `feature/c04-01-contratos-catalogo-tenants-20261112`; **PR #83**.  
**Estado:** CÓDIGO DE CONTRATOS DOMAIN/APPLICATION EN REVISIÓN. **NO HAY CATÁLOGO OPERATIVO, PROVISIONING, AUTH REAL, API NI PERSISTENCIA TENANT**. La certificación de C04-01 aún depende de pruebas y auditorías sobre el SHA integrado; no se declara por este documento.

**Autoridad:** el único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Texto íntegro de C04: SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, 72 párrafos indexados `1735..1806`. El inventario se reconstruye con `qa/c04_01_normative_inventory.py inventory`; no sustituye la fuente binaria ni cambia el maestro.

## 1. Trazabilidad del capítulo 04 (texto original, NO plan inventado)

| Índices del Word C04 | Contrato o limitación de C04-01 | Futuro capítulo/microfase |
| --- | --- | --- |
| 1735–1740 | Identidad CRECISOL única, `TenantId` opaco y `TenantCatalogSnapshot` sin permisos ni host, un mismo usuario puede tener múltiples pertenencias | C04-05/06, C05/06 identidad y auth real |
| 1741–1752 y 1787–1793 | `TenantAddressMode` enum `PathSlug / Subdomain / CustomDomain` sin aceptar URL, DNS ni host como pertenencia | C04-04/07 verificación DNS y reserva |
| 1753–1755 | Estados `Draft / PendingVerification / Active / Suspended / Deactivated`; snapshot de solo lectura y sin método de alta o activación | C04-07 provisión atómica con owner/plan/contrato/defaults |
| 1756–1758 | No selector de empresa en login, `tenant_id` nunca es autorización; lectura interna requiere un puerto de permiso propio | C04-05/06, C05/06 |
| 1759–1761, 1771–1772 y 1781–1782 | Identificador tipado, port de lectura por ID, rechazo de lectura que devuelve otro tenant | C04-02 FK compuestas, índices y MySQL 8.4 |
| 1762–1767 | No emitir eventos, cachés, adjuntos, archivos ni exports en C04-01 | C04-10/11 y C21/C32 |
| 1768–1770 | `Suspended` solo como metadato; sin transición ni borrar registros | C04-08 suspensión / offboarding |
| 1773–1785 | QA positiva/negativa real .NET10 de Domain/Application; auditoría C de alcance, sin confundir con cumplimiento de todo C04 | C04-02..12 y C04 integral |
| 1794–1803 | Las ocho puertas normativas internas del Word C04 son `extraer/diseñar/codificar/probar/A/B/C/acta`. **NO equivalen** a 12 microfases técnicas C04-01..12 del plan prospectivo | C04-12 certifica capítulo completo |
| 1797–1805 | SHARED y DEDICATED, TEN-001/002/008/009/010/021/032, seguimiento universal y segmentación tenant/sucursal quedan sin implementar y con prueba pendiente | C04-02/03/06/09/10/11/12; G.7 |

**Corte estricto:** C04-01 implementa solamente **contratos internos de identidad, nombre, estado, dirección de tipo y lectura autorizada** sin infraestructura, y no interpreta el catálogo como un proveedor real de permisos, datos o dominios. Para la definición comercial de slug/modalidad, paquete, contrato, plazo y cobro se reservan requisitos COM-001/009/010 y ADR-014 sin mezclar precio con tenancy. La referencia a SOLQARYN es organizativa, sin integración de código ni identidades.

## 2. Entregables de código separados por capa

- `backend/src/Crecisol.Domain/Tenancy/TenantId.cs`: valor opaco inmutable con `Guid.Empty` rechazado. Conocer el ID **NO autoriza**.
- `backend/src/Crecisol.Domain/Tenancy/TenantCatalogSnapshot.cs`: snapshot inmutable rehidratable **solo desde catálogo de backend confiable**; nombre administrativo vs display, `Revision`, UTC, estados y modalidades tipados, sin host, DSN, ubicación física ni credenciales. Valida enums, longitudes, caracteres de control y Unicode de formato; `ToString` no imprime nombres ni IDs.
- `backend/src/Crecisol.Application/Tenancy/ReadInternalTenantCatalog.cs`: puerto `IPlatformCatalogReadAuthorization` provisto por futuro backend real, `ITrustedTenantCatalogReader` de lectura por ID y caso de uso `ReadInternalTenantCatalog`. **Autoriza antes de leer**, corta cancelación, rechaza que el adapter devuelva otro tenant y proyecta `InternalTenantCatalogView` de cinco campos permitidos. No acepta `Host` ni `tenant_id` de HTTP como permiso.
- `backend/tests/Crecisol.TenantCatalog.ContractsTests/`: suite positiva/negativa .NET 10 sin NuGet externos, con denegaciones reales del caso de uso, port falso malicioso y reflexión de propiedades/operaciones.
- `qa/c04_01_normative_inventory.py`, `qa/c04_01_contracts_audits.py`, workflow de lectura mínima: verifican fuente, traza, mutaciones adversariales y compilaciones.

**Superficie intencionadamente inexistente:** no se registra el caso de uso en la composición DI de `Crecisol.Api`, no se añaden endpoints de lectura pública/administración, no hay `ITenantCatalogRepository` productivo, aprovisionamiento/escritura, migraciones, datos persistidos ni llamadas a DNS, Cloudflare o bases dedicadas. **El puerto de permiso no tiene implementación productiva** hasta identidad/autorización segura, así que ningún request puede obtener lectura de tenant. No es seguridad end-to-end implementada.

## 3. Riesgos y pruebas positivas/negativas

- **P0 autorización:** identificador/Host/cookie no concede permiso; lector no toca repositorio si el port deniega o falla; respuesta solo tras permiso y después se valida igualdad de TenantId. El adapter real y membresías son futuros; no se presume seguridad de una ruta privada inexistente.
- **P0 exposición:** DTO de cinco campos sin `InternalName`, DSN, ubicación de DB, permisos, secretos o datos personales. Serialización y reflexión se prueban. Los registros de diagnóstico redactan IDs y nombres.
- **P0 límites cruzados:** lector malicioso que devuelve Tenant B para el ID de A causa excepción; no hay fallback a default tenant. No es una prueba de MySQL/caché/archivos multiempresa porque todavía no existe persistencia C04.
- **P1 validación:** estados/descripciones desconocidos, nombre con marcado/invisible/bidi/control, fecha no UTC, revisión inválida o ID vacío fallan. Normalización Unicode NFC para nombre de visualización.
- **Disponibilidad:** cancelación antes de auth, durante auth, durante read; excepción de autorizer y storage se propagan sin autorización implícita. No se cambian estados en este capítulo.
- **Regresión:** compilar en Release `Crecisol.Api`, Domain y Application; ejecutar contratos C01/C03 previos; inspección de dependencias, ausencia de rutas privadas, tests con SHA/run exactos.

## 4. Tres auditorías y condiciones de cierre

**Auditoría A — funcional/trazabilidad:** capítulo C04 Word/PDF con hashes inalterados, tabla completa, 12 microfases técnicas diferenciadas de ocho puertas MAE-C04, archivos y matrices de pruebas.  
**Auditoría B — seguridad/tenancy:** negativas de Domain/Application realmente ejecutadas, mutaciones documentales/código, no host/slug como permiso, sin DSN ni token en DTO, ausencia de API privada.  
**Auditoría C — arquitectura/calidad:** build real .NET 10, tests positivos/negativos y regresiones, identidad de run, proyecto sin nuevas dependencias, sin cambios frontend/database/Infrastructure/API por C04-01.

**Gates formales:** comprobar PREMERGE del HEAD real de PR #83 y POSTMERGE del SHA exacto integrado en `dev`, registrar salida de A/B/C, incidentes, SHA y acta de alcance. Los `skipped` condicionales no se cuentan como `SUCCESS`. Si faltan pruebas esenciales, P0/P1 o se encuentran fugas, el cierre queda BLOQUEADO y C04-02 no se habilita.

**Rollback:** revertir únicamente PR #83 en `dev`, con QA y contrato nuevo. No reescribir C03, alterar maestros, `main`, SOLQARYN ni servicios productivos.

**Dictamen antes de CI:** código interno desarrollado, **sin catálogo productivo**, **microfase aún NO CERTIFICADA** y C04 NO CERTIFICADO. Solo avanzar si toda la evidencia coincide.
