# CRECISOL · C01: primer corte de código y trazabilidad

**Rama:** feature/c01-identidad (desde dev).
**Fuente primaria:** docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, capítulo 01, apartados 1.1–1.7 y sus tres tablas. Esta ficha NO crea un documento maestro nuevo.
**Estado:** C01 EN DESARROLLO, implementación parcial, compilación inicial comprobada en CI sin certificación; capítulos certificados: 0/37.

## Qué se implementó en este primer corte

- Domain: BrandSnapshot tipado, ámbito PLATFORM/TENANT, revisión, estado y validación estricta.
- Application: caso de uso ReadPublishedPlatformBrand que solo devuelve el snapshot público aprobado del plano de plataforma. No expone borradores, campos privados ni marcas tenant.
- Infrastructure: fuente JSON local versionada y de lectura exclusiva para bootstrap. Será sustituida por repositorio MySQL autorizado cuando existan los mecanismos correspondientes; no fingir que ya existe almacenamiento productivo.
- API .NET 10: GET /api/v1/public/branding/platform; sin endpoints de administración, sesiones o elección de tenant.
- Frontend Angular 22: pantalla pública institucional, estados de carga/error, recuperación manual, identidad dinámica con bindings seguros y SEO básico; no pago ni contratación reales.
- Logo: script determinista que extrae la imagen oficial de Word sin redibujarla, tras comprobar huellas SHA-256 de documento e imagen. El PNG generado se excluye del commit.
- Database: DDL inicial de scopes, versiones y publicaciones, probado en MySQL 8.4.13 temporal y aislado; no se aplicaron migraciones persistentes.
- QA: compilaciones Angular 22 y .NET 10, pruebas HTTP de API pública y cinco pruebas negativas MySQL ejecutadas con éxito en GitHub Actions.

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
| §1.7, pruebas C01 | QA de identidad y separación | Build Angular/.NET, HTTP y FK MySQL comprobados | Pruebas UI, autenticación, aislamiento de tenants reales y auditorías integrales |

**Cobertura todavía incompleta:** las 21 filas originales (7+8+6) y los 34 párrafos del capítulo 01 no se declaran implementados por este primer corte. Los capítulos futuros solo se leen para dependencias, no se desarrollan.

## Impacto, riesgos y rollback

- Frontend: solo interfaz pública inicial, sin lógica de permisos ni paneles administrativos.
- Backend: caso de uso y proyección pública; sin PII, sin operaciones de escritura.
- Database: DDL probado solo en contenedor MySQL 8.4.13 descartable de CI; sin migración real aplicada, datos persistentes ni secretos.
- Riesgo P0 no cerrado: no existe seguridad multitenant ni autenticación real. NO habilitar endpoints tenant ni datos de clientes.
- Riesgo P1 no cerrado: no hay persistencia MySQL integrada, autorización del editor, versiones publicadas con auditoría ni comprobación visual con navegador. Los builds Angular/.NET y pruebas HTTP limitadas sí se ejecutaron en CI.
- Controles de privacidad: sin datos reales, contratos legales sin emisor inventado, no se declaran dominios verificados.
- Rollback: revertir los commits de esta microtarea en feature/c01-identidad; no hay datos que migrar ni infraestructura externa afectada.

## Herramientas y pruebas

**Entornos diferenciados:** el contenedor local tiene Node 22.16 y carece de .NET/MySQL. GitHub Actions ejecutó Node 24.22.2 con Angular 22.2.2 / TypeScript 6.0.3, .NET SDK 10.0.403 y MySQL 8.4.13 efímero.

**Pruebas ejecutadas y verificadas:** foundation/preflight, logo con SHA-256, `npm ci`, build Angular, contrato Node, build .NET, 11 comprobaciones de dominio, HTTP público con límites CORS/métodos y MySQL 8.4 con cinco rechazos. GitHub Actions CI #42516130198.

**No ejecutadas todavía:** UI interactiva en navegador, accesibilidad automatizada, carga y rendimiento, autenticación/tenancy de clientes reales, dominios/hosting y las tres auditorías integrales. No hay SaaS operativo.

## Auditorías y cierre

- Auditoría A — trazabilidad: **ABIERTA**, exige 21 filas, 34 párrafos y aceptación real.
- Auditoría B — seguridad: **ABIERTA**, se comprobaron cinco negativas SQL y rechazo HTTP; aún faltan auth, tenancy y pruebas adversariales completas.
- Auditoría C — arquitectura/rendimiento/UI: **ABIERTA**, los builds y HTTP básico ya pasaron; faltan navegador, accesibilidad, rendimiento y regresión.
- Acta de C01: **NO EMITIDA**. Aprobación: **PENDIENTE**. Siguiente capítulo C02: **BLOQUEADO**.

Esta entrega solo comienza C01. No es un prototipo React ni una certificación del SaaS.

## Evidencia verificable de la primera CI de C01

- Commit evaluado: `e83efe2e379437e2e9830ed12cbd19a23ad30de7`.
- GitHub Actions: https://github.com/solqaryn-platform/CRECISOL/actions/runs/42513216018
- Resultado comprobado por los pasos individuales de GitHub: `Foundation scope and secrets baseline` = SUCCESS; `Validate C01 contract and approved logo source (static only)` = SUCCESS; `Verify approved logo extraction from canonical Word` = SUCCESS; `Execute native Node brand contract tests (no packages)` = SUCCESS.
- Estas pruebas solo validaron estructura, fuente autorizada, extracción y campos de marca. No prueban la API .NET, ejecución Angular, rutas de tenant ni MySQL; no son certificación de C01.
- El entorno local usado para revisar el origen tampoco cuenta con el SDK .NET 10 ni MySQL 8.4; Angular 22 requiere Node 24.15+ y TypeScript 6.0.x. Instalar/probar este toolchain sigue siendo condición previa a la compilación.
- El PR #10 sigue en borrador. La regla de avance permanece: auditorías A/B/C, pruebas positivas y negativas reales, hallazgos P0/P1 resueltos y aprobación antes de C02.

## Segundo corte: pruebas reales de compilación, HTTP y MySQL (8 de noviembre de 2026)

**Evidencia principal:** https://github.com/solqaryn-platform/CRECISOL/actions/runs/42516130198 — tres trabajos concluidos SUCCESS, token de solo lectura, sin despliegues.

| Evidencia | Resultado comprobado | Límite |
| --- | --- | --- |
| Angular | Node 24.22.2, Angular 22.2.2, TS 6.0.3, `npm ci` y `ng build`: aprobados | Aún no se ejecutó navegador real |
| Dependencias | `frontend/package-lock.json` v3, 353 entradas | Árbol reproducible de paquetes; falta auditoría de seguridad de terceros |
| .NET | SDK 10.0.403, build Release y 11 grupos de comprobaciones: aprobados | Lógica pública inicial, sin cuentas/tenants reales |
| HTTP | `qa/c01_http_smoke.py`: aprobada | GET marca, cabeceras, rechazo POST/admin, CORS y query `tenant_id` sin efecto |
| MySQL | MySQL 8.4.13: esquema creado, 2 publicaciones ficticias y 5 escenarios negativos rechazados | Contenedor temporal sin red, sin migración real ni autorización backend |
| Marca | Word maestro y logo extraído validados con hashes SHA-256 | Sin redibujar ni duplicar binario |

**NO GO vigente:** C01 no cubre todavía 21 filas originales y 34 párrafos completos, autenticación, aislamiento efectivo entre tenants, editor con publicación/rollback y auditoría, validación de dominios, interfaz probada ni auditorías A/B/C cerradas. 0/37 certificados; C02 no iniciado.

**Skill externa consultada solo en modo lectura:** EXT-10 Angular Developer, repositorio original `angular/angular`, commit `6808966993bf2b2d455d9b6245e960a59a01b9a9`, ruta `skills/dev-skills/angular-developer/SKILL.md`. No se instaló ni copió; se verificó compatibilidad y disciplina de build.

**Rollback:** revertir los commits de C01 en `feature/c01-identidad` o cerrar PR #10. Sin cambios en `main`, `dev`, VPS, datos reales ni SOLQARYN.
