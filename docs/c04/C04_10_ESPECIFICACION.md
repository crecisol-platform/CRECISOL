# CRECISOL — C04-10 · Preparación de aislamiento de caché, trabajos, medios y exportaciones

**Fecha:** 12-11-2026 (America/Tegucigalpa). **Estado:** PREPARACIÓN / NO CERTIFICADA. **Repositorío:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base autorizada:** `dev` SHA `ba08137fc4ea1d39f21e63ce0cd9e3d309f585fb`. **Ramas:** `feature/c04-10-preparacion-20261112` (documentación), `feature/c04-10-artifact-isolation-20261112` (técnica).

## 1. Autoridad y puerta de entrada

- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, con huella SHA-256 registrada `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; `docs/MASTER_REFERENCE.md`; `docs/PLAN_37_CAPITULOS.md`; `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`; `docs/SEGURIDAD_MULTITENANT.md`; `docs/ADR-015_CLOUDFLARE_ALMACENAMIENTO_MULTIMEDIA.md`.
- C04-09 **certificada exclusivamente en contratos y pruebas sintéticas**, PR técnico #101 y documental #100, comentario [institucional #6840753129](https://github.com/crecisol-platform/CRECISOL/pull/100#issuecomment-6840753129); 20/20 workflows aplicables aprobados, último SHA de `dev` verificado.
- Entregable oficial C04-10: **“Aislamiento de caché y trabajos”**. Positivo: **“Tests de keys, jobs, medios y exportaciones”**. Negativo bloqueante: **“No contaminar caché cruzada”**. C04-11 (ofensiva) y C04-12 (capítulo) permanecen bloqueadas durante C04-10.

## 2. Amenazas y alcance estricto

**P0**: tenant B recibe una clave, entrada de caché, trabajo, blob, vista previa o exportación de A por una colisión de claves; claves compartidas sin tenant/branch/recurso/actor/epoch; serialización de un contexto de tenant considerada permiso; trabajos encolados que sobreviven a suspensión, cambio de propietario o revocación; lectura por nombre de archivo/URL/host o DSN elegidos por cliente; acceso privado de SuperAdmin sin concesión excepcional; ejecución dos veces por carreras; restaura un respaldo para tenant ajeno.

**P1**: un replay erróneo tras caída, keys demasiado largas o con datos privados, logging de IDs/secretos, cache invalidation sin revisión de acceso, media y exportaciones guardadas como BLOB en MySQL, job payload con credenciales, permisos del usuario DB excesivos, una cuenta de workers capaz de llamar DDL o leer filas globalmente.

**Alcance que se permite IMPLEMENTAR:**
1. Contratos internos puros `backend/src/Crecisol.Domain/Tenancy/` para un scope autorizado POR EL SERVIDOR, con `TenantId`, sucursal, recurso, actor, acción y revisiones de tenant/identidad/branch/recurso/placement y variante canónica de representación construida por el servidor, estado activo, expiración UTC, modo SHARED/DEDICATED y slot tipado cuando corresponda.
2. Derivación determinista de claves **opacas HMAC-SHA-256** con separación de entorno y propósito; ningún GUID, URL ni texto privado visible en las claves. Una clave de caché NO es autorización. Debe incluir una variante canónica de representación generada por el backend (p.ej. detalle y resumen no colisionan), nunca URL/query sin normalizar; las claves de jobs, medios y exportaciones además quedan ligadas a la operación concreta y revisión.
3. Puertos `Application` para leer autorización vigente, derivar clave con material seguro exclusivo del backend, ejecutar/reintentar por un adaptador FUTURO y revalidar exactamente el scope y recibo (también al consumir un job). No acoplar worker al viejo outbox PLATFORM C03-07 ni registrar servicios hospedados.
4. Pruebas .NET10 positivas/negativas de colisión A/B, sucursal, recurso, actor, revisiones/epoch, cancelación, expiración, revocación durante ejecución, operación ajena, firma/clave malformada, replay, recibos inconsistentes y reautorización.
5. Fixture MySQL 8.4/ InnoDB **desechable, sin red ni puertos**, con integridad compuesta por tenant+branch+resource en cuatro tablas: caché, cola de trabajos, medios como referencias (no blobs) y exportaciones como manifiestos opacos. Simular carrera concurrente del mismo job, rechazo de cross-tenant válido y suspensión/revocation de accesos antes de reintentar. No se permitirá cualquier cuenta DB normal leer filas globales.

**No implementado ni certificable ahora:** Redis u otro cache real, worker/cola productiva, outbox de tenant conectado, R2/Stream/Cloudflare, middleware autenticado, MFA, presigned URLs, exportaciones reales, DNS/host, backups/restore productivos, DSN de tenants, despliegues, VPS, pagos o producción. Tampoco se implementará un borrado real de archivos. `main`, Word/PDF canónicos y SOLQARYN intocables.

## 3. Matriz de aceptación y negativas obligatorias

| ID | Flujo | Evidencia positiva esperada | Negativa bloqueante |
| --- | --- | --- | --- |
| C10-K01 | Clave caché por recurso privado | Mismo scope/revisiones/variante de respuesta produce clave estable de 64 hex tras prefijo; distinta entre tenants, branch, actor, recurso, entorno, época de acceso, formato/proyección y categoría | A y B con el mismo resource_id no comparten key ni entrada |
| C10-K02 | Protección de privacidad | Clave HMAC separada de identificadores; secreto de plataforma >= 32 bytes y sin serializar; namespace no incluye datos | No aceptar clave/DSN/host/tenant provisto por cliente como autorización |
| C10-J01 | Trabajo en cola | Envoltorio inmutable, operación, scope, fencing y reautorización al ejecutar/reintentar | No consumir si tenant/usuario/sucursal/recurso están suspendidos, vencidos, revocados o tienen epoch cambiado |
| C10-J02 | Concurrencia y replay | Un claim válido por operación en 12 ejecuciones simultáneas; idempotencia y fence protegido | Sin doble ejecución, ack stale ni cruce A/B aun con IDs iguales |
| C10-M01 | Media Cloudflare futura | Solo referencia opaca+propietario+versión; tenant/branch/resource FK compuesta en MySQL desechable | Ningún tenant lee el metadato de B ni recibe enlace permanente/clave de B |
| C10-E01 | Exportaciones | Manifiesto opaco y versión aislados; scope revalidado tras preparación | No reutilizar export de A para B, ni reanudar tras suspensión/revisión obsoleta |
| C10-R01 | Recuperación | Reintento exacto recupera el mismo recibo sin abrir acceso fuera de scope | No permitir replay de otra operación/tenant, ni recibir un recibo falsificado |
| C10-A01 | Auditorías | A funcional, B seguridad, C arquitectura/performance; pruebas reales con SHA y logs | Cualquier P0/P1, pendiente o skipped obligatorio = NO-GO |

## 4. Arquitectura y decisiones técnicas

- Una clave HMAC es solamente un identificador lógico; **jamás** se trata como bearer token ni sustituye membresía/propiedad/permisos del backend. La autorización debe volver a verificarse al **enqueue**, **dequeue**, antes del **commit** y antes de servir una descarga; la regla de atomicidad del adaptador es obligatoria para integración futura.
- `scope` de caché puede reutilizarse entre solicitudes autenticadas equivalentes mientras todas las revisiones coincidan; `job/media/export` añaden el `operation_id` para evitar alias de efectos independientes.
- Aislar SHARED y DEDICATED con el modo y slot protegido en el scope; no aceptar parámetros de ubicación del cliente.
- Tenant suspendido o deactivated => denegación inmediata; la reactivación debe incrementar epoch y generar claves distintas. Retener/invalidar claves no implica borrar históricos.
- MySQL guardará **solo referencias y metadatos de prueba**, no adjuntos ni contenido multimedia. Esto no crea tablas persistentes del SaaS; los datos de prueba se eliminan con el contenedor.
- El único outbox real existente, `PlatformOutboxWorker`, continúa exclusivamente PLATFORM. C04-10 no hereda su permiso para eventos tenant ni lo modifica.

## 5. Auditoría y fusión estricta

Antes de integración: diff exacto; cero secretos, rutas productivas, DSN o cambios fuera del alcance; compilación `.NET 10 Release -warnaserror` y pruebas positivas/negativas, prueba MySQL 8.4 real y regresiones anteriores relevantes; auditorías independientes A/B/C registradas y sin fallos. La CI debe terminar `completed/success` sobre el SHA final. Después: fusionar únicamente en `dev` y repetir CI POSTMERGE; acta documental propia con SHA/run IDs, PR documental con PREMERGE/POSTMERGE y dictamen institucional permanente. **No contabilizar `skipped` condicionales como PASS.**

Cualquier ausencia de evidencia, hallazgo P0/P1 o incompatibilidad de código => **C04-10 PENDIENTE / NO CERTIFICADA**. No avanzar a C04-11. El capítulo 04 sigue NO CERTIFICADO hasta C04-12.

## 6. Impacto anticipado

`frontend/`: ninguno; `backend/`: dos archivos de contratos/puertos sin DI/HTTP ni adaptadores; `database/`: un fixture aislado y un runner descartable, no migración de producto; `qa/` y `.github/workflows/`: auditorías y gates; `docs/c04/`: especificación y eventual acta, sin cambiar el maestro. Reversión limitada a commits/PR de C04-10 sin DDL persistente. No se ejecutan referencias externas de Skills.
