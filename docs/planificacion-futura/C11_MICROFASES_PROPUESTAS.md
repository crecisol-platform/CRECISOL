# C11 · Inventario, compras y operaciones minoristas — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C10 certificado y aceptado. **Riesgo rector:** Movimientos de existencias consistentes e idempotentes.

## 10 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C11-01 | Modelo de existencias por ubicación | SKU, lote y sucursal con PK/FK correctas | No sumar stock entre tenants |
| C11-02 | Entradas y salidas tipadas | Movimientos compensables y auditables | No cantidades negativas no autorizadas |
| C11-03 | Reservas de stock concurrentes | Pruebas simultáneas sin sobreventa | No doble descuento por carrera |
| C11-04 | Transferencia entre sucursales | Origen/destino autorizados con trail | No mover stock entre negocios |
| C11-05 | Proveedores y órdenes de compra | Flujo borrador-aprobada-recibida | No aceptar proveedor ajeno |
| C11-06 | Costos, impuestos y redondeos | Cálculos reproducibles en fixtures | No precisión de punto flotante |
| C11-07 | Devoluciones y ajustes controlados | Motivo, reautenticación y bitácora | No borrar movimientos históricos |
| C11-08 | Alertas y puntos de reorden | Reglas parametrizadas y cuotas | No filtrar métricas de otra tienda |
| C11-09 | Panel operativo responsive | UI de stock y estados accesibles | No revelar costos privados en público |
| C11-10 | Auditorías integrales y acta C11 | A/B/C, pruebas de carreras y aceptación | No habilitar C12 sin consistencia |

## Puertas por microfase y capítulo

Cada C11-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c11/C11_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 10/10 microfases cerradas, C11 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C12. Este plan no demuestra implementación.
