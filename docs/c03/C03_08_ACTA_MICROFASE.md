# CRECISOL — C03-08 | Acta técnica, configuración segura y auditorías A/B/C

**Estado de este registro:** **C03-08 CERRADA TÉCNICAMENTE (8/10), NO CERTIFICADA** hasta verificación independiente de CI de este PR documental y eventual actualización del estado oficial. El estado canónico de `dev` continúa **C03-01 a C03-07 CERTIFICADAS (7/10), C03-08 NO INICIADA** hasta formalización. **C03-09 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02)**.

## Autoridad, alcance y autorización

- Repositorio único `solqaryn-platform/CRECISOL`, desarrollo sobre `dev` y rama `feature/c03-08-local-config`, PR técnico [#70](https://github.com/solqaryn-platform/CRECISOL/pull/70). Nunca `main` ni `solqaryn/Solqaryn`.
- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` aprobado, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 **`e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**; PDF original intacto, tabla T01 y 7 párrafos/9 filas originales del capítulo. Las 8 puertas normativas MAE no sustituyen las 10 microfases técnicas.
- Autorización del propietario, anterior a las pruebas, 10 de noviembre de 2026: **«Continúa por favor»**, inmediatamente después de la certificación C03-07 y dentro del mismo protocolo. Es autorización para continuar C03-08; **no se inventa una aprobación cronológicamente posterior ni se amplía a C03-09, C04 o producción**.

## Alcance técnico cerrado

- `backend/src/Crecisol.Api/Configuration/LocalRuntimeSettings.cs`: tipos `Disabled` (por defecto), `Local`, `Ci`; validación estricta del perfil, hosts, máximo de batch 1..32, denegación de acceso a red externa y de DSN/URL/socket no desechable; rutas de prueba `/tmp/` sin traversal ni caracteres peligrosos. No contiene ni registra secretos, conexiones, router de tenant ni roles.
- `backend/src/Crecisol.Api/Program.cs`: composición valida esas opciones al arranque **solo desde variables del proceso**, jamás desde URL/Host/headers/claims del navegador. Preserva el único GET público de Branding; sin nuevos endpoints, sin control de auth privada, sin `AddHostedService`, sin habilitar workers.
- `backend/tests/Crecisol.LocalConfig.ContractsTests/`: **72 aserciones ejecutables** .NET10 sobre entradas tipadas, perfiles válidos, producción rechazada, red externa rechazada, números, rutas/sockets, duplicaciones, hosts/inyección.
- `database/tests/c01_04_api_mysql_smoke.sh`: modo opt-in `CRECISOL_C03_08_LOCAL_PROFILE_SMOKE=1` para usar el **socket Unix real** de un MySQL 8.4 desechable `--network none` con cuenta de mínimos permisos, sin almacenar credenciales. Conecta únicamente la API pública sintética ya probada, sin afectar datos comerciales.
- `qa/c03_08_local_config_audits.py` y `.github/workflows/c03-08-local-config.yml`: Word A, **18/18 mutaciones adversariales B**, C de evidencia real .NET10 Release/CI MySQL HTTP y regresiones C03-07 completas con marcadores del mismo job.
- `frontend/`: intacto. `database/`: ninguna migración ni DDL productivo. No se instalaron servicios, no se subieron secretos, no se activaron despliegues/VPS/DNS, pagos, cloud ni SOLQARYN.

## CI PREMERGE técnica PR #70

HEAD exacto `97ac8431ee4b1d3ab263dc54205b2bd22f07600a`:
- [#42823974509](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42823974509) — C03-08 A/B/C, 18 mutaciones, .NET10, **72 aserciones**, MySQL8.4, HTTP y regresión runtime C03-07 con acta histórica: **completed/success**.
- [#42823974454](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42823974454) — Foundation: **completed/success**.
- Otros workflows condicionales `skipped` **no se contabilizaron como success**. Intentos PREMERGE previos fallidos se conservaron y corrigieron: referencia histórica literal C03-07 y falta de ejecución de prueba histórica C03-07 en el mismo job.

## CI POSTMERGE técnica PR #70

Squash a `dev`, SHA exacto **`221afb6cc515fc79a3b6ca6e010af11f2e20b0a2`**. Verificación directa **6/6 CI POSTMERGE `push/completed/success`**, sin fallos ni pendientes:

| Workflow | GitHub Actions | Resultado |
| --- | --- | --- |
| C03-08 A/B/C, local typed config y MySQL8.4 | [#42824458247](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458247) | SUCCESS |
| C03-01 integral, Angular22/Chromium y regresiones | [#42824458457](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458457) | SUCCESS |
| C03-05 API/DTO, MySQL8.4/HTTP | [#42824458283](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458283) | SUCCESS |
| C03-06 módulo/DI, contratos y regresiones | [#42824458016](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458016) | SUCCESS |
| Portabilidad estática Windows/Linux | [#42824458308](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458308) | SUCCESS |
| Foundation | [#42824458319](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458319) | SUCCESS |

## Tres auditorías y limitaciones

**Auditoría A — trazabilidad:** Word SHA original, C03-07 certificado y C03-08 como siguiente, clean architecture sin cambio de dependencias, sin Word/PDF reescrito. **PASS**.

**Auditoría B — seguridad:** **18/18 alteraciones hostiles** rechazadas. Local/CI explícitos, no activación parcial de variables huérfanas, prohibición de host remoto/duplicado, conexiones y redes externas, socket no temporal, path traversal, expresiones de números, configuraciones "production" o roles nuevos. **PASS** para este corte.

**Auditoría C — runtime:** .NET10 Release, **72 contratos ejecutables**, conexión HTTP pública real con MySQL8.4 local desechable sin red, regresión completa C03-07 con MySQL real y acta histórica, además de Angular22/Chromium en CI integral. **PASS**.

**No se certifican BOLA/IDOR/MFA privados, tenancy real/DSN shared-dedicated, worker real ni publicaciones, operaciones comerciales, entorno de producción, administración integral o proveedores externos.** La cadena de conexión existente de lectura de marca sigue siendo la conexión sintética confiada del runner; el nuevo perfil técnico no concede permiso para emplearla ni cambia permisos SQL.

## Decisión y rollback

**C03-08 CERRADA TÉCNICAMENTE (8/10), NO CERTIFICADA** hasta que el acta documental supere **CI PREMERGE y POSTMERGE** y el estado oficial supere el gate de sincronización. Continúa **7/10 certificado formal**, **C03-09 NO INICIADA**, **C03 NO CERTIFICADO**, **C04 BLOQUEADO**, capítulos 2/37. No se confunden resultados de PR técnico con aprobación posterior fabricada.

**Rollback:** revert exclusivo del squash técnico `221afb6cc515fc79a3b6ca6e010af11f2e20b0a2` y acta mediante PR sobre `dev`; no existe base productiva ni despliegue que revertir.
