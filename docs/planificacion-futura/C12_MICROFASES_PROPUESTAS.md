# C12 · Pedidos, cotizaciones, caja y cobros del negocio — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C11 certificado y aceptado. **Riesgo rector:** Timeline de expediente y aprobación ligada a versión exacta; pagos simulados.

## 12 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C12-01 | Estados de pedido y cotización | Máquina de estados documentada y testeada | No transición arbitraria |
| C12-02 | Renglones y totales deterministas | Cálculos decimales, descuentos y monedas | No manipular precio desde cliente |
| C12-03 | Cotización versionada e inmutable | Hash y edición generan nueva revisión | No aprobar revisión obsoleta |
| C12-04 | Aceptación parcial del cliente | Firma/decisión asociada a versión exacta | No aprobar ítems alterados después |
| C12-05 | Timeline universal de expediente | Eventos ordenados, responsables y trazas | No eliminar decisiones anteriores |
| C12-06 | Checkout y carrito aislados | Contexto tenant y expiración verificables | No compartir carrito entre negocios |
| C12-07 | Caja y turnos internos | Apertura/cierre con autorización backend | No asignar caja de otra sucursal |
| C12-08 | Registro de cobros ficticios | Ledger sintético idempotente | No tarjetas ni cargos productivos |
| C12-09 | Devoluciones y cancelaciones | Compensaciones e historial verificables | No doble reembolso por reintento |
| C12-10 | Notificaciones de estado simuladas | Outbox local con idempotencia | No enviar mensajes comerciales reales |
| C12-11 | Seguimiento, entrega y cierre | Evidencia de cumplimiento antes de cerrar | No marcar entregado sin resolución |
| C12-12 | Auditorías integrales y acta C12 | A/B/C, concurrencia, BOLA y aceptación | No habilitar C13 con fuga o saldo erróneo |

## Puertas por microfase y capítulo

Cada C12-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c12/C12_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 12/12 microfases cerradas, C12 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C13. Este plan no demuestra implementación.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C12.

- **COM:** COM-002, COM-005, COM-013.
- **Microfases a revisar sin renumerar:** C12-01, C12-02, C12-08, C12-09 y C12-12.
- **Aceptación futura:** Separar catálogo, caja, pago y reembolso del negocio de cualquier factura por licencias SaaS de CRECISOL; ambos usan contratos distintos.
- **Negativos:** No vender membresía SaaS con flujo de caja de tienda ni mezclar ingresos, reembolsos o saldos de un tenant con plano global.
- **Pruebas definidas y NO ejecutadas:** COM-T05 y COM-T07.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
