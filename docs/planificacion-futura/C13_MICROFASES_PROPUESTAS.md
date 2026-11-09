# C13 · Reservas, horarios, personal y recursos — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C12 certificado y aceptado. **Riesgo rector:** Disponibilidad concurrente y aislamiento de agendas.

## 10 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C13-01 | Modelo de recursos y calendarios | Espacios, empleados y capacidad por tenant | No reservar recurso ajeno |
| C13-02 | Reglas de horarios y feriados | Zona horaria y DST con casos reales | No horarios inexistentes o solapados |
| C13-03 | Disponibilidad calculada | Ventanas libres y bloqueos verificables | No filtrar agenda privada |
| C13-04 | Reservas transaccionales | Prueba de doble booking concurrente | No sobreasignar cupos |
| C13-05 | Reprogramación y cancelación | Estados, límites y timeline inmutable | No cambiar reserva ajena |
| C13-06 | Asignación de empleados | Permisos branch y disponibilidad por actor | No publicar datos sensibles de personal |
| C13-07 | Listas de espera y recordatorios locales | Orden, TTL y desduplicación | No notificaciones reales sin autorización |
| C13-08 | Vista calendario responsive | Teclado, lector y mobile reales | No dependencia exclusiva de drag-and-drop |
| C13-09 | Check-in, atención y expediente | Eventos vinculados a versión de cita | No cerrar servicio sin registro |
| C13-10 | Auditorías integrales y acta C13 | A/B/C, carreras y aceptación | No iniciar C14 sin coherencia |

## Puertas por microfase y capítulo

Cada C13-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c13/C13_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 10/10 microfases cerradas, C13 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C14. Este plan no demuestra implementación.
