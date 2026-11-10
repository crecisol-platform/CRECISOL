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

**Certificación C19:** 11/11 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C20. Todo permanece **PENDIENTE** hasta habilitación secuencial.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF ya fue incorporada a la rama del PR #73 con SHA y blob verificados; su aprobación normativa sigue pendiente de CI y fusión a dev**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C19.

- **Requisitos relacionados:** COM-011, COM-014.
- **Microfases técnicas a revisar:** C19-02, C19-04, C19-05, C19-09, C19-10 y C19-11 (no equivalen a puertas normativas `MAE-C19-xx`).
- **Evidencia positiva futura exigida:** Distinguir campaña institucional CRECISOL y Social Hub del tenant; respetar ubicación permitida, fechas, aprobación de creatividad, consentimiento/preferencias y métricas agregadas por scope.
- **Negativos obligatorios:** Publicidad de marca asociada no expone clientes ni acceso a SOLQARYN; campaña deshabilitada no se publica; impedir HTML/JS inseguro, audiencias ajenas y mensajes no consentidos.
- **Pruebas propuestas:** COM-T07, COM-T13 y COM-T14; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C19 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
