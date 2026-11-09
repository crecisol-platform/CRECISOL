# C08 · Gobierno central, planes, contratos y facturación SaaS — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C07 certificado y aceptado. **Riesgo rector:** Gobierno platform/tenant y facturación únicamente simulada en esta etapa.

## 11 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C08-01 | Modelo de gobernanza y responsables | Cuentas y administradores con límites definidos | SuperAdmin no accede a datos privados por defecto |
| C08-02 | Catálogo versionado de planes | Entitlements y límites tipados comprobados | No conceder permisos por texto de marketing |
| C08-03 | Contrato de capacidades por suscripción | Semántica de estado, vigencia y suspensión | No activar funciones sin plan permitido |
| C08-04 | Políticas de alta y baja de tenant | Pruebas de transiciones y cuarentena | No borrar datos ajenos |
| C08-05 | Contratos comerciales parametrizables | Versiones y aceptaciones trazables | No inventar razón social o impuestos |
| C08-06 | Cálculo determinista de tarifas | Fixtures de prorrateo, moneda y redondeos | No cobro efectivo ni pasarela conectada |
| C08-07 | Facturación interna de SaaS simulada | Asientos y estados idempotentes en pruebas | No emisión fiscal ni cargos reales |
| C08-08 | Dunning y suspensión diferida | Simular reintentos sin alterar servicios ajenos | No suspender por evento duplicado |
| C08-09 | Consola global de planes y estados | Backend autorizado más UI accesible | No ver PII tenant sin acceso excepcional |
| C08-10 | Auditoría de cambios comerciales | Historial inmutable de precios y decisiones | No reescribir contratos aceptados |
| C08-11 | Auditorías integrales y acta C08 | A/B/C y aceptación explícita | No habilitar C09 ni producción con P0/P1 |

## Puertas por microfase y capítulo

Cada C08-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c08/C08_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 11/11 microfases cerradas, C08 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C09. Este plan no demuestra implementación.
