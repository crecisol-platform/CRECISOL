# C16 · Extensiones por sector comercial — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C15 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** Extensiones configurables, sin duplicar backend por industria.

## 9 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C16-01 | Matriz sector-capacidades | Contratos universales y variaciones catalogadas | No clonar módulos por rubro |
| C16-02 | Mecanismo de habilitación de módulos | Features tipadas por plan/tenant | No activar módulos vía navegador |
| C16-03 | Adaptador para comercio minorista | Fixtures y reglas de inventario reutilizadas | No romper operaciones base |
| C16-04 | Adaptador de servicios profesionales | Citas, entregables y timeline por tenant | No omitir autorización de expediente |
| C16-05 | Adaptador de restauración | Pedidos, cocina y mesas sobre contratos comunes | No duplicar pagos |
| C16-06 | Adaptador de membresía/gimnasio | Clases y accesos sobre contratos comunes | No mezclar membresías ajenas |
| C16-07 | Campos y reglas sectoriales allowlist | Schemas por tipo y valores válidos | No permitir código inyectado |
| C16-08 | Pruebas combinadas multirrubro | Tenant mixto con 2+ rubros aislados | No pérdida de datos al cambiar rubro |
| C16-09 | Auditorías integrales y acta C16 | A/B/C y aceptación con regresión | No habilitar C17 sin universalidad |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c16/C16_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C16:** 9/9 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C17. Todo permanece **PENDIENTE** hasta habilitación secuencial.
