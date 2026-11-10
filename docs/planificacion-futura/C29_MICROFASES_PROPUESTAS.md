# C29 · Puertas de lanzamiento y decisiones pendientes — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C28 con triple auditoría integral, acta y aceptación. **Riesgo clave:** No autorizar despliegue o pagos sin decisión específica futura.

## 8 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C29-01 | Inventario de ADR y pendientes | Riesgos, responsables y estado rastreados | No cerrar ADR por suposición |
| C29-02 | Matriz de puertas de seguridad | P0/P1, BOLA, secretos, backups y recuperación | No GO si gate no ejecutado |
| C29-03 | Compatibilidad y aceptación operativa | Stacks y pruebas reales con versión exacta | No por etiqueta de versión |
| C29-04 | Protección de datos y cumplimiento | Evidencias legales y privacidad por jurisdicción | No afirmar cumplimiento sin revisión |
| C29-05 | Plan de lanzamiento y rollback | Runbooks y ventanas simuladas | No cambiar VPS o DNS |
| C29-06 | Presupuestos, límites y costos | Escenarios medidos con tarifas verificadas | No comprar servicios |
| C29-07 | Plan de observabilidad/soporte | Alertas, soporte y respuesta proyectados | No recolectar PII real |
| C29-08 | Auditorías integrales y acta C29 | A/B/C, decisión formal GO/NO GO | No considerar lanzado un producto no desplegado |

## Regla de avance y cierre

C29-XX se habilita únicamente tras el cierre probado de su microfase inmediatamente anterior (C29-01 requiere la aceptación integral de C28), con especificación individual en `docs/c29/C29_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C29** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta con aceptación expresa del propietario y certificación antes de habilitar C30. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C29.

- **COM:** COM-001..COM-014.
- **Microfases a revisar sin renumerar:** C29-01, C29-02, C29-03, C29-04, C29-06, C29-07 y C29-08.
- **Aceptación futura:** Registrar precios, modalidad real, legalidad de renovación/cancelación, PSP, impuestos, publicidad y SLA como decisiones pendientes antes del lanzamiento.
- **Negativos:** Sin aprobar términos legales, pagos o protección de datos por inferencia; sin adelantar despliegue o contratación de terceros.
- **Pruebas definidas y NO ejecutadas:** COM-T11, COM-T12 y COM-T18.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
