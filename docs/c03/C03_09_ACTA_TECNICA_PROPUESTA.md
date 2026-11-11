# CRECISOL — C03-09 | Acta técnica PROPUESTA — NO CERTIFICADA

**Estado documental:** PROPUESTA PARA REVISIÓN HUMANA. **NO CERTIFICADA**. No es un acta de certificación POSTMERGE ni una aprobación del propietario.

**Microfase:** C03-09 de C03, compatibilidad real y rendimiento basal reproducible. **Estado de capítulos:** C01 y C02 certificados (2/37); C03-01..08 certificadas (8/10); **C03-09 pendiente de certificación**, C03-10 pendiente, C03 no certificado y C04 bloqueado.

**Repositorio exclusivo:** `solqaryn-platform/CRECISOL`.

**SHA técnico al que corresponde la evidencia local:** `53cc2134eca8b33c808bcd5d7643684b51080fa8`.

**Base `dev` verificada para el análisis:** `25130475550cc2993e59d5ae4fc8537be0b47bea`.

**Fecha de ejecución registrada en el nombre de la evidencia:** `2026-11-11T04:26:39Z` (el nombre de carpeta no sustituye el timestamp interno del manifiesto).

## 1. Fuente, procedencia y grado de verificación

El propietario presentó un **informe técnico redactado por Codex**, que tiene acceso a su equipo local. Codex comunica haber inspeccionado el paquete almacenado **fuera de Git** en:

`~/crecisol-local-evidence/c03-09-20261111T042639Z-53cc2134eca8/`

Declaraciones del informe de Codex:
- `manifest.json`, `probe.json`, `C_runtime_benchmark.json` y los archivos A/B presentes y legibles.
- **16 archivos de evidencia** listados en el manifiesto, con SHA-256 concordantes, sin extras ni faltantes.
- Commit del manifiesto idéntico al SHA técnico indicado; árbol limpio; archivos `probe_error.txt` y `C_runtime_error.txt` vacíos.
- `probe.environment.ready=true`, `audit_A_static_traceability=PASS`, `audit_B_negative_guards=PASS`, `audit_C_real_compatibility=EXECUTED`, `real_compatibility_verified=true`.
- Estado del benchmark `MEASURED_LOCAL_ONLY_NOT_CERTIFIED`; `phase_certified=false` y `ci_postmerge=NOT_CLAIMED`.

**Límite explícito:** el asistente que prepara este borrador **NO ha leído ni verificado directamente los 16 archivos de la computadora**. El conjunto y sus hashes se registran aquí como **verificación efectuada y comunicada por Codex**, no como una segunda comprobación independiente ni como CI. No se han adjuntado al repositorio los logs locales ni credenciales.

## 2. Ejecuciones y mediciones informadas

| Control | Muestras aprobadas | Mediana/duración comunicada | Alcance correcto |
| --- | ---: | ---: | --- |
| API .NET 10, Release incremental | 3/3 | 6.9366 s mediana | Build incremental; no tiempo de arranque HTTP |
| Angular 22/Node 24, production incremental | 3/3 | 32.8119 s mediana | Build incremental; no métricas de UX del navegador |
| Contratos Outbox .NET 10, Release | 3/3 | 6.6356 s mediana | Build de proyecto de tests; no prueba de rendimiento SQL |
| MySQL 8.4 local, smoke Outbox | 1/1 | 25.0527 s total | Integración y arranque de contenedor; no latencia de consultas |

El smoke MySQL se realizó, según el informe, con imagen fijada por digest, contenedor **desechable `--network none`**, datos sintéticos y sin contenedores de prueba restantes. **No se conectó** a la base remota `crecisol_dev`; no se infiere compatibilidad de esa instancia ni se prueba aquí un producto desplegado.

Las duraciones expresan la línea basal de **esa máquina y ese entorno de ejecución**. No permiten inferir rendimiento en producción, percentiles HTTP, TPS, disponibilidad ni aislamiento multitenant completo.

## 3. Tres auditorías A/B/C — alcance y resultados de Codex

### A — Requisitos, fuente y trazabilidad: PASS informado

- Integridad del Documento Maestro vigente; prefijo histórico preservado, sin alterar Word/PDF.
- Relación de C03-09 con el plan C03 y antecedentes C03-07/C03-08 certificados.
- SHA exacto de la ejecución local y correspondencia con la rama.
- Validación de regresiones documentales C03-07/C03-08 y verificación del alcance limitado de C03-09.

### B — Seguridad y negativos: PASS informado

- 15 pruebas negativas/positivas del ejecutor.
- 9 contratos del orquestador, incluyendo protección de ramas y fail-closed.
- 13 controles TLS/DEV **offline**, sin usar la base remota.
- 8 pruebas de compatibilidad Word/logo vigente.
- C03-07: 16/16 negativas; C03-08: 18/18 negativas.
- Auditorías independientes C01-06 A/B/C y C03-07/C03-08 A/B, según informe.
- Codex no detectó defectos **P0/P1 dentro del alcance revisado**; esto no equivale a certificar funciones privadas, autorización de tenants, respaldos o la aplicación integral.

### C — Compilación y rendimiento: EJECUTADA, resultado satisfactorio informado

- Nueve compilaciones incrementales reales con toolchain instalada y `probe.ready=true`.
- Integración con MySQL 8.4 desechable y sin conexiones externas.
- Sin incidencias registradas en los archivos de errores, conforme al informe.
- La auditoría C **de C03-09** no habilita la certificación de C03 completo ni de C04.

## 4. Diff, arquitectura, riesgos y reversibilidad

El cotejo de GitHub frente al SHA `dev` anterior identificó **13 archivos** de alcance técnico original: nuevas utilidades/tests en `qa/`, documentación `docs/c03/` y un ajuste puntual en `qa/c01_06_official_assets.py`. Sin modificaciones en `frontend/`, `backend/`, `database/`, archivos maestros Word/PDF, workflows existentes ni actas certificadas.

**Justificación del único ajuste histórico:** el generador oficial de assets C01-06 usaba la huella del Word de 08-11-2026 después de haberse aprobado la edición comercial actual. La corrección exige `canonical_active_word()`, usa `CURRENT_WORD_SHA` para leer el DOCX vigente y sigue comparando el SHA-256 de la **misma imagen original aprobada**. No altera el Word, el logo, la constante `EXPECTED` histórica, los proyectos de Angular/.NET ni su salida funcional. Las pruebas independientes C01-06 A/B/C pasaron, según Codex.

**Hallazgos menores y tratamiento:**
1. `git diff --check` marcó **cinco** separadores Markdown de dos espacios finales en `C03_09_PREAUDITORIAS_ESTATICAS.md`. Se reemplazaron por líneas en blanco en un **commit documental posterior al SHA técnico medido**, sin cambiar código de ejecución.
2. `A_historical_static.txt` conserva textos de preflight antiguos que dicen `NO EJECUTADO`. Es un registro de una etapa anterior; no invalida ni sustituye la medición posterior en `C_runtime_benchmark.json`. La secuencia temporal debe preservarse.
3. El cambio de fuente documental afectaba el prebuild del logo: fue corregido y probado localmente antes de la ejecución exitosa de C03-09.

**Riesgos abiertos:** falta segunda lectura directa del paquete local por quien firme el acta; revisión del diff final documental; CI PREMERGE/POSTMERGE y revisión/aceptación formal pendientes. No se atribuyen aquí pruebas de autenticación privada ni datos del servidor DEV remoto.

**Reversibilidad:** todos los cambios están en una rama `feature/`; no hay migraciones ni alteraciones de datos persistentes. En caso de hallazgo, no integrar a `dev`; corregir exclusivamente la rama de CRECISOL y repetir solo las pruebas afectadas. Nunca modificar `solqaryn/Solqaryn`.

## 5. Puertas pendientes de certificación — obligatorias

- [ ] Revisar y aceptar formalmente la propuesta por el propietario **con referencia expresa a C03-09 y al SHA correspondiente**.
- [ ] Validar el paquete local `manifest.json` y SHA-256 de las 16 evidencias desde la fuente original o una entrega auditada; registrar conclusión del revisor, sin publicar secretos ni archivos de máquinas.
- [ ] Revisar el diff final y la integridad del registro documental generado después del SHA técnico de las pruebas.
- [ ] Preparar PR técnico exclusivamente hacia `dev`, cuando se autorice y pueda procesarse su CI; verificar **PREMERGE** del SHA exacto.
- [ ] Tras la integración autorizada, verificar **POSTMERGE** en `dev` `completed/success` sobre el SHA exacto. Un estado `failure`, `skipped` o `pending` **no equivale a PASS**.
- [ ] Formalizar el acta definitiva con resultados verificables de las tres auditorías y certificación del SHA correcto. **No reutilizar esta propuesta como prueba de CI.**

**No se creó ni fusionó ningún PR durante la elaboración de esta propuesta; no se ejecutaron GitHub Actions ni despliegues.** La imposibilidad actual de ejecutar CI se conserva como una puerta abierta, no se sustituye por evidencias locales mediante una excepción tácita.

## 6. Dictamen provisional

**Dictamen recomendado:** `C03-09: APTA PARA PROPONER CIERRE TÉCNICO, NO CERTIFICADA`.

Los resultados comunicados por Codex respaldan que las pruebas locales requeridas funcionaron con el stack real, pero **no autorizan todavía a marcar la microfase como CERTIFICADA**. El registro oficial de `dev` no se modifica por esta propuesta, C03-10 no se inicia y C04 permanece bloqueado.

**Firma/aceptación del propietario:** PENDIENTE.

**Certificación documental y CI:** PENDIENTES.
