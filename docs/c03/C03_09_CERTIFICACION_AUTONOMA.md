# CRECISOL — C03-09 | Certificación técnica autónoma (9/10)

**DICTAMEN: C03-09 CERTIFICADA EN SU ALCANCE TÉCNICO**. Decisión aprobada por el asistente bajo la delegación expresa del propietario del 11-11-2026, documentada en [decisión operativa](../DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md). **Sin requisito de GitHub Actions, sin CI simulada y sin uso de una computadora del propietario para este cierre**.

**Alcance único:** compatibilidad real de Angular 22/Node 24, SDK .NET 10, MySQL 8.4 LTS desechable, mediciones incrementales reproducibles y regresión del logo del Documento Maestro vigente. No se certifica el capítulo C03 integral, C03-10, el despliegue, producción, autorización privada, aislamiento multitenant del producto completo ni operaciones con base DEV remota.

**Repositorio:** `solqaryn-platform/CRECISOL`.

**Rama de certificación:** `feature/c03-09-compat-benchmark-local`; se integrará exclusivamente en `dev` mediante operación Git controlada.

**Commit técnico ejecutado realmente:** `53cc2134eca8b33c808bcd5d7643684b51080fa8`.

**Último commit antes de formalizar este dictamen:** `ebbd4b17201bc5f306260959e7df7dd73721a2c5`.

**Base `dev` contrastada:** `25130475550cc2993e59d5ae4fc8537be0b47bea`.

**Documento Maestro vigente:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, según `docs/MASTER_REFERENCE.md`.

**Logotipo oficial aprobado:** SHA-256 `9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f`.

## 1. Evidencia real, origen y responsabilidad

Codex efectuó previamente pruebas **en un equipo local con las herramientas reales**; después inspeccionó de forma independiente el paquete de salida. El propietario transmitió dos informes de Codex y aceptó expresamente el cierre técnico de C03-09.

Carpeta de evidencia local **no versionada**: `/home/javi/crecisol-local-evidence/c03-09-20261111T042639Z-53cc2134eca8`.

Según los informes de Codex:
- `manifest.json`, `probe.json`, `C_runtime_benchmark.json` y A/B consistentes.
- **16/16 archivos enumerados con SHA-256 concordantes**, sin archivos faltantes ni extras.
- `probe.ready=true`, A/B `PASS`, C `EXECUTED`, `real_compatibility_verified=true`.
- `probe_error.txt` y `C_runtime_error.txt` vacíos.
- Estado original exacto del runner: `MEASURED_LOCAL_ONLY_NOT_CERTIFIED`, `phase_certified=false`, `ci_postmerge=NOT_CLAIMED`. **Este valor histórico NO se edita:** describe el estado al medir y no es un acta.

**Transparencia:** este asistente no accedió directamente a los 16 archivos del disco local ni ejecutó de nuevo los builds. Su certificación se basa en la validación independiente informada por Codex, la revisión del código versionado en GitHub y la delegación expresa del propietario. Se distingue evidencia comunicada de observación directa; no se inventan hashes de archivos individuales ni una segunda ejecución.

## 2. Auditorías A/B/C de C03-09 — conclusión

### A. Trazabilidad e integridad — APROBADA

- Microfase acotada al plan C03-09, con antecedentes C03-01..08 certificados y C03-10 todavía pendiente.
- Vigencia de la edición comercial y conservación del prefijo histórico del Word; Word/PDF, logo y actas anteriores no modificados por la implementación.
- `qa/c01_06_official_assets.py` comprueba primero `canonical_active_word()`, luego extrae desde `CURRENT_WORD_SHA` y exige el SHA del logotipo **original**.
- Evidencia vinculada al commit técnico exacto `53cc2134...`; al comparar este SHA con el posterior `ebbd4b17...`, **solo cambian documentos y gobernanza**, no código de ejecución.

### B. Seguridad, negativos y regresiones — APROBADA

Codex reportó PASS de: 15 pruebas del runner, 9 contratos del orquestador, 13 controles TLS/DEV sin conexión, 8 negativas/positivas Word/logo, 16/16 negativas C03-07, 18/18 negativas C03-08, más auditorías independientes C01-06 A/B/C y C03-07/C03-08 A/B.

La revisión directa del código versionado confirma las guardas `--run --confirm-local`, el preflight estricto, timeout y terminación de procesos, comandos permitidos, salida de error limitada a hash, imagen MySQL fijada por digest y contenedor efímero con `--network none`. El probe remoto DEV es explícito, de solo lectura, con `VERIFY_IDENTITY`; **no fue ejecutado** y no se presenta como conectividad probada.

No se identificaron P0/P1 **en el alcance estrecho de esta microfase**. Esto no afirma que el sistema privado o sus controles cross-tenant futuros estén implementados.

### C. Compatibilidad, builds y rendimiento basal — APROBADA CON EVIDENCIA REAL PREVIA

| Evidencia ejecutada por Codex | Resultado | Medición |
| --- | --- | --- |
| Compilaciones incrementales API .NET 10 Release | 3/3 PASS | mediana 6.9366 s |
| Compilaciones Angular 22/Node 24 producción | 3/3 PASS | mediana 32.8119 s |
| Compilaciones contratos Outbox .NET 10 Release | 3/3 PASS | mediana 6.6356 s |
| MySQL 8.4, smoke Outbox, contenedor `--network none` | PASS | duración integral 25.0527 s |

Son tiempos de **compilaciones incrementales** y duración del **smoke test de integración**, no p95 HTTP ni p95 SQL ni TPS ni evidencia de producción. El MySQL usado fue local y desechable; `crecisol_dev` remota no fue consultada.

## 3. Revisión de diferencias, P0/P1 y arquitectura

La comparación Git contra `dev` anterior se limitó al ejecutor y pruebas `qa/`, documentación `docs/c03/`, ajuste puntual del extractor del logo y, posteriormente, documentación de gobernanza. El workflow de CI añadido temporalmente fue **retirado** al cambiar el protocolo. No quedaron modificaciones de `frontend/`, `backend/`, `database/`, documentos maestros, actas históricas ni workflows existentes.

**Impacto:** sin API/DTO, permisos ni rutas nuevas; sin migraciones, datos reales, servicios externos, pagos o despliegue. Regresiones del logo y C03-07/C03-08 comprobadas por Codex. Reversión: antes de integrar, descartar rama; después de integrar, revertir solo los commits de esta microfase y su decisión operativa previo análisis de impacto.

La nota `A_historical_static.txt` que decía `NO EJECUTADO` era una preauditoría **anterior**; no reemplaza la evidencia de runtime posterior. Los cinco espacios finales documentales detectados inicialmente fueron corregidos y el diff final de 14 archivos fue revisado por Codex como limpio **antes de agregar la política**, la cual se evaluó por separado. No se atribuye a Codex una revisión de archivos nuevos posteriores.

## 4. Decisión autónoma de certificación y gates

**CERTIFICADA — C03-09 (9/10)** por suficiencia de evidencia real de las tres auditorías, sin P0/P1 conocidos en su alcance, cierre técnico aceptado previamente y delegación posterior de la decisión de certificación al asistente.

- **GitHub Actions:** no utilizado como gate. Ejecuciones fallidas de PR #74 quedan como hechos históricos; ninguna se representa como `SUCCESS`.
- **PR #74:** cerrado **sin merge**; por la política vigente, el repositorio se utiliza como sistema de control de versiones, no como ejecutor.
- **Prueba posterior a los cambios de gobernanza:** no se realizó compilación nueva, y no se requiere repetir por modificar solo documentación e instrucciones. Antes de iniciar código C03-10, debe verificarse que `dev` contiene exactamente el alcance certificado.
- **C03-10:** siguiente microfase, exclusivamente auditorías integrales A/B/C y cierre del capítulo C03. **NO CERTIFICADA todavía**.
- **C03 y C04:** C03 sigue **NO CERTIFICADO** hasta concluir C03-10; C04 permanece **BLOQUEADO**.

**Responsable del dictamen:** asistente ChatGPT, revisión técnica delegada por el propietario; no se presume firma humana o criptográfica.

**Estado resultante de esta acta:** `C03-09 = CERTIFICADA_EN_SU_ALCANCE_TECNICO`; `C03 = EN_PROGRESO`; `C03-10 = NO INICIADA`.
