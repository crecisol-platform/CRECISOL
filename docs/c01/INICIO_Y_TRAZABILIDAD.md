# CRECISOL · C01: primer corte de código y trazabilidad

**Rama:** feature/c01-identidad (desde dev).
**Fuente primaria:** docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, capítulo 01, apartados 1.1–1.7 y sus tres tablas. Esta ficha NO crea un documento maestro nuevo.
**Estado:** C01 EN DESARROLLO, implementación parcial, sin compilación integral; capítulos certificados: 0/37.

## Qué se implementó en este primer corte

- Domain: BrandSnapshot tipado, ámbito PLATFORM/TENANT, revisión, estado y validación estricta.
- Application: caso de uso ReadPublishedPlatformBrand que solo devuelve el snapshot público aprobado del plano de plataforma. No expone borradores, campos privados ni marcas tenant.
- Infrastructure: fuente JSON local versionada y de lectura exclusiva para bootstrap. Será sustituida por repositorio MySQL autorizado cuando existan los mecanismos correspondientes; no fingir que ya existe almacenamiento productivo.
- API .NET 10: GET /api/v1/public/branding/platform; sin endpoints de administración, sesiones o elección de tenant.
- Frontend Angular 22: pantalla pública institucional, estados de carga/error, recuperación manual, identidad dinámica con bindings seguros y SEO básico; no pago ni contratación reales.
- Logo: script determinista que extrae la imagen oficial de Word sin redibujarla, tras comprobar huellas SHA-256 de documento e imagen. El PNG generado se excluye del commit.
- Database: DDL inicial de scopes, versiones y publicaciones, sin ejecutarse ni crear accesos.
- QA: comprobación estática de contratos y pruebas de dominio .NET preparadas, pendientes de ejecución real donde no existe SDK.

## Trazabilidad preliminar al maestro

| Fuente | Requisito | Primer corte | Pendiente obligatorio |
| --- | --- | --- | --- |
| §1.3, C01-T02-R01–R03 | Nombre, lema y descriptor oficiales | Config JSON publicada y DTO de lectura | Propagación a todas las seis experiencias, sin literales duplicados |
| §1.3 y §1.5, C01-T03-R01 | Marca coherente | Página pública inicial y metadatos | Correos, documentos, SEO SSR, QR, onboarding y SuperAdmin |
| §1.1, C01-T01-R04 | Seguridad por defecto | Solo GET público, sin privilegios de cliente | Auth real, permisos por acción y pruebas negativas integradas |
| §1.4, C01-T02-R04 | Respaldo SOLQARYN con independencia | Código y configuración exclusivos CRECISOL | Acreditación jurídica / uso de marca antes de publicaciones |
| §1.5, C01-T02-R06 | Dominio | websiteUrl nulo, sin declaración falsa | Titularidad, DNS/TLS, reserva de slugs y validación de hosts |
| §1.3, C01-T02-R07 / C01-T03-R02 | Marca propia de cada tenant | Modelo de scope preparado, sin endpoint tenant | Contexto seguro, membresía, aislamiento de datos y copyright |
| §1.1 y §1.6, C01-T01-R03 | Personalización editable con vista previa | Fuente única versionada; no hay editor | Draft, preview, autorización, auditoría, publicación, rollback |
| §1.6, C01-T02-R08 | Promesas comerciales honestas | Aviso de vista en desarrollo | Contrastar planes con capacidades efectivamente publicadas |
| §1.7, C01-T03-R04–R06 | Protección del entorno y comunicaciones | Sin correos/pagos ni enlaces productivos | Verificaciones de DNS, emisor, dominio, token y seguridad |
| §1.7, pruebas C01 | QA de identidad y separación | Smoke tests preparados | Angular+Node24, .NET10, MySQL8.4, UI y cross-tenant reales |

**Cobertura todavía incompleta:** las 21 filas originales (7+8+6) y los 34 párrafos del capítulo 01 no se declaran implementados por este primer corte. Los capítulos futuros solo se leen para dependencias, no se desarrollan.

## Impacto, riesgos y rollback

- Frontend: solo interfaz pública inicial, sin lógica de permisos ni paneles administrativos.
- Backend: caso de uso y proyección pública; sin PII, sin operaciones de escritura.
- Database: archivo DDL sin ejecución; no contiene migración aplicada ni secretos.
- Riesgo P0 no cerrado: no existe seguridad multitenant ni autenticación real. NO habilitar endpoints tenant ni datos de clientes.
- Riesgo P1 no cerrado: no hay persistencia MySQL, CI de compilación real, permisos de editor, revisiones/rollback con auditoría ni cotejo visual con navegador real.
- Controles de privacidad: sin datos reales, contratos legales sin emisor inventado, no se declaran dominios verificados.
- Rollback: revertir los commits de esta microtarea en feature/c01-identidad; no hay datos que migrar ni infraestructura externa afectada.

## Herramientas y pruebas

**Verificado en el entorno:** Node 22.16.0 y TypeScript 5.8.3 (NO válidos para compilar Angular22), Python 3; ausencia de SDK .NET10 y servidor MySQL8.4. La especificación oficial Angular 22 exige Node24.15+ en rama 24 y TS6.0.x.

**Pruebas ejecutables preparadas:** frontend/scripts/brand_contract.test.mjs (Node nativo), backend/tests/Crecisol.Branding.SmokeTests (al disponer de .NET10) y qa/c01_preflight.py (Python 3, estática), ejecutada también en la verificación de fundación del PR.

**No ejecutadas todavía:** compilación Angular22, ejecución API .NET10, pruebas de MySQL8.4, carga, UX de navegador, accesibilidad automatizada de la aplicación, cross-tenant y las tres auditorías completas.

## Auditorías y cierre

- Auditoría A — trazabilidad: **ABIERTA**, exige 21 filas, 34 párrafos y aceptación real.
- Auditoría B — seguridad: **ABIERTA**, faltan auth, tenancy, abuso y pruebas negativas ejecutadas.
- Auditoría C — arquitectura/rendimiento/UI: **ABIERTA**, faltan build, integración y revisión visual real.
- Acta de C01: **NO EMITIDA**. Aprobación: **PENDIENTE**. Siguiente capítulo C02: **BLOQUEADO**.

Esta entrega solo comienza C01. No es un prototipo React ni una certificación del SaaS.
