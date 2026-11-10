# C18 · Fidelización, promociones, reseñas y atención — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C17 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** Beneficios, moderación y datos personales por ámbito.

## 9 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C18-01 | Reglas de puntos y recompensas | Ledger de puntos reproducible y versionado | No saldo negativo por duplicación |
| C18-02 | Acumulación idempotente | Eventos de compra sintéticos deduplicados | No sumar por replay |
| C18-03 | Canje y reversión | Pruebas de concurrencia y compensación | No doble canje |
| C18-04 | Promociones y condiciones | Vigencias, límites y elegibilidad servidor | No aplicar descuentos manipulados |
| C18-05 | Reseñas con origen verificable | Reglas de publicación y consentimiento | No publicar datos privados |
| C18-06 | Moderación y apelación | Historial de decisiones auditable | No editar reseña ajena |
| C18-07 | Atención y tickets | Asignación y expediente de soporte por tenant | No acceder a tickets de otro negocio |
| C18-08 | Panel de fidelización | UX accesible y privacidad de rankings | No ranking de clientes no consentidos |
| C18-09 | Auditorías integrales y acta C18 | A/B/C, abuso, privacidad y aceptación | No abrir C19 sin controles |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c18/C18_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C18:** 9/9 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C19. Todo permanece **PENDIENTE** hasta habilitación secuencial.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C18.

- **COM:** COM-004, COM-011.
- **Microfases a revisar sin renumerar:** C18-04, C18-07, C18-08 y C18-09.
- **Aceptación futura:** Diferenciar descuentos SaaS administrados por CRECISOL de promociones y fidelización que cada negocio ofrece en su propio tenant.
- **Negativos:** Campaña comercial del tenant no modifica precios globales ni concede acceso a facturación de plataforma.
- **Pruebas definidas y NO ejecutadas:** COM-T01 y COM-T08.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
