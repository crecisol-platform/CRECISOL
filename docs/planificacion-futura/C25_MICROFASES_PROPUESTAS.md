# C25 · GitHub, CI/CD y disciplina de microtareas — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C25, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C24 y CI dev verde. **Riesgo rector:** Integración trazable sin forzar branch protections ni compras.

## 8 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C25-01 | Inventario de workflows activos | Jobs y permisos verificados en dev | No asumir rulesets activos |
| C25-02 | Convenciones de rama y commits | PR pequeños y SHA reproducible | No commits directos a main |
| C25-03 | Matrix de compilación por módulo | Builds Angular/.NET/MySQL ejecutados | No PASS por archivo YAML |
| C25-04 | Pruebas negativas en CI | Casos tenancy, auth, XSS y fallos reales | No ignorar controles fallidos |
| C25-05 | Auditorías A/B/C reproducibles | Evidencia y SHA por microfase | No aprobar viejo commit |
| C25-06 | Política de artefactos y retención | Logs redactados y trazabilidad de ejecución | No publicar secretos en Actions |
| C25-07 | Rollback y recovery de PR | Revert/diff y carreras ensayados | No force-push ni merge sin CAS |
| C25-08 | Auditorías integrales y acta C25 | A/B/C, revisión del plan gratuito y aceptación | No exigir reglas premium ni desplegar |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c25/C25_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 8 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C26. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C25.

- **COM:** COM-001..COM-014.
- **Microfases a revisar sin renumerar:** C25-01, C25-02, C25-04, C25-05, C25-06, C25-07 y C25-08.
- **Aceptación futura:** Conservar hashes históricos del DOCX/PDF; nuevo Word único, PDF derivado, evidencias A/B/C y validadores versionados en PR documental reproducible.
- **Negativos:** No reemplazar un SHA esperado sin prueba de migración ni modificar actas certificadas; CI roja bloquea merge.
- **Pruebas definidas y NO ejecutadas:** COM-T18.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
