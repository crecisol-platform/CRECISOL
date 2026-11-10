# C15 · Membresías y acceso físico universal; perfil gimnasio — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C14 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** Suscripción de negocio y controles de acceso físico simulados.

## 11 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C15-01 | Modelo de planes de membresía | Beneficios, vigencias y cuotas parametrizadas | No permisos por campo libre |
| C15-02 | Ciclo de suscripción del cliente | Alta, pausa, renovación y baja probadas | No reactivar vencidos sin autorización |
| C15-03 | Perfiles y elegibilidad de acceso | Verificación backend de persona y tenant | No acceso entre gimnasios |
| C15-04 | Control de cupos y visitas | Check-in idempotente por instalación | No doble conteo por reintento |
| C15-05 | Credenciales QR de acceso | Tokens efímeros revocables y anti-replay | No QR eterno ni reutilizado |
| C15-06 | Puerta física simulada | Driver abstracto y eventos sintéticos | No activar cerraduras reales |
| C15-07 | Horarios, clases y reservas | Capacidad por recurso y reglas de membresía | No sobrerreserva |
| C15-08 | Renovaciones y cobros simulados | Ledger y estados reproducibles | No cargos efectivos |
| C15-09 | Panel de ocupación y asistencia | Indicadores agregados y accesibles | No exponer datos de socios ajenos |
| C15-10 | Incidentes, revocación y emergencia | Auditoría de acceso y rechazo inmediata | No acceso tras revocación |
| C15-11 | Auditorías integrales y acta C15 | A/B/C, antifraude/privacidad y aceptación | No abrir C16 con controles incompletos |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c15/C15_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C15:** 11/11 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C16. Todo permanece **PENDIENTE** hasta habilitación secuencial.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C15.

- **COM:** COM-002, COM-003, COM-007.
- **Microfases a revisar sin renumerar:** C15-01, C15-02, C15-08 y C15-11.
- **Aceptación futura:** Diferenciar membresías que el negocio vende a consumidores de las suscripciones SaaS de las empresas; usar modelos/cuentas y autorizaciones independientes.
- **Negativos:** Un consumidor no accede a plan comercial de CRECISOL ni puede cancelar suscripción SaaS de la empresa.
- **Pruebas definidas y NO ejecutadas:** COM-T07 y COM-T08.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
