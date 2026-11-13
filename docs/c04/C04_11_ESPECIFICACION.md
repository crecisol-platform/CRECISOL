# CRECISOL — C04-11: especificación de pruebas ofensivas multitenant

**Fecha:** 12-11-2026, Honduras. **Estado:** PREPARACIÓN / NO CERTIFICADA. Repositorio autorizado: crecisol-platform/CRECISOL, ID 1450414592, base dev a80fdc87fe15b13a22af268d007da88d2d270810.

## 1. Autoridad, precondición y alcance

La [certificación institucional C04-10](https://github.com/crecisol-platform/CRECISOL/pull/105#issuecomment-6841621470) acredita 28/28 workflows SUCCESS, auditorías A/B/C y alcance **solo contratos internos y pruebas sintéticas**. C04-11 está habilitada únicamente para preparación; C04-12 está bloqueada.

Documento Maestro Único canónico: docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx. Huella SHA-256 registrada en docs/MASTER_REFERENCE.md: ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. Otros referentes: docs/PLAN_37_CAPITULOS.md, docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md, docs/SEGURIDAD_MULTITENANT.md y Skill CRECISOL.

**Requisito oficial:** Pruebas ofensivas multitenant. **Prueba positiva:** BOLA, IDOR, host, claims y DSN falsificados. **Negativa que impide cerrar:** cualquier lectura/escritura cross-tenant. Toda microfase requiere auditorías A/B/C y pruebas reales en el SHA final; skipped nunca se contabiliza como PASS.

## 2. Amenazas y límites de las pruebas

**P0:** un usuario A recupera o modifica objeto de B por UUID conocido, incluso si ambas empresas usan el mismo resource_id; Host/Forwarded/claims falsificados cambian tenant; branch, actor o resource de otro ámbito conceden acceso; DSN/slot Dedicated seleccionado desde datos de navegador; cache/media/exports de otro tenant; suspensión permite replay; una carrera concede dos claims.

**P1:** certificados de host caducados/duplicados; parser de Host acepta CRLF, URL, path, wildcard o credenciales; silencio/fallback ante fallo de catálogo; acceso previo a autenticación; keys/recibos de caché tratados como bearer tokens; errores revelan identificadores; cuentas SQL de tenant leen tablas completas, SELECT/UPDATE/DELETE sin scope; revisión/epoch/lease invalidado se vuelve a usar.

Los contratos C04-01..10 están mayormente **sin adaptadores productivos**. No existe todavía API privada autenticada, middleware JWT/MFA, router DSN productivo, Redis, R2/Cloudflare, workers de clientes ni exportaciones reales. Por tanto, esta microfase solo puede atacar **contratos Domain/Application reales mediante fakes backend confiables, pruebas de integración en MySQL 8.4 desechable y regresiones**. Nunca afirmar que se han probado endpoints privados, auth real, descargas o producción inexistentes. Negativas no ejecutables productivamente se registran como PENDIENTES DE INTEGRACIÓN FUTURA, no PASS.

## 3. Matriz de ataques obligatorios

| ID | Vector sintético | Evidencia de denegación obligatoria |
| --- | --- | --- |
| O11-01 | BOLA/IDOR: A solicita resource_id de B, ambas empresas válidas | Metadata/permiso ajeno rechazados; lectura y escritura SQL de B inaccesibles |
| O11-02 | Sucursal de B, grant de B o acción de escritura usando actor A | Scope C04-06 debe exigir tenant+branch+actor+acción+revisión exactos |
| O11-03 | Host manipulado (URL, puerto, wildcard, CRLF, path, duplicado, expirado o rebind) | Parser C04-04 y catálogo de host único fallan cerrados; hint no autoriza |
| O11-04 | Claim tenant_id=B, role=SuperAdmin y bearer sin firma dentro de petición A | C04-05 ignora claims sin verificar; exige sesión, host, membresía y tenant activo |
| O11-05 | Inyección DSN de B, selector Dedicated o slot ajeno por header/URL | C04-03 autoriza tenant en servidor y rechaza catálogo B; ningún DSN en contrato |
| O11-06 | Cache key, job operation_id y recibos de B, variante o epoch obsoletos | C04-10 rechaza key/recibo y nunca convierte un GUID en autoridad |
| O11-07 | SQL SELECT/UPDATE/DELETE/DDL directo, FK cruzadas y entradas hostiles | Contenedor MySQL 8.4 sin red; roles sin acceso a tablas, 0 modificaciones B |
| O11-08 | Doce reclamos simultáneos con fencing, replay y suspensión | Exactamente 1 CLAIMED/11 BUSY; ningún ack duplicado/obsoleto |
| O11-09 | Cancelación, revocación concurrente y caída de catálogo | Ningún fallback permisivo ni efecto después del fallo |
| O11-10 | A/B/C, .NET10, MySQL 8.4, regresiones anteriores y C03-10 integral | Todos los workflows aplicables SUCCESS sobre SHA exacto |

**Positivo obligatorio:** A/B pueden ver solo su propio ámbito, incluso con resource_id iguales; funcionamiento permitido explícitamente por tenant y recurso. **Negativos:** cada ataque debe mostrar rechazo real con una aserción/test no vacía; las negativas que no se ejecuten impiden cerrar.

## 4. Arquitectura y blast radius

Solo archivos nuevos: backend/tests/Crecisol.TenantOffensive.ContractsTests/ (harness .NET10); database/tests/c04_11_* (fixture aislado o regresión ofensiva C04-10); qa/c04_11_offensive_audits.py; .github/workflows/c04-11-offensive.yml; docs/c04/C04_11_*.

**Sin modificar** frontend, código de producto de backend, esquema productivo, Documento Maestro Word/PDF, main, producción/VPS/DNS/Cloudflare, cobros ni solqaryn/Solqaryn. Los contenedores y cuentas SQL de ensayo se destruyen tras la prueba; un PR fallido se cierra/revierte por SHA, preservando los resultados fallidos.

## 5. Puertas secuenciales de certificación

Crear PR técnico y documental en ramas separadas, sin merge hasta pruebas positivas/negativas .NET/MySQL reales y auditorías A/B/C sobre HEAD final. Integrar técnico solo en dev tras CI PREMERGE 100% SUCCESS, repetir POSTMERGE; registrar SHA/runs en expediente documental; PREMERGE documental, merge exclusivo a dev, POSTMERGE documental y comentario institucional permanente. Si un P0/P1, falla, pending o skipped obligatorio queda abierto: **C04-11 NO CERTIFICADA; C04-12 BLOQUEADA**. Incluso certificada en su alcance sintético, C04 completo NO CERTIFICADO hasta C04-12, sus tres auditorías integrales y aceptación prevista.
