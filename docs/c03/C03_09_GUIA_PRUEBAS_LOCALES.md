# CRECISOL — C03-09 | Pruebas locales reales en Windows / WSL2 (sin GitHub Actions)

**Estado:** Preparado para ejecución local en el equipo del propietario; **NO CERTIFICADO**. Solo CRECISOL, rama de trabajo `feature/c03-09-compat-benchmark-local`. C03-01..08 conservadas como certificadas, C03-10 pendiente, C04 bloqueado.

Esta guía no configura facturación, hosting, nube ni producción. Los scripts **no instalan** .NET, Node, paquetes, imágenes Docker ni herramientas de terceros; no realizan `npm ci`, `dotnet restore`, `docker pull`, pagos ni despliegues de manera automática. El propietario decide si prepara previamente esas dependencias en su PC.

## 1. Preparación del equipo (una sola vez, realizada por el propietario)

**Plataforma recomendada:** Windows 11 con WSL2 Ubuntu; Docker Desktop con integración explícita en la distribución WSL2. Usar la carpeta **Linux** `~/src/CRECISOL` para mediciones más consistentes; `/mnt/c/...` también es posible pero puede alterar los tiempos.

Herramientas necesarias **dentro de WSL2 Ubuntu**:

- Git y Python 3.11 o posterior.
- Node **24.15.0 o superior dentro de la rama mayor 24** y npm. Node 22/25 se rechaza.
- SDK .NET **10.0.x**. Ejecutables `dotnet` disponibles en WSL2; no basta instalar .NET solo en Windows.
- Bash, Docker CLI y motor Docker local mediante `/var/run/docker.sock` (Docker Desktop + WSL2 o motor Linux local).
- Imagen **MySQL 8.4 LTS** fijada con el digest exacto usado por los tests de CRECISOL.
- Credenciales de desarrollo privadas y paquetes locales, **nunca** contraseñas de producción.

No usar permisos de administrador para ejecutar las pruebas. Si falta una herramienta, detenerse; su instalación inicial es manual y fuera del alcance automático de estos scripts.

### Consultar las versiones dentro de Ubuntu

```bash
python3 --version
node --version
dotnet --list-sdks
docker info
```

### Obtener el código en Linux/WSL2

Si no existe un clon local limpio de CRECISOL:

```bash
mkdir -p ~/src
cd ~/src
git clone --branch feature/c03-09-compat-benchmark-local https://github.com/solqaryn-platform/CRECISOL.git
cd CRECISOL
```

Para un clon existente, usar un árbol limpio y cambiar de rama **sin borrar cambios locales**:

```bash
git status --short
git fetch origin
git switch feature/c03-09-compat-benchmark-local
git pull --ff-only
```

**Nunca trabajar en el repositorio SOLQARYN ni alterar `main`.** En caso de cambios locales sin confirmar, guardarlos mediante un procedimiento propio de Git, no descartarlos.

## 2. Preparar las dependencias cacheadas (manual, solo si faltan)

**Estos comandos pueden descargar dependencias e imagen desde sus proveedores, por lo que se muestran como preparación opcional que el propietario decide ejecutar una sola vez. El runner de pruebas no los ejecuta.**

```bash
cd ~/src/CRECISOL
cd frontend && npm ci --no-audit --no-fund && cd ..
dotnet restore backend/src/Crecisol.Api/Crecisol.Api.csproj
dotnet restore backend/tests/Crecisol.Outbox.ContractsTests/Crecisol.Outbox.ContractsTests.csproj
docker pull 'mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
```

Estos comandos usan archivos de proyecto y `package-lock.json` existentes, pero cualquier descarga de paquetes o uso del motor Docker depende de la configuración del equipo. No utilizar imágenes no fijadas, registros privados desconocidos ni secretos de producción.

## 3. Primera ejecución: diagnóstico seguro y pruebas estáticas A/B

En el terminal de Ubuntu:

```bash
cd ~/src/CRECISOL
bash qa/c03_09_local_suite.sh --probe
```

El lanzador ejecuta **cuatro comprobaciones A** (maestro, preflight histórico y auditorías de código de C03-07/C03-08) y **cinco comprobaciones B** (15 pruebas del runner, 5 contratos sintéticos de orquestación, auditorías negativas C03-07/C03-08 y autoprueba fail-closed). Después detecta herramientas, SDKs, dependencias locales e imagen MySQL disponible.

- Si faltan herramientas, informa `BLOCKED_MISSING_LOCAL_TOOLCHAIN`, devuelve código **2** y deja C como **NOT_EXECUTED**; no inventa una medición.
- La carpeta de evidencia se crea por defecto en `~/crecisol-local-evidence/c03-09-FECHA-SHA/`, **fuera del repositorio** y con permisos de usuario.
- `manifest.json` registra commit exacto, auditoría A documental, B de negativos, C de compatibilidad y huellas SHA-256 de cada archivo generado.

Alternativa desde **PowerShell**, si el proyecto está ubicado en una ruta Windows accesible desde WSL2:

```powershell
.\qa\c03_09_windows.ps1 -Mode Probe
```

Si la política de PowerShell impide ejecutar scripts no firmados, usar el comando Bash anterior desde WSL2 en lugar de desactivar controles del sistema.

## 4. Segunda ejecución: pruebas reales y benchmark basal C

**Solo después de que `probe.json` marque `ready: true`**:

```bash
bash qa/c03_09_local_suite.sh --run
```

O desde PowerShell, con el mismo requisito:

```powershell
.\qa\c03_09_windows.ps1 -Mode Run
```

El runner requiere aceptación explícita `--run` y valida requisitos antes de construir o levantar MySQL. No intenta descargar nada por su cuenta.

Si la máquina está preparada, ejecuta:

1. Compilación real .NET 10 Release de `Crecisol.Api`, **3 rondas** con `--no-restore`.
2. Compilación real Angular 22 con Node 24, `npm run build` y assets de marca existentes, **3 rondas**.
3. Compilación real .NET 10 de los contratos Outbox, **3 rondas**.
4. Prueba de integración MySQL 8.4 sobre Docker efímero **`--network none`** mediante el smoke local existente, con datos sintéticos y sin puertos públicos.

Reporta tiempos por muestra, mediana y p95 nearest-rank de *compilación*, y duración del smoke MySQL. **No son percentiles p95 HTTP/SQL ni indicadores de producción.**

## 5. Evidencias, auditorías y estado

| Bloque | Evidencia local | Naturaleza |
| --- | --- | --- |
| A | `A_master_integrity.txt`, `A_historical_static.txt` | Trazabilidad e integridad documental; no certifica arquitectura integral |
| B | `B_negative_unittests.txt`, `B_orchestrator_contracts.txt`, `B_c03_07_negative.txt`, `B_c03_08_negative.txt`, `B_guard_selftest.txt` | 15 tests del runner, 5 escenarios sintéticos del lanzador y regresiones B históricas; no sustituyen pruebas de backend |
| C | `probe.json`, `C_runtime_benchmark.json` si se ejecutó | Compilaciones y MySQL reales, o motivo explícito `BLOCKED` |
| Control | `manifest.json`, `source.txt` | Commit, hora, estados, hashes de evidencia e indicación `phase_certified=false` |

**Estados:** `PASS` A/B solo para controles estáticos; `EXECUTED` C solo con mediciones reales verificadas; `NOT_EXECUTED`/`BLOCKED` si falta evidencia. El script devuelve código 2 cuando C no puede ejecutarse y jamás transforma esa situación en éxito.

La carpeta de evidencia es privada. Compartir únicamente `manifest.json` y `probe.json` después de revisar que no contienen detalles privados; si C se completó, añadir `C_runtime_benchmark.json`. Los logs extensos se conservan localmente y se comparten solo si es necesario y tras inspección.

## 6. Límites innegociables

- No tocar el Maestro ni las actas certificadas, ni modificar `frontend/`, `backend/` o `database/` en esta tarea; las únicas incorporaciones son instrumentación y documentación C03-09.
- No borrar pruebas históricas ni crear marcadores artificiales de CI o POSTMERGE.
- No ejecutar servicios, migraciones o pagos de producción; no utilizar el entorno ni el código de SOLQARYN.
- **Los resultados locales no sustituyen la CI posterior al PR #73.** El bloqueo de ejecución de esa CI continúa como antecedente, pero no impide construir y probar de forma independiente.
- No declarar **C03-09 CERTIFICADA**, **C03 CERTIFICADO** ni habilitar C04 sin haber realizado A/B/C de microfase, revisión humana y evidencia real conforme al gobierno del proyecto.

**Siguiente paso operativo:** ejecutar primero `--probe` en la PC del propietario, leer `probe.json`, preparar manualmente los prerrequisitos que realmente falten y luego ejecutar `--run`. No se requiere adquirir GitHub Pro ni habilitar gastos de Actions.

**Alcance de aislamiento:** `NPM_CONFIG_OFFLINE=true` impide ciertas solicitudes del gestor npm, pero por sí solo no establece un cortafuegos para scripts de compilación o extensiones. `--network none` aplica exclusivamente al contenedor desechable MySQL; ejecutar las herramientas locales con dependencias verificadas y sin secretos ni datos productivos.

### Protección de rama y pruebas del lanzador

El script de WSL2 solo admite `feature/c03-09-compat-benchmark-local` o `dev`, con origen `solqaryn-platform/CRECISOL` y árbol limpio. `main`, repositorios ajenos y árboles modificados se rechazan con código 2 **antes** de ejecutar pruebas. El propio ejecutor comprueba estos rechazos usando cinco fixtures Git temporales; sus datos son sintéticos y no incluyen credenciales reales.

Para probar los controles sin ejecutar builds, con Python/Bash/Git disponibles, puedes ejecutar opcionalmente:

```bash
python3 -m unittest discover -s qa -p test_c03_09_local_baseline.py -v
python3 -m unittest discover -s qa -p test_c03_09_local_suite_contract.py -v
```

No equivalen a una auditoría C aprobada: C solo podrá completarse cuando `--run` ejecute Angular/.NET/MySQL reales. Toda evidencia indica `phase_certified=false` hasta la revisión del equipo y acta correspondiente.
