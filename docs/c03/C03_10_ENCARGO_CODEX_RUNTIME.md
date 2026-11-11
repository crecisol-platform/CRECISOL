# CRECISOL — C03-10 | Encargo único de pruebas reales para Codex (SIN GitHub Actions)

**Estado:** INSTRUCCIONES, no constancia de ejecución. **C03-10 NO CERTIFICADA; C03 NO CERTIFICADO; C04 BLOQUEADO.**
**Repositorios permitidos:** solo `solqaryn-platform/CRECISOL`. Prohibido escribir en `solqaryn/Solqaryn`.

## 0. Límites irrevocables

Este encargo lo asignará el propietario **a Codex en su propio entorno de ejecución**, no a la computadora del propietario. La decisión de programar, auditar y certificar queda en el asistente responsable; Codex es **ejecutor de pruebas reales, no certificador**.

**NO** usar GitHub Actions, pipelines, VPS, producción, bases remotas, `crecisol_dev`, secretos reales, cuentas externas, compras, ni despliegues. **NO** editar código, cambiar ramas remotas, fusionar ni certificar. Si falta alguna toolchain, registrar `NO EJECUTADO` con razón; no simular, omitir ni inventar resultados. No activar workers de plataforma contra servicios externos.

No generar prueba `.c03-*-ci-proof` antigua ni falsificar `PASS: build` para satisfacer auditores heredados: esos marcadores pertenecen a flujos históricos de CI y **no reemplazan ejecución real**. El nuevo cierre C03-10 utilizará evidencia de runtime independiente.

## 1. Checkout, procedencia y carpeta de evidencia

En un entorno Linux aislado apto para Node 24.15+, Angular 22, SDK .NET 10, Python 3.11+, Docker con MySQL 8.4 fijado por digest y, si procede, Chromium:

```bash
set -Eeuo pipefail
git fetch origin
git switch feature/c03-10-auditoria-integral-autonoma
git pull --ff-only origin feature/c03-10-auditoria-integral-autonoma
test -z "$(git status --porcelain)"
git remote get-url origin
git rev-parse HEAD
python3 --version
node --version
dotnet --version
docker --version
```

Validar que el origen es **exactamente** `solqaryn-platform/CRECISOL` y la rama coincide. No ejecutar si hay cambios ajenos, evidencia de fuente alterada o credenciales de otro proyecto. Guardar el SHA completo de `HEAD` **después** de actualizar el checkout: los SHA previos de esta documentación son base histórica, no SHA inmutable de ejecución.

Evidencias **fuera del repositorio** en carpeta privada con modo `0700`, por ejemplo `$HOME/crecisol-c03-10-evidence/<UTC>-<SHA12>`; ejecutar `umask 077`. Capturar exit codes reales, stdout/stderr de pruebas en archivos `0600`, duración con reloj monotónico si se dispone, SHA-256 de cada log. **No copiar logs que contengan DSN/contraseñas ni subirlos a Git**; entregar al asistente resumen depurado y hashes.

## 2. Auditoría A integral (primero, con comandos reales)

Ejecutar **cada comando individualmente**, conservar exit status y salida. Un fallo detiene la certificación, no se convierte en PASS:

```bash
python3 qa/master_source_guard.py --self-test
python3 qa/c03_local_preflight.py
python3 qa/c03_01_source.py A
python3 qa/c03_02_domain_audits.py A
python3 qa/c03_03_application_audits.py A
python3 qa/c03_04_infrastructure_audits.py A
python3 qa/c03_05_api_audits.py A
python3 qa/c03_06_module_audits.py A
python3 qa/c03_07_outbox_audits.py A
python3 qa/c03_08_local_config_audits.py A
git diff --check
```

Además, comprobar la secuencia y las actas **C03-01..09**, grafos de `ProjectReference`, contratos públicos, Word/PDF y logo exactos, parametrización, separación de dominios, regresiones C01/C02 y ausencia de archivos fuera de scope. Si un auditor heredado interpreta un estado histórico como vigente o exige CI, **reportar el fallo exacto para corrección por el asistente**, sin modificar el código del auditor unilateralmente.

## 3. Auditoría B integral (independiente de A)

Ejecutar controles negativos **individuales** y capturar éxito/fallo real:

```bash
python3 qa/c03_01_source.py B
python3 qa/c03_02_domain_audits.py B
python3 qa/c03_03_application_audits.py B
python3 qa/c03_04_infrastructure_audits.py B
python3 qa/c03_05_api_audits.py B
python3 qa/c03_06_module_audits.py B
python3 qa/c03_07_outbox_audits.py B
python3 qa/c03_08_local_config_audits.py B
python3 -m unittest discover -s qa -p test_c03_09_local_baseline.py -v
python3 -m unittest discover -s qa -p test_c03_09_local_suite_contract.py -v
python3 -m unittest discover -s qa -p test_c03_09_dev_db_tls_probe.py -v
python3 -m unittest discover -s qa -p test_c03_09_word_logo_compat.py -v
python3 qa/c03_09_local_baseline.py --self-test
python3 qa/c03_06_status_audits.py
python3 qa/c03_08_status_audits.py
```

Evaluar negativas de Authorization, `X-Tenant-Id`, forwarded Host, DSN de servidor, LocalRuntimeSettings, contratos sintéticos de outbox, FK compuestas, leases, reintentos, blobs y ZIP adulterados. Ningún test sintético se interpreta como BOLA/IDOR de endpoints privados aún no implementados. **No conectar DEV remota** (`qa/c03_09_dev_db_tls_probe.py --connect-readonly` está prohibido en este encargo).

Comprobar ausencia de nuevos secretos en Git y si existen procesos fuera de permiso. Si un comando falla por estado documental, distinguirlo de un fallo de seguridad real, pero registrarlo con exit code y hallazgo P0/P1/P2; no ocultarlo.

## 4. Auditoría C integral — ejecución real del stack, una ronda

Solo si A/B **sin P0/P1** y el entorno posee dependencias legítimas, imagen MySQL 8.4 ya fijada y Docker aislado. Restaurar dependencias y ejecutar **una única compilación** por proyecto afectado, no repetir la línea base de **nueve** mediciones C03-09 salvo problema nuevo.

```bash
npm ci --prefix frontend --no-audit --no-fund
npm run build --prefix frontend
dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release --nologo -warnaserror
dotnet build backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj -c Release --nologo -warnaserror
dotnet run -c Release --project backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Branding.ApplicationTests/Crecisol.Branding.ApplicationTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Domain.ContractsTests/Crecisol.Domain.ContractsTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Application.ContractsTests/Crecisol.Application.ContractsTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Infrastructure.ContractsTests/Crecisol.Infrastructure.ContractsTests.csproj -- contracts
dotnet run -c Release --project backend/tests/Crecisol.Api.ContractsTests/Crecisol.Api.ContractsTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Api.ModuleTests/Crecisol.Api.ModuleTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.LocalConfig.ContractsTests/Crecisol.LocalConfig.ContractsTests.csproj
dotnet run -c Release --project backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj -- contracts
bash -n database/tests/c03_07_outbox_mysql_smoke.sh
bash -n database/tests/c01_04_api_mysql_smoke.sh
bash database/tests/c03_07_outbox_mysql_smoke.sh
CRECISOL_C03_05_CONTRACT_SMOKE=1 CRECISOL_C03_08_LOCAL_PROFILE_SMOKE=1 bash database/tests/c01_04_api_mysql_smoke.sh
```

**MySQL obligatorio:** el script usa contenedores `--network none`, acceso por socket Unix, imagen `mysql:8.4@sha256:...`, sin puertos publicados, datos sintéticos y borrado final. Verificar que no queden contenedores de pruebas. Si la imagen exacta no está disponible y no puede obtenerse de forma segura, registrar `BLOCKED` y no sustituirla silenciosamente por MySQL 8.0, Latest ni una base remota.

**Playwright/Chromium:** si existe navegador y dependencias compatibles, ejecutar regresiones pertinentes de C01/C02 sobre Angular pública (por ejemplo `cd frontend && npm run test:e2e:ci`) y registrar números exactos. Si no existe el navegador, marcar `NO EJECUTADO`, indicando impacto real según rutas cambiadas (C03-10 solo documentación). No afirmar WCAG manual verificada por tests automáticos.

**No ejecutar** `qa/c03_01_source.py C`, `qa/c03_02_domain_audits.py C` ... `qa/c03_08_local_config_audits.py C` si exigen pruebas `.c03-*-ci-proof` históricas que no están presentes; explicar su dependencia y reemplazarla por salida real de los comandos anteriores, **sin fabricar marcadores**.

## 5. Entrega obligatoria a quien certifica

Devolver un informe sin secretos que contenga:
- Repositorio, rama, SHA exacto, toolchains (Node24, Angular22, .NET10, Docker, digest MySQL8.4), `git status --short` inicial/final.
- **Auditoría A:** comandos y exit codes, mapeo de 9 microfases y fallos documentales.
- **Auditoría B:** comandos y exit codes, negativas reales, hallazgos de seguridad y matriz de regresiones.
- **Auditoría C:** builds y contratos con exit code, MySQL 8.4 (red y cleanup), HTTP públicos, navegador si se usó, tiempos no manipulados.
- Listado de archivos de evidencia, SHA-256 de logs sanitizados, prueba de que la base DEV remota no se utilizó.
- Hallazgos **P0/P1/P2**, correcciones **propuestas sin aplicarlas** y pruebas que habría que repetir.
- Dictamen sugerido `APTA PARA EVALUACIÓN TÉCNICA` o `BLOQUEADA`, **NUNCA «CERTIFICADA» por Codex**.
- No crear PR, no ejecutar Actions, no hacer commits, no integrar `dev`, no avanzar C04.

**Criterio final:** el asistente revisará independientemente el resultado y decidirá si puede certificar **C03-10 y C03 integral**. Sin evidencia real suficiente, estado **NO CERTIFICADA**.
