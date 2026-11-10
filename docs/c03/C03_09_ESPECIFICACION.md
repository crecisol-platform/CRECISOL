# CRECISOL — C03-09 | Compatibilidad real y rendimiento basal reproducible

**Estado:** EN DESARROLLO TÉCNICO / **NO CERTIFICADA**. C03-01 a C03-08 certificadas (8/10); **C03-09 abierta**, C03-10 pendiente, C03 no certificado, C04 bloqueado y 2/37 capítulos certificados.

**Repositorio y autoridad:** `solqaryn-platform/CRECISOL`; base de inicio `dev` commit `25130475550cc2993e59d5ae4fc8537be0b47bea` (PR documental #73 integrado). Documento Maestro Único DOCX actual en `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`. Referencias históricas originales conservadas en `docs/MASTER_REFERENCE.md`; los 8 pasos normativos MAE-C03 no equivalen a las 10 microfases técnicas. Fuente de planificación: `docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md`.

## Propósito acotado

Medir compatibilidad **real** de Angular 22 con Node 24, .NET 10 y MySQL 8.4, junto con una línea base de **builds reproducibles** y una regresión local de MySQL desechable. La instrumentación debe distinguir muestras de build incremental y tiempo de prueba funcional con arranque de contenedor: **no son latencia HTTP ni p95 SQL**. No se declara compatibilidad por el simple inventario documental.

El cierre técnico requiere los tres controles A/B/C con evidencia efectiva y repetible. Si falta el runtime/imagen, declarar `BLOCKED_MISSING_LOCAL_TOOLCHAIN`; nunca generar un reporte PASS de rendimiento ni fabricar marcadores de CI. El bloqueo externo de GitHub Actions **no impide escribir código y ejecutar pruebas locales**, pero su estado se conserva como limitación documental; esta microfase no elimina ni desactiva workflows existentes.

## Corte de implementación

1. `qa/c03_09_local_baseline.py`: runner CPython estándar, sin dependencias adicionales; modos `--probe` (solo inspección), `--self-test` (guardas sintéticas, no compatibilidad real), `--run --confirm-local` (solo toolchains validadas). No modifica ajustes de facturación ni usa GitHub Actions.
2. Guardas de compatibilidad: Node `24.15+` (nunca 22 ni 25), SDK `.NET 10.0.x`, paquetes Angular 22 en `frontend/package.json`, `node_modules` preparados localmente, proyectos .NET restaurados **previamente**, Docker local por socket Linux, imagen MySQL 8.4 fijada por digest y precargada en caché. Rechazar ausencia de cualquiera **antes** de ejecutar benchmarks.
3. Medidas de tiempo con `perf_counter()` monotónico; 3, 4 o 5 iteraciones consecutivas de build incremental para: `Crecisol.Api` .NET 10 Release (warnings as errors), Angular production y contratos `Crecisol.Outbox.ContractsTests` .NET 10. `median`, `p95 nearest-rank` y muestras explícitas, con etiqueta de hardware/ambiente; **la primera iteración no se denomina cold build ni sustituto de un perfil limpio**.
4. MySQL 8.4 mediante `database/tests/c03_07_outbox_mysql_smoke.sh`: contenedor efímero `--network none` con socket Unix y pruebas sintéticas. Registrar **duración total de prueba**, NO TPS/latencia de queries, ni conexiones remotas. Usar únicamente la imagen cacheada; el runner no hará `docker pull`, `npm install`, `dotnet restore` ni descargas.
5. Proceso de build con `subprocess.Popen` + lista de argumentos (sin `shell=True`), entorno filtrado por lista permitida y contenedor MySQL con red deshabilitada. Los programas pueden leer configuración local del equipo: no usar credenciales productivas. Se descarta stdout, stderr queda en archivo temporal privado y solo se reporta su hash ante fallo. Matar el grupo de procesos tras timeout o `KeyboardInterrupt`; fallar cerradamente ante errores.
6. Reporte JSON a stdout con `status`, `environment.checks`, `metrics`, `generated_utc`, `real_compatibility_verified` y `scope`. Falta de herramientas: métricas vacías y flag `false`. El informe se puede redirigir manualmente a archivo local fuera del repositorio, nunca se suben secretos o datos privados.

## Pruebas y auditorías obligatorias

- **A (trazabilidad):** C03-08 antecedente certificado, rutas de los proyectos, DOCX vigente y plan C03; ninguna modificación de C01-C03-08 ni del maestro. Mapeo explícito de scripts a evidencia positiva.
- **B (seguridad):** pruebas negativas de Node 22/25, .NET 9, falta de cache local de MySQL, imagen no disponible, ejecución `--run` sin `--confirm-local`, timeout y error de subprocess; no ejecución remota, sin credenciales, sin SQL de producción ni falsos PASS.
- **C (compatibilidad y rendimiento):** compilación Angular 22/Node 24 real, compilación .NET 10 Release, pruebas MySQL 8.4 sobre contenedor `--network none`, muestras de rendimiento 3+ por build y regresiones pertinentes. **No marcar PASS si cualquiera no se ha ejecutado**.

### Evidencia realmente obtenida en la sesión de inicio

Runner Python de C03-09, probado en un **fixture aislado**:
- `python3 -m py_compile`: PASS.
- `python3 qa/c03_09_local_baseline.py --self-test`: PASS, versión Node y .NET, percentil, mediana y comprobación de fail-closed.
- `--run` sin `--confirm-local`: salida 2, rechazada (PASS de control negativo).
- `--probe` y `--run --confirm-local` en el fixture: `BLOCKED_MISSING_LOCAL_TOOLCHAIN` y salida 2, `metrics={}`, `real_compatibility_verified=false`. Este entorno ofrece Node 22.16.0 y npm 10.9.2 pero **no** SDK .NET 10, Node 24 ni Docker/MySQL 8.4; tampoco contiene checkout completo de CRECISOL. No se atribuye una compilación o benchmark real al fixture.

Esta evidencia prueba únicamente que el runner rechaza estados no válidos. **A/B/C de microfase, builds y benchmark real: PENDIENTES**, no certificados.

## Impacto, seguridad y reversión

- `frontend/`: **ningún cambio**; solamente futuro comando de build de la aplicación pública existente.
- `backend/`: **ningún cambio**; únicamente build de API pública y contratos Outbox existentes; no se crean nuevos endpoints, entitlements, DI privada, worker operativo o auth.
- `database/`: **ningún cambio**; smoke efímero existente, sin datos reales, DDL de producción ni servicios externos.
- `qa/`: instrumentación nueva y pruebas negativas de instrumentación. `docs/c03/`: especificación propia; acta solo cuando haya evidencia real. C01/C02/C03-01..08 y sus SHAs quedan inalterados.
- **Prohibido:** escribir o consultar en modo escritura el repositorio SOLQARYN, ejecutar pagos o desplegar, instalar/activar conectores o alterar workflows para esquivar bloqueos de facturación. No se inicia C03-10 ni C04.
- Reversión: cerrar PR de C03-09 sin merge o revertir su commit exclusivamente en `dev`; sin migraciones ni datos persistentes.

**Condición de salida:** COMPATIBILIDAD REAL + METRICAS REALMENTE MEDIDAS + A/B/C de C03-09 + ausencia de P0/P1 + revisión del diff. Un preflight sintético por sí solo no autoriza certificado ni avanzar C03-10.

## Paquete operativo de ejecución local — sin Actions

- `qa/c03_09_local_baseline.py`: versión de herramientas, compatibilidad, tiempos repetidos de .NET/Angular, smoke MySQL 8.4 aislado, estado fail-closed.
- `qa/test_c03_09_local_baseline.py`: quince pruebas de lógica del runner (incluyendo interrupciones y timeouts); `qa/test_c03_09_local_suite_contract.py`: cinco contratos del lanzador sobre un Git sintético, sin simulaciones atribuidas a CRECISOL.
- `qa/c03_09_local_suite.sh`: un solo comando desde WSL2 para preauditoría A (integridad y antecedentes), B (negativos) y C (compilación y MySQL reales). Guarda SHA de commit y SHA-256 de evidencias en `manifest.json` fuera del repositorio; `phase_certified=false`.
- `qa/c03_09_windows.ps1`: lanzador opcional Windows→WSL2, no instala dependencias ni modifica la configuración local del sistema.
- `docs/c03/C03_09_GUIA_PRUEBAS_LOCALES.md`: instrucciones de preparación manual, diagnóstico, ejecución, conservación y revisión de evidencias.

**Evidencia verificada en el entorno del asistente:** se ejecutaron inicialmente 9 pruebas unitarias de control y, tras reforzar la gestión de procesos y el rechazo de órdenes no permitidas, **15/15 pruebas del runner y 5/5 pruebas del lanzador pasaron** en copias aisladas, sin toolchains de aplicación; `python3 -m compileall` y `bash -n` también PASS sobre los ficheros reproducidos localmente; esa evidencia no procede de una clonación integral autenticada del repositorio. La sonda detectó Node 22.16.0, ausencia de .NET y Docker, por lo que devolvió correctamente `BLOCKED_MISSING_LOCAL_TOOLCHAIN`, sin métricas ni evidencia de compatibilidad real. La comprobación sintética del ejecutor detectó A fallida en un fixture incompleto, B satisfactoria y C bloqueada; no se confunde con un test del checkout completo. Se verificó en un **repositorio sintético** el control de flujo del ejecutor Bash: `--probe` y `--run` devuelven código 2 al faltar herramientas; `manifest.json` registra `phase_certified=false` y C queda `NOT_EXECUTED` o `BLOCKED_MISSING_LOCAL_TOOLCHAIN`. Las comprobaciones A simuladas en ese fixture no acreditan el maestro real. **PowerShell/WSL2 y builds reales en la PC del propietario: NO EJECUTADOS todavía.**

El estado de `dev` permanece C03-09 pendiente hasta integrar y aprobar la evidencia de la microfase conforme a gobernanza. Ninguna prueba de esta sección reemplaza registros POSTMERGE ni certificaciones anteriores.

## Endurecimiento adicional de regresión — misma microfase

En la rama de trabajo, el lanzador exige identidad del repositorio, rama `feature/c03-09-compat-benchmark-local` o `dev`, árbol limpio y SHA de commit antes de cualquier prueba. Suma **cuatro controles A** (maestro, preflight histórico y auditorías A de C03-07 y C03-08) y **cinco controles B** (15 pruebas del runner, 5 contratos sintéticos del lanzador, auditorías B de C03-07 y C03-08, autoevaluación). Los scripts históricos no se modificaron ni se consideraron certificados de nuevo.

Pruebas adicionales ejecutadas **en reproducciones locales de los scripts**: 15/15 del runner y 5/5 del lanzador, y comprobación de salida 2 ante rama errónea, origen falso o herramienta faltante; fallo B simulado bloquea C, sin manifiesto certificado. Los scripts del lanzador y sus cinco contratos también fueron contrastados por identificador de blob Git con la rama de trabajo. No existen compilaciones verificadas de Angular 22, .NET 10 ni MySQL 8.4 en esta sesión. **C03-09 continúa NO CERTIFICADA.**
