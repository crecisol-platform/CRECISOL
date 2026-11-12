# CRECISOL — C04-01 · Especificación de preparación del catálogo confiable de tenants

**Estado documental:** PREPARACIÓN TÉCNICA, NO IMPLEMENTADA, NO CERTIFICADA.  
**Capítulo:** C04 — multitenancy, dominios y aprovisionamiento; única microfase abierta: C04-01.  
**Repositorio:** `crecisol-platform/CRECISOL`, ID `1450414592`; integración `dev`.  
**Punto de partida:** C03 institucional certificado (10/10) mediante [PR #81](https://github.com/crecisol-platform/CRECISOL/pull/81) y [cierre verificable](https://github.com/crecisol-platform/CRECISOL/pull/81#issuecomment-6812682477), `dev` SHA `30c976e1db9add6ea00b07549c34f3c50d98dc31`, 6/6 CI PREMERGE aplicables y 17/17 CI POSTMERGE SUCCESS.  
**Autoridad funcional:** único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` y PDF de lectura con hashes de `docs/MASTER_REFERENCE.md`. [Plan técnico preliminar de 12 microfases](../planificacion-futura/C04_MICROFASES_PROPUESTAS.md). Esta especificación no modifica el Maestro ni convierte la propuesta de 12 filas en una norma nueva.

## 0. Comprobación de fuente canónica — extracción ejecutada

El runner real del PR #82 extrajo del único Word canónico el **capítulo 04** entre los párrafos técnicos `1735..1807`: `6 680` caracteres y SHA-256 del texto normalizado `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. Es evidencia de lectura del Word, **no** certificación del capítulo.

**Huellas integrales verificadas del Documento Maestro Único:**
- Word SHA-256: `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`.
- PDF SHA-256: `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`.
- Los blobs Git y la autoridad funcional se mantienen inalterados, según `docs/MASTER_REFERENCE.md`.

**Trazabilidad directamente observada en el capítulo 04 del Word:** modalidad A (ruta bajo el dominio SaaS), modalidad B (subdominio de plataforma) y modalidad C (dominio propio del cliente); slug único con redirección canónica y reserva de nombres sensibles; creación conjunta de tenant, dueño, plan, contrato, dominio provisional y defaults de forma atómica o saga compensable; prohibición de publicar altas parciales; retry idempotente; login sin código de empresa; prohibición de confiar en `tenant_id` del navegador; filtrado tenant/sucursal en backend. La **implementación atómica** corresponde a C04-07, mientras que C04-01 sólo prepara el catálogo y su contrato seguro.

| Requisito de fuente C04 | Relación acotada con C04-01 | Dependencia posterior |
| --- | --- | --- |
| Modalidades ruta, subdominio, dominio propio | `AddressClaim` documenta tipo y reserva; **sin activar DNS** | C04-04/07 |
| Slug único / reserva de palabras sensibles | Identidad canónica y concurrencia planeadas; sin crear por `Host` | C04-04/07 |
| Alta tenant + propietario + plan + contrato + defaults | Estados de `TenantRecord` y referencias comerciales separadas | C04-07 y C08 |
| No publicar altas parciales / idempotencia | `LifecycleState` no equivale a acceso; transición segura prevista | C04-07/08 |
| Login sin código empresa, `tenant_id` no confiable | Catálogo nunca otorga membresía o autorización | C04-05/06 |
| Filtrado datos por tenant y sucursal | Identificadores opacos y denegación por defecto | C04-02/05/06/11 |

**Límite de cotejo:** el auditor automatizado validó ubicación y contenido base del capítulo y el pin criptográfico; los demás requisitos normativos de la tabla de capítulo continúan sujetos a revisión línea por línea al autorizar la implementación, sin convertir este extracto en una sustitución del Word.

## 1. Objetivo, límite y responsabilidad

**Objetivo único C04-01:** definir contratos, invariantes y pruebas para un catálogo **confiable y administrado desde backend** que distinga la identidad del tenant, su nombre, estado y referencias comerciales/de dominio **sin exponer detalles de infraestructura**. Ningún tenant debe nacer por recibir un `Host` arbitrario. Todas las altas, reservas, transiciones y asignaciones de ámbito deben pasar por autoridad administrativa explícita en etapas autorizadas.

**Esta entrega es de preparación exclusivamente:** no crea tablas, datos, endpoints de administración, autenticación privada, conectores de bases dedicadas, workers, rutas de producción, inquilinos activos, DNS ni recursos Cloudflare. C04-02 a C04-12 permanecen bloqueadas. Ningún contenido de esta especificación acredita pruebas de aislamiento real ya ejecutadas.

**Dependencias obligatorias:** catálogo de fuente canónica, mecanismo de identidad/membresía autorizado y verificación de host (C04-04/C04-05), persistencia shared tenant-aware (C04-02), puertos dedicated (C04-03), aprovisionamiento idempotente (C04-07), suspensión (C04-08) y pruebas ofensivas (C04-11). C04-01 no anticipa esas implementaciones.

## 2. Modelo de dominio propuesto (contratos, NO DDL)

| Entidad / concepto | Campos y semántica propuesta | Restricción innegociable |
| --- | --- | --- |
| `TenantRecord` | `TenantId` opaco e inmutable, `LegalOrInternalName` administrativo, `DisplayName` administrable, `LifecycleState`, `Revision`, fechas UTC y causa de cada transición | El `TenantId` no es prueba de identidad ni autorización |
| `TenantLifecycleState` | `Draft`, `PendingVerification`, `Active`, `Suspended`, `Deactivated`; ciclo versionado | Estado no habilita acceso sin identidad, pertenencia y scopes; nunca reactivar por input público |
| `AddressClaim` | Referencia al tenant y modalidad `PathSlug`, `Subdomain` o `CustomDomain`; nombre normalizado, estado de verificación y revision | El nombre reservado o recibido por HTTP **no** certifica propiedad del dominio |
| `TenantPublicDescriptor` | Identidad pública, nombre e información segura de marca, sólo cuando el ámbito está habilitado para exposición | **Nunca** devolver DSN, secretos, ubicación física de datos, cadena de conexión, IDs de base ni grants privados |
| `TenantCatalogRevision` | `Revision`, actor administrativo autenticado, motivo, timestamp, cambios antes/después, trazabilidad y reversión controlada | Cambios auditados y escritura idempotente/optimista; no confiar en timestamps del navegador |
| `DataPlacementReference` | Identificador interno opaco al catálogo protegido; el adaptador backend resolverá ubicación en C04-03/09 | Prohibido resolver DSN desde `Host`, `X-Forwarded-Host`, `tenant_id`, slug, JWT sin validación o cookie |

**Decisión conservadora:** una entidad `TenantRecord` no representa una sesión ni una membresía. El estado del catálogo es **metadato administrativo**, no permiso. El almacén protegido de conexiones/datos dedicados y el router físico son contratos distintos que requerirán ADR-002/004 y pruebas, no campos serializables del DTO público.

## 3. Flujo de confianza objetivo (no implementado)

1. **Límite de red:** API recibe `Host` y la cadena de proxies; nunca confía en `Forwarded` / `X-Forwarded-Host` sin lista explícita de proxies confiables, por diseño de C04-04.
2. **Registro administrativo:** una identidad de plataforma habilitada crea o propone tenant mediante operación autenticada y autorizada; **Ningún request público crea tenants**.
3. **Catálogo:** resuelve sólo nombres reservados y verificados contra registro backend protegido, con unicidad normalizada y versionada; nombres desconocidos o en conflicto -> DENY por defecto.
4. **Contexto del usuario:** antes de abrir recursos privados, identidad confirmada + membresía tenant + permiso de acción + sucursal/propiedad; un host válido por sí solo **no autoriza** acceso, conforme a C04-05/06.
5. **Persistencia:** shared/dedicated se elige exclusivamente desde referencia de colocación protegida del backend; nunca por entrada del cliente (C04-02/03/09).
6. **Suspensión/alta:** cualquier cambio de estado ocurre por operación administrativa transaccional y auditada; suspender no borra datos ni habilita sesiones existentes (C04-08).
7. **Lectura pública:** se sirve sólo proyección mínima declarada pública, sin enumeración de tenants ni metadatos de infraestructura; respuestas de error homogéneas para evitar oracle de existencia.

**Configuración administrable, no hardcode:** nombre mostrado, marca pública, modalidad comercial de dirección, dominios propuestos y políticas de reserva se gestionarán desde consola autorizada con `Revision` y auditoría. El precio/paquete, duración del contrato y periodicidad de cobro son conceptos separados (COM-001/009/010); **nunca** se deducen del slug ni se activan cobros aquí. La validez legal/comercial queda pendiente de su capítulo y ADR-014.

## 4. Normalización y garantías de nombres (decisiones sujetas a pruebas)

- Nombres de dominio: canonicalización IDNA/punycode, minúsculas, sin punto final inesperado y validación de sintaxis y longitud; invalidar unicode confusable y entradas sin autoridad. La normalización no demuestra posesión DNS.
- Slugs de ruta: sintaxis explícita, tamaño máximo parametrizable con límites seguros, lista de palabras reservadas y unicidad normalizada. Colisiones devuelven error estable sin enumerar datos de otro tenant.
- Reservas: estados `Proposed -> Reserved -> Verified -> Released` **solo documentales**; ninguna reserva se activa por `Host` en tráfico no confiable.
- El catálogo debe ser consistente bajo concurrencia; claves únicas compuestas y transacción/compare-and-swap deberán probarse en fases de persistencia/aprovisionamiento, no simularse como implementadas ahora.
- Desactivación: no reciclar identificadores, no permitir referencias a tenant anterior ni reutilizar dominios sin proceso de liberación verificado.
- Caches, bitácoras, eventos, archivos Cloudflare y exportaciones deberán aislarse por TenantId **más autorizaciones reales** cuando existan; C04-10/11 lo demostrarán.

## 5. Matriz de amenazas y pruebas negativas por ID

| Prueba | Ataque/condición | Resultado requerido | Responsable de ejecución |
| --- | --- | --- | --- |
| T01 | `Host` desconocido, vacío o falsificado | No crear ni activar `TenantRecord`; DENY | C04-01 diseño + C04-04 runtime |
| T02 | `X-Forwarded-Host` / `Forwarded` de proxy no confiable | Ignorar cabecera, DENY | C04-04 |
| T03 | `tenant_id` en query/cookie/header/JWT no verificado | No otorgar permisos ni seleccionar conexión | C04-05/11 |
| T04 | Tenant A intenta leer/suspender tenant B | Denegación backend, sin fuga de metadatos | C04-05/11 |
| T05 | Cliente pasa `dsn` / `database` / `connectionString` | Rechazo/ignorancia segura; sin cambio de colocación | C04-03/11 |
| T06 | Slug/host duplicado y carreras concurrentes | Reserva única/idempotente, sin tenant duplicado | C04-07/11 |
| T07 | Homógrafos, unicode alterado, espacios, mayúsculas y punycode | Canonicalización y validación deterministas | C04-01 contratos + C04-04 |
| T08 | Tenant `Suspended` con token previo | Denegar recursos privados, revocar contexto/caché | C04-08/11 |
| T09 | Proyección pública incluye DSN, secreto o IDs físicos de DB | Falla de contrato / no serializar | C04-01 contratos |
| T10 | Actualización concurrente con `Revision` obsoleta | Conflicto, no sobrescritura silenciosa | C04-01 contrato + C04-07 |
| T11 | Enlace Cloudflare/preview de tenant equivocado | No emitir URL; aislamiento de permisos | C04-10/11; C21/C32 |
| T12 | Catálogo no disponible o estado ambiguo | Fail-closed, no fallback al tenant por defecto | C04-01 contrato + C04-11 |
| T13 | Host válido sin identidad/membresía | Sólo proyección pública permitida, denegar datos privados | C04-05/11 |
| T14 | Error de API enumera nombres de tenants reservados | Respuesta genérica, auditoría interna sin PII | C04-01 contrato + C04-11 |
| T15 | SuperAdmin sin MFA/step-up intenta lectura privada | Rechazo y registro excepcional | C05/C06; C04-11 |
| T16 | Migración shared/dedicated elige DB por parámetro | Denegación, router servidor y rollback | C04-03/09/11 |
| T17 | Cambio de paquete comercial altera automáticamente permisos | Separación de modalidades, contratos y privilegios | C08; COM-T06/07/09/17 |
| T18 | Borrado lógico rompe historial/legal hold | Retención controlada y revocable, sin desaparición de auditorías | C04-08; C23 |

**Ninguna de T01–T18 está marcada como prueba runtime ejecutada en C04-01.** Su definición y revisión adversarial documental sí son parte de esta preparación.

## 6. Arquitectura y superficie futura acotadas

| Capa | Ubicación prevista | Obligación |
| --- | --- | --- |
| Domain | `backend/src/Crecisol.Domain/Tenancy/` | Estados, nombres, invariantes puros sin infraestructura |
| Application | `backend/src/Crecisol.Application/Tenancy/` | Puertos de catálogo y casos de uso autorizados, denegación por defecto |
| Infrastructure | `backend/src/Crecisol.Infrastructure/Tenancy/` | Adapter backend protegido sin revelar connection strings |
| API | `backend/src/Crecisol.Api/Modules/Tenancy/` | Ningún endpoint privado hasta auth/membresías verificables |
| Database | `database/` | Modelo y migraciones MySQL 8.4 sólo tras aprobar su microfase C04-02 |
| Frontend | `frontend/` | No introducir selector en login ni administración sin backend autorizado |

**No se crearán** estas rutas de producto en el PR preparatorio. Las decisiones de EF Core y migraciones de `ADR-002`/dedicated `ADR-004` continúan abiertas, sin presuponer proveedor o DSN.

## 7. Matriz de aceptación / puerta de implementación de C04-01

- [ ] **Fuente normativa:** extraer y contrastar el texto íntegro del capítulo 04 del Word canónico y fijar matriz requisito `MAE-C04-xx -> C04-01`. El plan de doce microfases es solo propuesta. No aprobar código hasta cerrar divergencias.
- [ ] **Decisión de contratos:** ID, estados, revisión, privacidad de DTO, modalidades y normalización confirmados en acta; sin inventar precios ni reglas legales.
- [ ] **Pruebas runtime futuras:** builds reales .NET 10; casos Domain/Application positivos y negativos; adversariales T01/T07/T09/T10/T12; integración con MySQL 8.4 en etapas correspondientes; resultados de fallas guardados.
- [ ] **Seguridad P0:** revisar autorización backend, prohibición de Host como identidad, rechazo de DSN de cliente, ausencia de filtraciones de infraestructura y concurrencia de altas.
- [ ] **Auditorías A/B/C:** A trazabilidad, B aislamiento/ataques y PII, C arquitectura/compatibilidad/rendimiento/accesibilidad; pruebas que no puedan ejecutarse se marcan NO EJECUTADAS.
- [ ] **Estado de implementación:** C04-01 no pasa a IMPLEMENTADA ni CERTIFICADA sin su código autorizado, pruebas verificables, CI aplicable, acta de cierre y SHA exacto. C04-02 seguirá bloqueada hasta ese momento.

## 8. Evidencia, riesgos residuales y rollback de la preparación

**Riesgos abiertos:** P0 de confundir `Host` con autenticación; P0 de exponer DSN/tenant física; P0 de colisiones cross-tenant; ADR-002/004 abiertos; fuente normativa C04 no verificada textualmente aún; ausencia de implementación. **Estado de riesgos:** controles de diseño previstos, no mitigaciones ejecutadas en producto.

**Evidencias existentes:** CI del cierre C03 PR #81 y hashes del Maestro; esta especificación y `qa/c04_01_preparation_audits.py` son el único nuevo alcance. **No pruebas de seguridad del runtime C04**.

**Rollback:** no fusionar el PR preparatorio si fallan auditorías/QA; si está fusionado, revertir solo cambios documentales y tests/CI de preparación por SHA. Prohibido modificar el Word/PDF, `main`, SOLQARYN o producción.

**Siguiente paso permitido:** afinar esta especificación mediante lectura automatizada del Word, revisión de diferencias y aprobación de contrato. **No activar C04-02, C05 ni rutas privadas**.
