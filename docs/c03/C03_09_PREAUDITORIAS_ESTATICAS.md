# CRECISOL — C03-09 | Registro de preauditorías técnicas A/B/C sin equipo del propietario

**Estado:** PREAUDITORÍAS ESTÁTICAS / PARCIALES, **NO CERTIFICADA**, **NO ACTA**.

**Fecha:** 10-11-2026.

**Repositorio:** `solqaryn-platform/CRECISOL`; rama `feature/c03-09-compat-benchmark-local`.

**Último HEAD de trabajo verificado para esta revisión:** `da5c974d379ac30a666fa70bf1760e3cf66329a7`.

**Base `dev` verificada:** `25130475550cc2993e59d5ae4fc8537be0b47bea`.

**Puerta:** C03-01..08 certificadas históricamente, C03-09 en desarrollo, C03-10 pendiente, C03 no certificado, C04 bloqueado y 2/37 capítulos certificados.

Este documento es registro preparatorio; NO contiene resultados de compilación del repositorio completo. La indisponibilidad temporal de una PC y el bloqueo de GitHub Actions no autorizan inventar métricas ni modificar el criterio de certificación.

## Auditoría A — requisitos y trazabilidad (REVISIÓN ESTÁTICA, no certificación)

**Fuentes revisadas directamente en GitHub:** `AGENTS.md`, Skill local, `docs/MASTER_REFERENCE.md`, `docs/C03_PREPARACION.md`, `docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md`, `docs/c03/C03_08_CERTIFICACION_POSTMERGE.md`, especificación y guía nuevas de C03-09.

- Se confirmó como alcance exclusivo `C03-09` la compatibilidad Angular 22/Node 24, .NET 10 y MySQL 8.4 y el benchmark de línea base repetible.
- El diff GitHub inicial verificado desde `dev`: **seis archivos nuevos exclusivamente en `docs/c03/` y `qa/`**, sin cambios en `frontend/`, `backend/`, `database/`, `main`, Word/PDF o actas previas.
- Se preservan contratos históricos C03-01..08 y los controles documentales maestros; no se finge que estos scripts desarrollan autorización privada, facturación, tenants, endpoints o características comerciales.
- **Pendiente A definitiva:** comprobar íntegramente los hashes y los scripts sobre un checkout completo del SHA exacto, revisar dependencias y adjuntar evidencia firmada por ejecución local auténtica.

## Auditoría B — riesgos y controles negativos (14/14 pruebas de lógica en copia aislada, NO backend)

**Ejecución real en entorno aislado del asistente:** `python3 -m unittest discover ...` sobre una reproducción local de los scripts de C03-09, **14 pruebas pasaron**. No existía un checkout completo privado de CRECISOL, por lo que esta evidencia **no** se presenta como CI ni como pruebas del frontend o backend.

Cobertura ejecutada:
- Versiones no válidas Node 22/25 y .NET 9/preview; paquetes Angular 23 no aprobados.
- Fallo previo a ejecutar builds ante falta de toolchain, prohibición de `--run` sin `--confirm-local`.
- Variables sensibles y `DOCKER_HOST` remoto excluidos del ambiente heredado.
- Comandos no permitidos rechazados antes de iniciar proceso.
- Salida de error solo con digest SHA-256, sin imprimir texto sensible.
- Timeout de un proceso Bash con hijos, abortado por grupo de procesos.
- Simulación de ruta de medición y error que nunca presenta certificado.
- Muestras estadísticas, reporte de bloqueo JSON y autoevaluación sin marcadores de CI.

**Pendiente B definitiva:** auditoría de comportamiento sobre herramientas reales, revisión de cadena de dependencias y ejecución de casos adversariales con Docker/HTTP y MySQL del repositorio auténtico.

## Auditoría C — arquitectura y rendimiento (ESTÁTICA PARCIAL, runtime NO EJECUTADO)

**Ejecutado localmente en copias aisladas:** `python3 -m compileall` (PASS), `bash -n qa/c03_09_local_suite.sh` (PASS), `--self-test` (PASS), 14 tests de runner (PASS). Se realizaron pruebas sintéticas de `qa/c03_09_local_suite.sh` con un repositorio Git ficticio para validar su orquestación: `--probe` y `--run` devolvieron estado 2, `manifest.json` tuvo `phase_certified=false` y C constó como `NOT_EXECUTED` / `BLOCKED_MISSING_LOCAL_TOOLCHAIN`. Los pasos A de ese fixture eran deliberadamente simulados; **no** acreditan el maestro real.

Hallazgos detectados y corregidos antes de esta constancia:
1. El timeout anterior podía dejar procesos secundarios abiertos. Ahora el runner usa grupo de procesos y lo termina expresamente en Linux/WSL2; stderr se guarda temporalmente y solo se identifica por digest en errores.
2. El lanzador `--probe` anterior podía devolver código 0 aun faltando herramientas. Ahora devuelve **2** cuando no está listo.
3. Se aumentó la cobertura del runner de 9 a **14** pruebas.
4. Se documentó que `NPM_CONFIG_OFFLINE` no equivale a un cortafuegos de red; `--network none` corresponde únicamente al contenedor MySQL.

**NO EJECUTADO:** build Angular 22 con Node 24, build API .NET 10, pruebas MySQL 8.4/HTTP, mediciones reales de tiempos, revisión PowerShell bajo Windows/WSL2, test de regresión integral C03 y cualquier CI posterior. Este entorno carece de los tres toolchains/artefactos requeridos y no dispone de checkout privado completo. **Auditoría C integral: PENDIENTE.**

## Riesgos abiertos y condiciones de continuidad

- **P0 de evidencia:** no existe benchmark real ni validación del stack completo, por lo que queda prohibida la inscripción C03-09 CERTIFICADA y el inicio de C03-10 o C04.
- **Costos:** ninguna herramienta se instala, ningún runner GitHub se invoca por estos cambios, no se autoriza gasto ni ejecución productiva.
- **Reversión:** la rama puede descartarse o integrarse mediante PR únicamente cuando el gobierno y evidencias lo permitan; no se han editado datos ni migraciones.
- **Siguiente paso técnico:** ejecutar desde una PC preparada `bash qa/c03_09_local_suite.sh --probe` y luego `--run`, sobre el SHA real y un checkout limpio. Conservar manifiesto SHA-256, muestras y resultados; resolver fallos y completar A/B/C antes de cualquier certificación.

**Conclusión honesta:** las preauditorías estáticas y las 14 pruebas de control del ejecutor evidencian preparación y correcciones; **no** prueban aún que CRECISOL compile o que su rendimiento y MySQL sean correctos.

## Segunda ronda de preauditoría — fortalecimiento del mismo C03-09

**A — Trazabilidad complementaria (revisión estática; no cierre):**
- Verificados en GitHub el alcance actual de `dev` y de `feature/c03-09-compat-benchmark-local`, contratos de `qa/c03_07_outbox_audits.py` y `qa/c03_08_local_config_audits.py` y las modalidades A/B/C. Los comandos **A** y **B** de ambas microfases certificadas son estáticos; `C` exige sus pruebas de ejecución históricas y no se simula.
- El ejecutor añade explícitamente cuatro verificaciones **A** y cinco verificaciones **B**, y rechaza `main`, otro origen Git, árboles sucios y un SHA no válido. Esto prepara la regresión documental y funcional, sin reabrir certificaciones previas.

**B — Seguridad del ejecutor (20/20 pruebas en copias aisladas):**
- `qa/test_c03_09_local_baseline.py`: **15/15** pruebas de Python positivas/negativas pasadas sobre una reproducción del código. Se añadió limpieza de grupos de procesos por `KeyboardInterrupt` además de timeout y redacción de logs de error.
- `qa/test_c03_09_local_suite_contract.py`: **5/5** contratos sintéticos pasados en Linux: rechazo de rama `main`, rechazo de origen ajeno, preflight con nueve controles, `--run` bloqueado al faltar herramientas y fallo B simulado que bloquea C sin certificación.
- El script Bash del lanzador y su test de contrato se verificaron además por **Git blob SHA-1 idéntico** a la rama: `74b5842e10dd73ce2943b4df47cd44cdbc157ece` y `6d1ef2f76ea3e2807b888a1a42a8eb6f5534dec4`. Las otras pruebas se ejecutaron en copias aisladas y **no** deben presentarse como un checkout completo autorizado de CRECISOL.

**C — Arquitectura/compatibilidad (revisión estática PARCIAL):**
- `bash -n` y `python3 -m py_compile` de los scripts locales: PASS. Los fixtures `--probe` y `--run` retornaron bloqueo predecible y `phase_certified=false` cuando faltaba el stack; se rechazó rama incorrecta **antes** de crear evidencia.
- **NO EJECUTADO:** `.NET 10`, `Node 24 + Angular 22`, `MySQL 8.4`, pruebas integrales de aislamiento ni métricas basales reales. La auditoría C integral sigue pendiente, así como acta, compilación, revisión humana y certificación.

**Riesgos remanentes y cierre:** las pruebas de orquestación sintéticas no validan las pruebas históricas reales; las declaraciones A/B definitivas requieren ejecución del checkout completo y la tercera auditoría exige toolchains reales. Estado: **C03-09 EN DESARROLLO, NO CERTIFICADA; C03-10 PENDIENTE; C04 BLOQUEADO.** El bloqueo externo de GitHub Actions no impide estas preparaciones pero nunca se registra como CI satisfactoria.

## Tercera ronda: acceso DEV remoto comunicado, sin tocar la base

**Entrada del propietario (no verificada):** base de desarrollo `crecisol_dev` aparentemente vacía, en un servidor remoto, TCP 3306 y `SSL REQUIRED`; password no suministrada. No se registró host/IP ni cuenta de aplicación en el código o documentación. No se ha comprobado disponibilidad, versión `8.4.x`, vacío, permisos, identidad TLS ni red. Aunque el entorno sea DEV, no se presupone autorización para ensayos destructivos en una instancia persistente.

**Auditoría A — trazabilidad (REVISIÓN DOCUMENTAL, PARCIAL):** C03-09 incorpora [C03_09_BASE_DEV_REMOTA_SEGURA.md](C03_09_BASE_DEV_REMOTA_SEGURA.md), `qa/c03_09_dev_db_tls_probe.py` y test independiente; sin cambiar Word/PDF, modelos de tenant, DDL, clientes Angular o API. La base remota se trata como recurso externo de lectura opcional, no sustituto de la base MySQL desechable para compatibilidad y regresiones.

**Auditoría B — seguridad (13/13 CONTROLES OFFLINE PASS, no conexión):** pruebas ejecutadas con CPython en sandbox: plan sin conexión, aceptación explícita, opciones `VERIFY_IDENTITY`, ausencia de contraseña en argv/entorno, CA real por ruta absoluta fuera de Git, rechazo de base/version/cipher erróneos, resultados contaminados, SQL fijo solo `SELECT`/`SHOW`, errores privados y bandera `phase_certified=false`. Se repitieron **5/5** contratos de orquestación sintéticos del lanzador tras integrar el sexto control B. Total conjunto nuevo registrado: **33** (15 + 5 + 13). No ejecuta autenticación ni consultas reales.

**Auditoría C — compatibilidad e infraestructura (PENDIENTE):** comandos de Python compilaron y tests sin red aprobaron, pero el entorno del asistente carece de MySQL 8.4 cliente/servidor, Node 24, .NET 10, CA y acceso autenticado. No se efectuaron compilaciones del producto, MySQL remota ni pruebas de rendimiento. El acceso remoto opt-in requerirá cert SAN/hostname, CA fuera de repo, privilegio de solo lectura, red restringida y consentimiento explícito.

**Hallazgo documental adicional:** `docs/MASTER_REFERENCE.md` conserva una línea que trata PR #73 como candidato en borrador aunque su integración en `dev` fue verificada. Se anota inconsistencia de estado para remediación documental aislada; no se edita aquí para no alterar controles históricos de C01/C02/C03-01..08 sin regresiones completas.

**Estado formal sin cambios:** C03-09 **EN DESARROLLO / NO CERTIFICADA**; C03-10 PENDIENTE; C03 NO CERTIFICADO; C04 BLOQUEADO; `dev`, `main` y el servidor remoto sin cambios. No se generó evidencia falsa de CI.


## Verificación externa comunicada por el propietario y remediación del wrapper (C03-09)

**Declaración recibida:** el propietario informó un checkout limpio en `feature/c03-09-compat-benchmark-local`, SHA exacto `01fbb85bf202615c172eb043822decb733af2e3b`; `git fetch`, `git switch` y `wsl --status` correctos con Ubuntu 2. El `Probe` de PowerShell arrancó, pero el `Run` falló por conversión de ruta vía `wslpath`. Se ejecutó desde WSL2 la suite Bash, con **A PASS; B PASS; C BLOCKED_MISSING_LOCAL_TOOLCHAIN; phase_certified false**. El propietario conservó la evidencia fuera del repositorio bajo una carpeta local con fecha y SHA.

**Grado de evidencia:** reporte humano recibido, coherente con las condiciones de ejecución. No se ha recibido ni verificado de forma independiente su `manifest.json`, logs y hashes; por ello se **registra como ejecución declarada, no como certificación auditada**.

**Corrección revisada:** `qa/c03_09_windows.ps1` elimina `wslpath` y utiliza `wsl.exe --cd` con ruta absoluta Windows o conversión UNC de WSL sin procesos externos. Propaga el código de salida y no instala herramientas. Se añadieron cuatro tests estáticos al conjunto de contratos del lanzador, que pasa de cinco a nueve escenarios de código. No se han ejecutado pruebas de PowerShell reales en esta sesión.

**Pendiente:** validar nuevamente el lanzador en Windows, disponer de Node 24.15+, .NET 10, Docker Engine Linux y MySQL 8.4 para C. Las 15 pruebas de Python, 13 de seguridad DEV, cinco sintéticas originales del lanzador y cuatro comprobaciones estáticas nuevas no sustituyen compilaciones ni base real. C03-09 permanece **NO CERTIFICADA**; C03-10 y C04 sin habilitación.


## Incidencia de prebuild C01-06 por nueva edición del Word — revisión sin ejecutar `Run`

**Evidencia comunicada por el propietario (no descargada de su equipo):** durante `--run` en el commit `b8d1f1286a63b37807ac9000acac50eeee7d9e96`, A/B y build incremental API .NET pasaron; `angular22_production_incremental_build` falló antes de compilar por `FAIL C01-06: Canonical Word SHA mismatch`. El manifiesto registró C=`FAILED_RUNTIME` y `phase_certified=false`; el checkout local quedó sin cambios. El propietario pidió **no ejecutar de nuevo `Run` hasta investigar el Word**; se respetó.

**Causa verificada en código de GitHub:** `frontend/package.json` llama durante `prebuild` a `qa/c01_06_official_assets.py --write`. Ese generador todavía usaba `candidate(WORD, EXPECTED)`, y `EXPECTED` es el SHA **histórico** `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. En contraste, `qa/master_source_guard.py` declara la edición comercial **actual** con SHA `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`. Por tanto, el generador fallaba legítimamente al comparar valores de ediciones diferentes.

**Contraste independiente de los binarios, sin modificación:** se examinaron copias de ambos Word y PDF presentes en el entorno de revisión. SHA-256 de Word/PDF y blob Git del Word coinciden con las huellas del `docs/MASTER_REFERENCE.md` y la rama `dev`. La imagen `word/media/image1.png` de ambos Word tiene el **mismo** SHA aprobado `9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f` y la relación interna `rId11` apunta a `media/image1.png` en ambos. La edición vigente conserva el fingerprint de los primeros 1.876 elementos OOXML `6238a81010d8bfd035eda77fa33897a009f4b363d4e281723c723b6b60c807d5`, con 1.687 párrafos y 189 tablas históricos. **No se modificó ningún Word, PDF, logo ni acta certificada.**

**Corrección mínima en `qa/c01_06_official_assets.py`:** antes de extraer se invoca `canonical_active_word()`, que valida la edición actual y el prefijo histórico; después se ejecuta `candidate(WORD, CURRENT_WORD_SHA)`. La constante histórica `EXPECTED` de `qa/c01_06_logo_candidate.py` queda **intacta** para las auditorías antiguas, y `APPROVED_SHA` sigue rechazando cualquier imagen no autorizada. No se aceptan hashes arbitrarios ni se crea una lista permisiva de ediciones.

**Regresiones añadidas:** `qa/test_c03_09_word_logo_compat.py` incorpora 8 pruebas: pines histórico/actual, orden de guardia y extracción, fallos de guardia, imagen modificada, relación distinta, DOCX sintético con SHA incorrecto, relación externa y verificación positiva **de solo lectura** del Word/PDF/logotipo reales. El lanzador `qa/c03_09_local_suite.sh` ejecutará esta batería en B **antes de iniciar cualquier benchmark C**, y sus contratos sintéticos se adaptan a once comprobaciones A/B.

**Auditorías A/B/C de esta corrección:**
- **A — trazabilidad:** causa vinculada a PR #73, Word/PDF actual e histórico, prebuild y C01-06; huellas de binarios reales y registro de dos versiones corroborados, sin reescribir la certificación original.
- **B — seguridad:** la corrección conserva fail-closed ante Word incorrecto, archivo ZIP/OOXML alterado, logo cambiado o ruta externa. Los casos negativos han sido **codificados**, pero la nueva batería completa en el checkout Git aún NO ha sido ejecutada en el equipo del propietario.
- **C — arquitectura y compatibilidad:** no hay cambio en Angular, .NET ni MySQL, ni en la lógica del logo recortado; la compilación Angular posterior a esta corrección, el benchmark real y el `Run` quedan **NO EJECUTADOS**, por instrucción del propietario. No se presenta el análisis de bytes como compilación exitosa.

**Prueba de comprobación controlada permitida para más adelante (NO `Run`):**
`python3 -m unittest discover -s qa -p test_c03_09_word_logo_compat.py -v`, previa actualización del checkout limpio. El archivo prueba la procedencia y extracción en memoria **sin escribir assets**, sin levantar API, Docker ni usar la base DEV remota. **No ejecutar nuevamente `Run` hasta revisar su salida y autorizar el siguiente paso.**

**Estado:** C03-09 **EN DESARROLLO / NO CERTIFICADA**; C03-10 PENDIENTE; C03 NO CERTIFICADO; C04 BLOQUEADO. Las auditorías históricas permanecen inmutables.
