# C07 · Ciberseguridad integral y respuesta a incidentes — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Esta guía deriva del índice `docs/PLAN_37_CAPITULOS.md` y de las políticas transversales de CRECISOL. Antes de activar C07, contrastar cada punto con el texto íntegro del capítulo en el Word canónico, su SHA registrado en `docs/MASTER_REFERENCE.md` y sus anexos; ajustar el número o alcance por PR trazable. Este archivo **no modifica el Documento Maestro Único**.

**Puerta de entrada:** C06 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C07 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** P0: controles de defensa en profundidad sin servicios de producción.

## Plan secuencial de 10 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C07-01 | Modelo de amenazas STRIDE y activos | Amenazas priorizadas y superficies catalogadas | No excluir archivos, jobs o backups |
| C07-02 | Validación de entradas y salida | Casos XSS, SQLi, SSRF y deserialización | No interpolar consultas o código cliente |
| C07-03 | Protección HTTP y navegador | CSRF, CORS, headers y cookies comprobados | No wildcard con credenciales |
| C07-04 | Seguridad de secretos y dependencias | Escaneo reproducible y política de rotación | No secretos en logs, código o CI |
| C07-05 | Rate limits, antifraude y abuso | Pruebas de límites por actor/tenant/IP | No bloqueo global de otro tenant |
| C07-06 | Auditoría y telemetría segura | Correlación y redacción de PII | No exposición de eventos cross-tenant |
| C07-07 | Integridad de medios y enlaces | Escaneo, validación, expiración y permisos | No SVG activo, macros o descarga ajena |
| C07-08 | Respuesta a incidentes y runbooks | Clasificación, contención y comunicación simulada | No ejecutar cambios productivos |
| C07-09 | Backups y recuperación aislada simulada | Restore negativo cruzado y objetivo de RTO documentado | No restaurar datos tenant A en B |
| C07-10 | Auditorías integrales y acta C07 | A/B/C, pentests locales y aceptación | No abrir C08 con P0/P1 pendientes |

## Contrato obligatorio para C07-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c07/C07_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c07-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 10 microfases **no** certifica C07. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C08. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.
