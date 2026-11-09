# C19 · Marketing, Social Hub y mensajería multicanal — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C18 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** Comunicaciones solo simuladas en esta etapa; consentimiento trazable.

## 11 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C19-01 | Canales y propósitos de comunicación | Inventario de email, SMS y redes con límites | No habilitar integración real sin aprobación |
| C19-02 | Consentimientos y preferencias | Opt-in/out versionado por tenant y canal | No mensajes sin consentimiento |
| C19-03 | Plantillas parametrizadas seguras | Variables allowlist y escaping comprobado | No HTML o links externos ejecutables |
| C19-04 | Audiencias por criterios autorizados | Segmentación tenant-scoped y minimizada | No exportar PII ajena |
| C19-05 | Programación y borradores | Estados, horarios y zona horaria verificables | No campaña no aprobada |
| C19-06 | Cola local idempotente | Simulación con reintentos y DLQ | No enviar duplicados |
| C19-07 | Webhooks y firmas en diseño | Verificación de eventos sintéticos | No aceptar callback no firmado |
| C19-08 | Bandeja Social Hub | Mensajes asociados a consentimiento y tenant | No mezclar conversaciones de empresas |
| C19-09 | Control de bajas y supresión | Revocación inmediata y pruebas negativas | No reactivar contacto bloqueado |
| C19-10 | Reportes agregados y cuotas | Métricas privadas por tenant y budget | No exponer destinatarios |
| C19-11 | Auditorías integrales y acta C19 | A/B/C, privacidad y aceptación | No iniciar C20 sin cierre |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c19/C19_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C19:** 11/11 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C20. Todo permanece **PENDIENTE** hasta habilitación secuencial.
