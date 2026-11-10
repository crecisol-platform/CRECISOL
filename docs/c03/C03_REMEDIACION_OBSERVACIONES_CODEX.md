# CRECISOL — Remediación de tres observaciones de Codex (C03, sin abrir C03-06)

**Ámbito:** solqaryn-platform/CRECISOL, rama feature/c03-remediacion-observaciones-codex hacia dev. Sin escritura a main, SOLQARYN, VPS, MySQL productivo ni servicios externos.

## 1. Estados históricos frente a vigentes

Codex observó un clon local en commit 6cb6cb7: README indicaba 0/37, C01_PREPARACION 1/37 y PROTOCOLO_CONTINUIDAD reflejaba el arranque antiguo. Eran instantáneas, no el estado de dev tras PR #58.

**Corrección propuesta:** marcar como históricas esas instantáneas; concentrar el avance actual en PLAN_37_CAPITULOS y sincronizar MASTER_REFERENCE, AGENTS, planes C01/C02/C03, README, decisiones, propuestas y protocolo sin reescribir el Documento Maestro Word/PDF. Estado: C01 8/8 y C02 9/9 certificados (2/37 capítulos); C03-01..05 certificados (5/10), C03-06 NO INICIADA, C03 NO CERTIFICADO, C04 BLOQUEADO.

## 2. C03-04 y C03-05: acreditación posterior real

C03-04 **sí certificada** por PR documental #56, SHA 6cb6cb79792efdddeab5fef0f0596a6532ed3afc y **10/10 CI POSTMERGE SUCCESS**. C03-05 **certificada** por PR documental #58, SHA 12fb19b0c35038c297939e17b27770c4fd63f77e y **11/11 CI POSTMERGE SUCCESS**. La inspección de Codex no incluía estos commits. Evidencia íntegra: [C03_05_CERTIFICACION_POSTMERGE.md](C03_05_CERTIFICACION_POSTMERGE.md). El acta original es una foto temporal previa a sus CI y permanece intacta.

## 3. Auditorías locales: rutas Windows y pruebas C

En C03-02 Domain y C03-03 Application se usaba str(Path.relative_to()), que emite barras invertidas en Windows; el código indexaba claves con barras '/'. Corrección técnica: Path.relative_to(...).as_posix() en ambas auditorías, verificado mediante PureWindowsPath y lectura de las claves reales del repositorio.

Los marcadores .c03-*-ci-proof no son archivos de origen ni se inventan: representan ejecución satisfactoria **en el mismo job de GitHub Actions**. Las auditorías C de build/.NET/MySQL/HTTP/Chromium siguen fail-closed cuando faltan. Se proporciona qa/c03_local_preflight.py para auditorías **A/B exclusivamente**, sin dependencies productivas ni acceso remoto; la salida aclara NOT EXECUTED para C y certificación. CI Windows/Linux ejecuta fuente real, negativos y prueba de denegación al faltar pruebas C. Ninguna omisión de prueba C se contabiliza como PASS.

## Impacto arquitectónico y reversión

Solo README, documentación, QA Python y workflows. Cero cambios a frontend productivo, backend .NET productivo, DDL MySQL, credenciales, red de producción, main o SOLQARYN. Triple verificación: A (fuente y cronología), B (negativos de etapa/tenant/CI sin suplantar evidencias), C (regresión GitHub Actions real .NET/MySQL/Angular más prueba de rutas en Windows). Rollback: revert mediante PR a dev, sin migraciones. No abre C03-06.

**Estado de revisión:** PR y pruebas de esta remediación sujetos a CI PREMERGE y POSTMERGE sobre SHA exacto; no declarar aprobados antes de verificarlos.
