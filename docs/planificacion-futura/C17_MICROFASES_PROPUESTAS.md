# C17 · Historial del cliente y CRM en dos planos — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C16 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** CRM tenant y consumidor plataforma separados, seguimiento inmutable.

## 10 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C17-01 | Separación de identidad y perfil CRM | Relaciones públicas/privadas trazables | No consolidar PII entre tenants |
| C17-02 | Modelo de contactos por negocio | Consentimiento y pertenencia por tenant | No lookup global de clientes |
| C17-03 | Historial de interacción tenant | Eventos append-only por expediente | No borrar decisiones previas |
| C17-04 | Timeline universal de seguimiento | Check-in, diagnóstico, adjuntos y responsables | No cerrar caso sin evidencia |
| C17-05 | Decisiones versionadas del cliente | Aprobaciones ligadas al snapshot exacto | No aprobar cotización modificada |
| C17-06 | Búsqueda e identificación contextual | Paginación y filtros tenant-scoped | No enumeración cross-tenant |
| C17-07 | Visión plataforma de consumidor | Permisos separados del CRM del negocio | No superusuario implícito |
| C17-08 | Retención, exportación y eliminación | Pruebas de solicitudes válidas y límites | No eliminar bitácoras legalmente retenidas |
| C17-09 | UI CRM y accesibilidad | Casos de alta, edición y consulta por roles | No exponer campos ocultos en API |
| C17-10 | Auditorías integrales y acta C17 | A/B/C, BOLA y aceptación | No abrir C18 con fuga de PII |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c17/C17_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C17:** 10/10 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C18. Todo permanece **PENDIENTE** hasta habilitación secuencial.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C17.

- **COM:** COM-010, COM-012, COM-014.
- **Microfases a revisar sin renumerar:** C17-01, C17-06, C17-07, C17-08 y C17-10.
- **Aceptación futura:** CRM plano SaaS limitado a empresas contratantes y datos administrativos; CRM del negocio solo para sus clientes.
- **Negativos:** No usar una vista plataforma de consumidores para consultar contactos privados de los negocios ni exportar PII ajena.
- **Pruebas definidas y NO ejecutadas:** COM-T07 y COM-T17.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
