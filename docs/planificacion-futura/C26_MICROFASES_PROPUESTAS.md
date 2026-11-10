# C26 · API, eventos y modelos de datos MySQL — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C25 con triple auditoría integral, acta y aceptación. **Riesgo clave:** Contratos tipados, compatibilidad evolutiva e idempotencia multitenant.

## 11 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C26-01 | Inventario de endpoints por módulo | Método/recurso/actor/permiso y scope registrados | No rutas sin autorización backend |
| C26-02 | Contrato OpenAPI y DTOs versionados | Esquema validado y pruebas de consumidores | No exponer entidades internas |
| C26-03 | Convenciones de errores y paginación | Estructura uniforme sin PII en errores | No enumeración cross-tenant |
| C26-04 | Eventos de dominio inmutables | Tipos y versiones registrados | No publicar datos privados sin control |
| C26-05 | Outbox transaccional | Commit atómico y deduplicación verificable | No pérdida ni doble envío |
| C26-06 | Procesadores idempotentes | Reintentos y DLQ en datos sintéticos | No dos transiciones por evento |
| C26-07 | Modelos MySQL tenant-aware | FK/índices y constraints compartidas/dedicadas | No FK entre tenants |
| C26-08 | Migración y compatibilidad del esquema | Expansión/contracción probadas en entornos locales | No ruptura de clientes antiguos |
| C26-09 | Contratos de caché, colas y búsquedas | Keys y filtros scope-aware | No filas cruzadas por caché |
| C26-10 | Carga y tracing de API | Benchmarks y correlaciones sin secretos | No N+1 ni log de token |
| C26-11 | Auditorías integrales y acta C26 | A/B/C y aceptación con regressiones | No iniciar C27 sin contrato estable |

## Regla de avance y cierre

C26-XX se habilita únicamente tras el cierre probado de su microfase inmediatamente anterior (C26-01 requiere la aceptación integral de C25), con especificación individual en `docs/c26/C26_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C26** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta con aceptación expresa del propietario y certificación antes de habilitar C27. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C26.

- **COM:** COM-001..COM-014.
- **Microfases a revisar sin renumerar:** C26-01, C26-02, C26-04, C26-05, C26-06, C26-07, C26-08, C26-09 y C26-11.
- **Aceptación futura:** Versionar DTO y eventos de oferta, consentimiento, activación, factura, pago confirmado, fin de renovación, suspensión, reactivación y campaña; contratos idempotentes.
- **Negativos:** Alterar host, id, versión o tenant no cambia usuario ni base; eventos repetidos o fuera de orden no duplican cobros ni renovación.
- **Pruebas definidas y NO ejecutadas:** COM-T04, COM-T07, COM-T08, COM-T09 y COM-T16.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
