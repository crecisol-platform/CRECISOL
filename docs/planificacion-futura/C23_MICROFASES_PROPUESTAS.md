# C23 · Legislación, políticas y consentimiento — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C23, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C22 y CI dev verde. **Riesgo rector:** Datos legales solo verificados por propietario; consentimiento versionado.

## 9 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C23-01 | Matriz de jurisdicción y obligaciones | Requisitos por región y fuente oficial trazados | No presentar asesoría legal definitiva |
| C23-02 | Identidad del emisor y titulares | Datos jurídicos marcados verificados/no verificados | No inventar registro o titularidad |
| C23-03 | Versionado de políticas | Documentos de privacidad y uso con SHA | No reemplazo silencioso de aceptación |
| C23-04 | Consentimientos granulares | Finalidad, fecha, origen y versión exacta | No optar al consentimiento por defecto |
| C23-05 | Retiro y preferencias | Revocación propagada y auditada | No seguir enviando comunicaciones |
| C23-06 | Retención y eliminación condicionada | Plazos y conflictos legales documentados | No purgar expediente legalmente retenido |
| C23-07 | Derechos de acceso/exportación | Solicitudes autenticadas con minimización | No divulgar datos de otro titular |
| C23-08 | UI de avisos y accesibilidad | Consentimiento claro en móvil y teclado | No dark patterns ni casillas premarcadas |
| C23-09 | Auditorías integrales y acta C23 | A/B/C + revisión legal pendiente de aceptación | No certificar cumplimiento legal inexistente |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c23/C23_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 9 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C24. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF todavía debe incorporarse y verificarse con su SHA en GitHub antes de aprobar el plan como normativo**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C23.

- **Requisitos relacionados:** COM-006, COM-007, COM-008, COM-011, COM-014.
- **Microfases técnicas a revisar:** C23-01, C23-02, C23-03, C23-04, C23-05, C23-06, C23-07, C23-08 y C23-09 (no equivalen a puertas normativas `MAE-C23-xx`).
- **Evidencia positiva futura exigida:** Conservar aceptación y versión contractual; ofrecer rescisión/no renovación con acuse, fecha efectiva y derechos; separar consentimiento publicitario de contratación SaaS; revisar ley hondureña por profesional competente.
- **Negativos obligatorios:** No usar términos jurídicos no revisados ni cobros o penalizaciones impuestas por defecto; no bloquear solicitud de cancelación por deuda; no usar dark patterns, renovaciones opacas ni borrado de retención legal.
- **Pruebas propuestas:** COM-T11, COM-T12, COM-T13 y COM-T17; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C23 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
