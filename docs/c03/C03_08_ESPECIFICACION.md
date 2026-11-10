# CRECISOL — C03-08 | Configuración segura y entornos locales

**Estado de inicio:** EN DESARROLLO TÉCNICO, **NO CERTIFICADA**. En `dev`, C03-01..07 **CERTIFICADAS (7/10)**; C03-08 sigue **NO CERTIFICADA**, C03-09 NO INICIADA, **C03 NO CERTIFICADO, C04 BLOQUEADO** y **2/37 capítulos certificados (C01/C02)**.

**Autoridad:** Documento Maestro Único de CRECISOL `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; `docs/MASTER_REFERENCE.md`, `docs/C03_PREPARACION.md`, `docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md`, `AGENTS.md` y Skill CRECISOL. Las 8 puertas normativas no sustituyen las 10 microfases C03.

## Objetivo

Implementar un contrato **tipado, explícito y fail-closed** para configuración local/CI, sin activar nuevas funciones comerciales, sin modificar el Documento Maestro, sin rutas HTTP nuevas, sin datos privados ni servicios externos. **No** convertir una variable del navegador, URL, Host, cookie, token o configuración administrable del tenant en selector de entorno, DSN o permiso de worker. Valores institucionales editables no se incrustan aquí: esta configuración es únicamente operativa/técnica.

## Corte ejecutable

1. `Crecisol.Api/Configuration/LocalRuntimeSettings.cs`: tipos `Disabled` (único estado por defecto), `Local` y `Ci`; parseo estricto de `CRECISOL_C03_08_PROFILE`, `CRECISOL_C03_08_MAX_WORKER_BATCH` (1..32), `CRECISOL_C03_08_ALLOW_EXTERNAL_NETWORK` (solo literal `false`) y `CRECISOL_C03_08_TEST_SOCKET` (ruta Unix de `/tmp/` sin traversal ni inyección). Sin secretos ni cadena de conexión.
2. Integración exclusiva **en `Crecisol.Api/Program.cs`, composition root**: lee variables del proceso servidor, preserva `CRECISOL_C01_ALLOWED_HOSTS`, `CRECISOL_C01_READONLY_DSN` y el único GET público. Host allowlist tipada con tamaño limitado, DNS ASCII validado, sin duplicados/wildcards. Si el perfil está ausente y aparece cualquier opción dependiente, falla el arranque; si está presente, admite solamente hosts loopback. Sin `AddHostedService`, administrador, publicación, auth nueva ni rutas privadas.
3. Pruebas .NET10 `Crecisol.LocalConfig.ContractsTests` con casos positivos y negativos para perfiles, números, red remota, socket, inyección, path traversal, host/port/encabezado falsificados, duplicados y perfil huérfano.
4. CI MySQL8.4 desechable con `--network none`, socket UNIX real y cuenta de mínimos privilegios reutilizando la prueba pública existente mediante `CRECISOL_C03_08_LOCAL_PROFILE_SMOKE=1` **solo en el harness CI**; el perfil no realiza ninguna conexión automática, no activa el outbox ni crea servicios.
5. Auditorías A/B/C propias: procedencia del Word y secuencia; al menos 16 mutaciones negativas sin rebajar controles; compilación .NET10 Release y regresiones históricas C01/C02/C03-01..07, con pasos CI y SHAs exactos.

## Límites e impacto

- `frontend/`: ningún cambio; no autoriza panel administrativo, SEO empresarial ni branding de tenant.
- `backend/`: archivo tipado de configuración API, integración mínima en composición y proyecto de pruebas. La configuración no concede permisos, roles, conexiones ni efectos.
- `database/`: **sin DDL ni cambios persistentes**. Solo se añade una opción al runner público desechable para probar un entorno CI con perfil local, sin modificar permisos MySQL.
- Sin `.env`, `appsettings.Production.json`, claves o contraseñas versionados; secretos sintéticos solo variables efímeras en el runner; nunca imprimir DSN.
- **Producción NO HABILITADA**: distinguir entorno ASP.NET de perfil local C03-08; no afirmar que exista aislamiento privado BOLA/IDOR/MFA, router shared/dedicated, conexiones productivas, worker real ni validación comercial por estos contratos.
- Riesgos P0: fallback a prod, env mutable o solicitado por cliente, perfil erróneo aceptado, conexión remota por variables técnicas, fuga de secretos, dobles registros/estados falsificados. Fail-closed en cada uno.
- Reversión por nuevo PR sobre `dev`; no existen migraciones aplicadas ni cambios en servicios, VPS, DNS, pagos, `main` o SOLQARYN.

**Puerta de certificación:** código y pruebas de configuración más integración real CI A/B/C, PR técnico PREMERGE/POSTMERGE, acta específica independiente y CI de esa acta; sincronización de estado oficial mediante otro gate y aprobación del propietario. Antes de obtener todo ello **C03-08 NO CERTIFICADA**. No abrir C03-09 ni C04.
