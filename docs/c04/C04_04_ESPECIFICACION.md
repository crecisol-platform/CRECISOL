# CRECISOL · C04-04 — Especificación de resolución interna de hosts verificados

**Fecha:** 12-11-2026 (Honduras). **Repositorio:** `crecisol-platform/CRECISOL` (GitHub ID original `1450414592`). **Base:** `dev` SHA `537e520222bc54ef6ab512768a078154c543f679`. **Rama:** `feature/c04-04-resolucion-hosts-verificados-20261112`. **Estado inicial:** PREPARACIÓN. C04-04 NO CERTIFICADA; C04-05 BLOQUEADA.

## 1. Autoridad y puerta anterior

Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo C04: 72 párrafos, texto SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, control `qa/c04_01_normative_inventory.py`. Ocho puertas normativas MAE-C04 distintas de 12 microfases técnicas.

Puerta anterior verificada: C04-03 certificada exclusivamente en contratos internos SHARED/DEDICATED, PR técnico #88 y [acta PR #89](https://github.com/crecisol-platform/CRECISOL/pull/89#issuecomment-6831740515). PR #89 fusionado en `537e520222bc54ef6ab512768a078154c543f679`, 4/4 CI documental POSTMERGE SUCCESS. **C04 (capítulo completo) NO CERTIFICADO; 3/37 capítulos**.

Plan técnico `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`: C04-04 “Resolución de hosts verificados”; positivo “Allowlist y ciclo de validación de dominio”; negativo “No confiar en Forwarded no verificado”.

## 2. Arquitectura y separación

Se implementan **dominio y caso de uso internos**, más validador ASP.NET de transporte **no registrado en Program.cs**, pruebas .NET 10 y auditorías A/B/C. C04-04 no crea nuevos endpoints, middleware o DI, no valida DNS en vivo ni autoriza sesiones privadas. El host es solamente una **pista de contexto**, nunca permiso de usuario ni selector de base de datos.

| Capa y archivo | Responsabilidad | Restricción |
| --- | --- | --- |
| `Domain/Tenancy/VerifiedTenantHost.cs` | Host ASCII canónico de etiquetas DNS exactas; vínculo tenant/host con revisión, modo Subdomain/CustomDomain, estado Pending/Verified/Suspended/Revoked, fechas UTC de verificación/vigencia | Sin comodines, U-label Unicode, direcciones IP, URL, puerto, whitespace, doble punto, path, caracteres de control ni segmentos vacíos; PathSlug no se resuelve a empresa por host |
| `Application/Tenancy/ResolveVerifiedTenantHost.cs` | Buscar en catálogo protegido por host tipado, exigir coincidencia canónica exacta, una sola asociación, estado Verified, ventana UTC vigente; devolver únicamente pista interna no autorizante | Prohibido fallback entre empresas, confiar en host como membresía o elegir DSN/slot; reloj backend inyectado, sin datos del cliente para tiempo |
| `Api/Configuration/StrictVerifiedHostTransport.cs` | Analizar cabecera HTTP Host única y directa, rechazar `Forwarded`, `X-Forwarded-Host`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Port`, `X-Original-Host`, `X-Forwarded-Server` | Solo validador aislado, **NO conectado a middleware ni ruta actual**; la política de proxies confiables se diseñará separadamente, nunca por cabecera no autenticada |
| `backend/tests/Crecisol.VerifiedHosts.ContractsTests/` | Pruebas .NET10 sobre valores reales de `HttpRequest` de ASP.NET Core, catálogo simulado y reloj backend de prueba | Demuestran parser/contratos/decisiones de estado; NO demuestran DNS real, registro de catálogos ni autorización HTTP privada |
| `qa/c04_04_hosts_audits.py`, workflow `c04-04-verified-hosts.yml` | Auditorías A/B/C, mutaciones y pruebas compiladas `Release -warnaserror` con pruebas negativas y huella SHA/run | Sin red a DNS, producción, servicios, secretos o migraciones |

## 3. Política P0/P1

- Host recibido es entrada no fiable: incluso si coincide con un dominio Verificado, **NO prueba identidad, membresía, ownership, autorización, estado de contrato ni permiso de almacenamiento**. C04-05/06 y C04-11 deben verificarlo después.
- Encabezados `Forwarded` y variantes `X-Forwarded-*` no se admiten en esta frontera; nunca derivar host de cabeceras de proxy no verificadas. La propiedad Host de HttpRequest tampoco se trata como autoridad por sí sola: requiere **catálogo confiable exacto**.
- Exigir host ASCII DNS limpio, comparación ordinal case-insensitive normalizada. No aceptar prefijos, sufijos, wildcard, puertos, esquemas `http://`, query/path, nul bytes, Unicode U-label, IPv4/IPv6, `localhost` o dominios no aptos. Los A-label punycode solo podrían ser considerados tras verificación explícita del catálogo y políticas posteriores de IDN; no hay validación DNS en este corte.
- El catálogo debe tener **un único vínculo** para cada host. Cero o múltiples vínculos = denegación; nunca escoger el primero ni usar la pista para otro tenant; también rechazar coincidencia host divergente o tenant vacío.
- Ciclo de vida: `PendingVerification`, `Verified`, `Suspended`, `Revoked`; solo Verified con `verifiedAtUtc <= nowUtc < validUntilUtc`, `revision > 0`, UTC y estructura coherente habilita una pista. Caducidad, suspensión y revocación deniegan.
- `TenantAddressMode.PathSlug` depende de un contexto de ruta **separado**, no resuelve tenant con solo host; tres modalidades comerciales no son permisos.
- El vínculo de host debe ser consecuencia de prueba de propiedad ejecutada por backend administrado, pendiente de fases de provisión; `RestoreFromTrustedCatalog` **no realiza DNS** ni crea registros.
- Un catálogo en memoria de pruebas NO acredita exclusividad transaccional, cambios DNS, caducidad real, cache invalidation, TLS/SNI, trusted proxy network, takeover, dominios huérfanos ni BOLA HTTP. Son pendientes expresos de C04-05/07/11 y controles de integración antes de habilitar tráfico privado.

## 4. Matriz de pruebas obligatorias

Positivos: normalización ASCII de mayúsculas; subdominio verificado de A; dominio propio verificado de B; hora al inicio exacto de vigencia; labels válidas; encabezado Host único sin forwarded; dos tenants con hosts distintos.

Negativos: host vacío/nulo, leading/trailing dot, doble punto, Unicode U-label, wildcard, punycode sin prueba (catálogo no lo contiene), URL, IP, localhost, :puerto, credenciales, CRLF, query, slash, backslash, percent-encoding, labels mal formadas y longitud excesiva; `Forwarded` y cada `X-Forwarded-*` sospechosa; Host duplicado; catálogo sin vínculo, coincidencia incorrecta, dos candidatos, pending, suspended, revoked, expired, fecha futura, modo PathSlug, revisión inválida, UTC inválido, tenant nulo, reloj no UTC, cancelación previa/durante/tras lectura, error catálogo; ninguna selección de DSN ni DI/API privada.

## 5. Gates y reversión

1. Construir solo los archivos de C04-04; revisar diff y las regresiones de C01/C03/C04-01..03. No cambiar `Program.cs` ni host allowlist pública.
2. Auditoría **A**: Word/PDF + 72 párrafos, cumplimiento de la fila C04-04, invariantes y sin salto de fase. **B**: mutaciones individuales de confianza y pruebas ofensivas HTTP con ASP.NET. **C**: .NET10 Release `-warnaserror`, dependencias Domain/Application/API, regresiones y evidencia del mismo SHA/run.
3. Cero P0/P1 detectados en el **alcance interno no conectado**, PREMERGE sobre HEAD exacto, POSTMERGE sobre SHA de `dev`, acta separada con PRE/POSTMERGE y comentario institucional final. No contar `skipped` como PASS.
4. Si falta un control, **C04-04 PENDIENTE; C04-05 BLOQUEADA**. No declarar DNS/verificación de propiedad, auth privada, router, exposición de endpoints ni capítulo C04 completo certificado.
5. Revertir exclusivamente commits C04-04 si fuera necesario; ninguna DB, producción ni despliegue ha sido tocado. `main`, Word/PDF, SOLQARYN, VPS, Cloudflare, DNS, pagos y facturación quedan fuera de alcance.
