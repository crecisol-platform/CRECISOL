# CRECISOL — C03-10 · Especificación de auditoría integral y cierre

**Estado inicial:** EN AUDITORÍA / NO CERTIFICADA. **Capítulo C03:** NO CERTIFICADO. **C04:** BLOQUEADO.

**Repositorio:** solqaryn-platform/CRECISOL. **Base exacta:** dev @ 6b64271c53ed1c6ccccb81291b5794d897103cd3. No se tocarán main, SOLQARYN, despliegues, VPS, facturación ni producción.

## Dependencias y autoridad

- C01 y C02 cuentan con actas de cierre limitadas a sus alcances.
- C03-01 a C03-08 cuentan con certificaciones históricas verificables; no se reescriben.
- C03-09 figura CERTIFICADA EN SU ALCANCE (9/10) mediante C03_09_CERTIFICACION_AUTONOMA.md, con informe local previo de Codex y posterior integración a dev. No se atribuyen a esta ejecución las mediciones de Codex.
- Documento Maestro Único: docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx. Hash activo de Word: ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. PDF derivado: 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77.
- El propietario informó el 11-11-2026 que hizo público exclusivamente CRECISOL y reactivó GitHub Actions automáticos; la nueva decisión prospectiva se documenta en DECISION_OPERATIVA_2026-11-11_RETORNO_CI_C03.md.

## Alcance exclusivo de C03-10

No desarrollar nueva lógica de negocio. Ejecutar A, B y C sobre el estado de dev y la integración candidata, con pruebas reproducibles de Angular 22/Node 24, .NET 10, MySQL 8.4 efímera, rutas públicas, contratos y dependencias. El objetivo es auditar C03 y su compatibilidad con el C01/C02 certificado, no certificar multitenancy privado aún no implementado.

### A — Trazabilidad y fuente canónica

1. Verificar el Word/PDF actual y la invariancia histórica con qa/master_source_guard.py.
2. Comprobar las 10 microfases, sus especificaciones y el acta limitada de C03-09.
3. Contrastar grafo y límites Clean Architecture, SHA exacto y delta respecto de dev.
4. Conservar huellas históricas y bloquear C04 hasta cierre y aceptación.

### B — Seguridad adversarial

1. Ejecutar A/B históricos C03-01..08, negativos C03-09 y pruebas de mutación complementarias.
2. Confirmar que no se habilitan rutas privadas, workers productivos, conexiones remotas, secretos ni despliegues.
3. Comprobar consultas de prueba MySQL 8.4 sin red, alcance PLATFORM sintético, contratos DTO, permisos mínimos y rechazo de cambios inseguros.
4. Cualquier P0/P1, filtro tenant omitido en alcance implementado o evidencia falsa implica NO_GO.

### C — Pruebas de integración, arquitectura y rendimiento basal

1. Ejecutar builds Release de API/contratos .NET 10 y build de producción Angular 22 con Node 24.
2. Ejecutar pruebas .NET positivas y negativas, Chromium público y harness sintético, MySQL 8.4 desechable, regresión C01/C02 y chequeos de archivos de marca.
3. Adjuntar resultados del workflow PREMERGE y, después de integrar, POSTMERGE del SHA exacto. Un resultado skipped, cancelled, failure o timed_out NO equivale a PASS.
4. No extrapolar los benchmarks incrementales locales de C03-09 a p95 HTTP/SQL ni a cargas productivas.

## Puerta de salida y reversión

La certificación exige A/B/C con evidencia REAL, ausencia de P0/P1, PR revisado, estados SUCCESS de todos los jobs obligatorios de C03-10 para SHA candidato, integración controlada exclusivamente a dev, repetición POSTMERGE, acta final y aceptación expresa del propietario para declarar **C03 capítulo certificado**. Un paso no ejecutado se registra como NO EJECUTADO y bloquea el cierre que dependa de él.

Mientras falte cualquiera de estos elementos: C03-10 = EN AUDITORÍA, C03 = NO CERTIFICADO, C04 = BLOQUEADO. Revertir el PR por SHA en dev, sin tocar main, si falla el gate.

**Impacto planificado:** documentación y qa/.github/workflows; cero cambios funcionales en frontend/, backend/, database/, DTO, migraciones o permisos.
