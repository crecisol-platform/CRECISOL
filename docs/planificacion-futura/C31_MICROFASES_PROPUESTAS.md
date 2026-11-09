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

C31-XX se habilita solo tras C31-12 anterior cuando aplique, con especificación individual en `docs/c31/C31_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C31** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta aceptada y certificación expresa antes de habilitar C32. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.
