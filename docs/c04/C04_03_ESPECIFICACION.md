# CRECISOL — C04-03 · Especificación y preparación: contratos de almacenamiento dedicado

**Fecha:** 12-11-2026 (Honduras). **Repositorio único:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). **Base:** `dev` SHA `e2b0a921449f4a5a762a7af4c29f3c96a36b6b37`. **Rama:** `feature/c04-03-contratos-almacenamiento-dedicado-20261112`. **Estado de inicio:** PREPARACIÓN; C04-03 **NO CERTIFICADA**, C04-04 **BLOQUEADA**.

## 1. Autoridad y precedentes realmente verificados

Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Texto de capítulo C04: 72 párrafos, SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, verificador `qa/c04_01_normative_inventory.py`. Las ocho puertas normativas MAE-C04-01..08 no equivalen a las doce microfases técnicas.

**Puerta C04-02 acreditada:** [acta de PR #87](https://github.com/crecisol-platform/CRECISOL/pull/87#issuecomment-6831090024), SHA `e2b0a921449f4a5a762a7af4c29f3c96a36b6b37`, CI documental POSTMERGE **3/3 SUCCESS**. PR #86 previo integra modelo relacional SHARED y lector interno sin API, 7/7 CI posterior; C04-02 está certificada en su alcance **2/12**, no en autenticación ni routing productivo. Capítulos completos certificados: **3/37**. Los apartados históricos anteriores no se reinterpretan como novedades.

**Entrega autorizada C04-03 según plan:** `Contratos de almacenamiento dedicado`; prueba positiva `Puertos de ubicación tipados, decisión del servidor`; negativa `No DSN desde URL, cookie o cabecera`.

## 2. Alcance permitido y arquitectura propuesta

Crear contratos internos **Domain + Application** sin conexiones, DI, API ni base de datos: el backend será el ÚNICO origen de la decisión SHARED/DEDICATED. Este corte **no implementa** un router en ejecución ni movimiento de tenants.

| Capa | Contrato | Regla |
| --- | --- | --- |
| Domain/Tenancy | `TenantPlacementMode` con solo `Shared` y `Dedicated`, `DedicatedStorageSlotId` opaco, `TenantStoragePlacementSnapshot` inmutable | Restaurado desde catálogo protegido, valida tenant, modo, slot, revisión, UTC; no acepta DSN, servidor, host ni database name |
| Application/Tenancy | `ICurrentTenantPlacementAuthorization` y `ITrustedTenantPlacementReader` | La decisión de identidad/membresía se inyectará SOLO desde backend futuro; no acepta argumentos de request |
| Application/Tenancy | `ResolveCurrentTenantStorage` | Autoriza antes de leer, falla cerrado sin autorización/catálogo, rechaza que lector devuelva datos de otro tenant, respeta cancelación y no produce DSN |
| Tests | Pruebas `.NET 10` reales con dobles de autorización y catálogo | Positivos SHARED/DEDICATED, negativas de tenant cruzado, modo/slot/estado inválidos, fallos/cancelación y reflexión de puertos sin input externo |
| QA | Tres auditorías A/B/C en CI con evidencia SHA/run | A Word + planificación, B mutaciones de límites de confianza, C compilación Release y ejecución .NET 10 sobre mismo SHA/run |

**Dependencias:** Domain puro; Application depende de Domain, Infrastructure/Api quedan intactos. La publicación HTTP de un enrutador, el catálogo real de colocación, el registro de pools/secretos y la selección de DSN serán responsabilidad posterior, condicionada a C04-04/05/06/07/09. NO almacenar cadenas de conexión, hostnames, URLs ni secretos en snapshots o DTO.

## 3. Riesgos, positivos y negativos obligatorios

- **P0 spoofing de contexto:** navegador modifica host, slug, URL, header, cookie, claim, formulario o token para seleccionar otro tenant o DSN. La API no recibe estos datos para decidir storage; el caso de uso acepta **solo** CancellationToken. **No conectarlo a API/DI antes de identidad/membresía real C04-05/06**.
- **P0 cross-tenant/catalogo:** un lector malicioso devuelve `Tenant B` para la identidad de A; rechazar excepción, no usar slot B ni fallback a shared por defecto. Falta de resultado = error cerrado, no cambiar silenciosamente el tipo.
- **P0 estado contradictorio:** SHARED con slot dedicado, DEDICATED sin slot, modo desconocido, GUID cero, revisión no válida, timestamp no UTC: rechazo.
- **P0 fuga de secretos:** solo ID opaco de slot de storage. No DSN ni proveedor, connection string, contraseña, host ni base de datos. Prohibido `ToString` que exponga IDs internos.
- **P1 carreras/versión:** snapshot con revisión inmutable, conservando la revisión del catálogo para futuras políticas de invalidación; revocación/failover/TTL/side-effects todavía NO probados.
- **P1 catálogo pendiente:** un slot dedicado compartido accidentalmente por tenants distintos requiere control de unicidad, ownership y grants en futura infraestructura; NUNCA usar GUID slot por sí solo como autorización ni establecer conexión por valor del cliente.
- **P1 rendimiento/UX:** no hay rutas ni I/O real en este corte; p95 de routing, aislamiento físico de bases, migrations/rollback y load testing no se pueden certificar anticipadamente.

**Matriz de pruebas propuesta:** positivos Shared/Dedicated con tenant A; dos tenants separados; revisiones distintas; denegación antes de leer; lector B para A; sin placement; estado inválido; slot nulo/cero o presente en Shared; timestamp no UTC; errores de autorización/storage; cancelación antes/durante/después; reflexión de firma pública sin input de request; sin propiedades string que permitan DSN; módulos API/Infrastructure/database intactos.

## 4. Condiciones estrictas de certificación y rollback

1. Verificar código, documentos, SHA e identidad de repo/rama/archivos antes de escribir. Especificación inicialmente no certifica implementación.
2. Ejecutar pruebas .NET10 positivas/negativas REALES y auditorías **A funcional, B seguridad con mutaciones, C arquitectura**. No contar unit tests con fakes como pruebas de autenticación real.
3. CI PREMERGE sobre HEAD exacto, revisar todos los workflows aplicables y excepciones `skipped`; 0 FAIL/ERROR/P0/P1 **dentro del contrato no expuesto**.
4. Solo después, fusionar en `dev` con SHA esperado, comprobar CI POSTMERGE sobre SHA de integración; registrar run IDs y reparar mediante nuevos commits cualquier fallo.
5. Emitir acta de alcance exacto en PR documental propio, con CI PREMERGE/POSTMERGE, jamás llamar al documento prueba por sí mismo. **C04-04 permanece BLOQUEADA** mientras no haya certificado C04-03.

**Rollback:** revertir solo los commits de C04-03 si aparecen fallos; nunca migrar una DB ni alterar producción. No tocar `main`, Documento Maestro Word/PDF, frontend, Cloudflare, DNS, SOLQARYN, VPS, pagos, suscripciones o permisos de GitHub.

## 5. Dictamen de preparación

**C04-03 PREPARADA PARA IMPLEMENTAR CONTRATOS INTERNOS**, no certificada ni operativa. El modelo de ubicación no equivale a autorización por actor, routing real ni base dedicada conectada. La implementación será bloqueada si se introduce DSN desde URL/headers, permisos implícitos, conexión productiva o se omite prueba/auditoría.
