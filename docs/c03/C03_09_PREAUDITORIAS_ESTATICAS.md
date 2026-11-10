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
