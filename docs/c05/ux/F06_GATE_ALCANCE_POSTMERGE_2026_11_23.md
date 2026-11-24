# CRECISOL C05 UX F06 — Corrección del gate de alcance postintegración

**Fecha:** 23 de noviembre de 2026 (Honduras). **Microtarea:** reparar el control estático de F06 que interpretaba archivos de fases anteriores como nuevos cambios de F06. **Estado:** EN VALIDACIÓN hasta ejecutar CI exacta y auditorías A/B/C. Solo Capítulo 05; C06 permanece bloqueado.

## Causa real

La CI de la candidata F06 en [#45217805123](https://github.com/crecisol-platform/CRECISOL/actions/runs/45217805123) se detuvo en el chequeo `qa/c05_ux_f06_feedback_audits.py A` por **fuera de alcance**, no por la compilación Angular, las pruebas Playwright ni MySQL. El código comparaba el SHA inicial F05 `b576d6ff64aea7d417c59aa851618dbf4dced68a` contra todo HEAD de F06; después de 243 commits y fusiones independientes esa diferencia incluye documentación, QA y vistas F01/F02/F03/F04/F05 que no pertenecen al ámbito F06.

La otra CI postintegración [inventario #45217805011](https://github.com/crecisol-platform/CRECISOL/actions/runs/45217805011) terminó **SUCCESS**, sin calificar ni certificar por sí sola las 65 superficies. Se preserva su resultado como evidencia aparte.

## Nueva política fail-closed

Se conserva íntegra la lista **ALLOWED F06** y su referencia inicial `b576d6f`, con solo dos entradas nuevas pertenecientes a esta microtarea (documentación y pruebas negativas). Las fusiones históricas están ancladas a dos SHAs exactos auditados:

- F04 integrada al candidato: `1d8a8e552b6eb7749d81aafaaa1e5201163b6c4e` (PR #182).
- F05 integrada al candidato: `196512a6b5747ce8c86a5d71c4e766af6ee4b047` (PR #183).

Los objetos deben ser ancestros verificables en orden: base inicial → F04 → F05 → HEAD. Un archivo fuera de ALLOWED solo puede pertenecer a la diferencia ya existente **hasta el SHA F05 aprobado**; no se blanquean archivos externos por agregarlos a un patrón amplio.

Para toda modificación posterior a F05 se comprueba **tanto el diff final como los nombres de archivos de cada commit**. Cualquier archivo ajeno a ALLOWED —backend, database, propiedad de otra fase, secretos, configuración, frontend no autorizado— es **NO-GO** incluso si se modificó y luego revirtió. Los cambios válidos de F06 siguen sujetos a inspección UTF-8, A/B/C, privacidad, seguridad, foco, accesibilidad, 65 superficies y E2E real Angular → .NET10 → MySQL8.4. Ninguna prueba anterior se silencia.

Las pruebas negativas cubren: incremento permitido, archivo de database nuevo, modificación de un archivo F05 ya aprobado, falsa limpieza por revertir un archivo F04, antecedente no reconocido, ausencia de cambios y lista de permisos estricta. El workflow las ejecuta antes de la compilación/CI visual.

## Evaluación de impacto

**Archivos previstos:** auditor de F06; fixture de negativas de alcance; workflow propio F06; este registro. **Sin cambios** en HTML/CSS de negocio, API, MySQL, migraciones, roles, tiempo de sesión (30 minutos iniciales, editable solo por autoridad backend), MFA SHA1, secretos, SOLQARYN, `dev`, `main` ni producción.

**Auditoría A — funcionalidad/trazabilidad:** el nuevo analizador clasifica correctamente cambios previos exactos y modificaciones F06 actuales, sin llamar PASS a CI histórico fallido.

**Auditoría B — seguridad/privacidad:** los commits nuevos fuera de F06 o que tocaron otra fase incluso transitoriamente son rechazados; el SHA aprobado se exige como ancestro exacto.

**Auditoría C — arquitectura/rendimiento/UX:** no cambia contrato Angular ni tamaño CSS de producción; regresiones completas siguen siendo obligatorias sobre la versión candidata resultante.

**Certificación prevista:** exigir dos trabajos F06 `SUCCESS` en el SHA final y CI de inventario 65 superficies satisfactoria. Este cierre será solo técnico/QA; las 65 valoraciones comerciales individuales ≥9.5 y aceptación humana continúan sujetas a sus controles separados. PR #172 mantiene barreras de recuperación SQL+llaves y autenticadores físicos. Prohibido desplegar o abrir Capítulo 06.
