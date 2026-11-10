# C31 · Pagos por enlace, cobros y suscripciones — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C30 con triple auditoría integral, acta y aceptación. **Riesgo clave:** Cobros externos reales expresamente prohibidos en etapa actual.

## 12 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C31-01 | Modelo de estados de pago | Orden, intento, captura y reversión definidos | No transición de pago por cliente |
| C31-02 | Contratos de PSP intercambiables | Puertos y claves de idempotencia | No credenciales ni SDK real no autorizado |
| C31-03 | Links de pago efímeros simulados | Tokens con caducidad/contexto y prueba de replay | No enlace reutilizable entre tenants |
| C31-04 | Aprobación previa de monto y moneda | Totales inmóviles vinculados a versión | No modificar importe después de aceptar |
| C31-05 | Webhook firmado en sandbox local | Firma, timestamp y orden de eventos testeados | No marcar pagado por payload sin firma |
| C31-06 | Conciliación de pagos ficticios | Fixtures de cargos/reintegros/retrasos | No cobrar dinero real |
| C31-07 | Refund y disputa simulados | Estados y compensaciones inmutables | No doble devolución |
| C31-08 | Suscripción recurrente ficticia | Ciclos, prorrateo y vencimiento reproducibles | No doble cobro por retries |
| C31-09 | Autorización y privacidad de pagos | No almacenar PAN/CVV, PII mínima | No mostrar transacción de otro tenant |
| C31-10 | UI de pago segura no operativa | Estados accesibles y mensajes veraces | No botón que acepte dinero real |
| C31-11 | Pruebas de fallo/duplicado/timeout | Backoff y recuperación por idempotency key | No pago marcado exitoso antes de confirmación |
| C31-12 | Auditorías integrales y acta C31 | A/B/C, evidencia sandbox; bloquear cobro real | No certificar pasarela productiva sin autorización |

## Regla de avance y cierre

C31-XX se habilita únicamente tras el cierre probado de su microfase inmediatamente anterior (C31-01 requiere la aceptación integral de C30), con especificación individual en `docs/c31/C31_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C31** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta con aceptación expresa del propietario y certificación antes de habilitar C32. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF todavía debe incorporarse y verificarse con su SHA en GitHub antes de aprobar el plan como normativo**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C31.

- **Requisitos relacionados:** COM-005, COM-007, COM-009, COM-013.
- **Microfases técnicas a revisar:** C31-01, C31-04, C31-05, C31-06, C31-07, C31-08, C31-09, C31-11 y C31-12 (no equivalen a puertas normativas `MAE-C31-xx`).
- **Evidencia positiva futura exigida:** Modelar pagos SaaS separados de pagos de los negocios; cuota mensual anticipada o prepago total; simular fechas, conciliación, reintentos de suscripción, refund y cambios de renovación sin PSP real.
- **Negativos obligatorios:** No doble cobro ni factura pagada sin evidencia; webhooks fuera de orden/repetidos no alteran saldo; no continuar cobro tras no renovación válida; no guardar PAN/CVV ni conectar cobro real.
- **Pruebas propuestas:** COM-T03, COM-T04, COM-T05, COM-T06, COM-T12 y COM-T16; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C31 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
