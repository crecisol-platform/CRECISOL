# C09 · Constructor visual universal y design system — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C08 certificado y aceptado. **Riesgo rector:** Editor visual seguro con bloques allowlist y cambios versionados.

## 12 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C09-01 | Inventario de tokens del design system | Paleta, tipografía y medidas tipadas | No CSS/JS libre del usuario |
| C09-02 | Componentes base accesibles | Botones, campos, tablas, diálogo y estados testeados | No foco perdido ni elementos inaccesibles |
| C09-03 | Esquema de bloques permitidos | Validación JSON de layout y propiedades | No HTML arbitrario |
| C09-04 | Motor de render declarativo | Bloques tipados sin evaluación dinámica | No ejecución de scripts de cliente |
| C09-05 | Gestión de marca por tenant | Separar brand platform/tenant en API | No heredar activos privados ajenos |
| C09-06 | Editor de páginas aislado | Persistir borradores solo del tenant autorizado | No escritura cross-tenant |
| C09-07 | Preview aislada y versionada | Diferenciar borrador/publicado en URLs temporales | No exposición anónima de previews |
| C09-08 | Publicación atómica de revisiones | Compare-and-swap y rollback testeados | No sobrescritura concurrente |
| C09-09 | Biblioteca segura de medios y iconos | Allowlist y referencias por tenant | No SVG ejecutable ni enlaces externos |
| C09-10 | Historial, deshacer y trazabilidad | Versiones revisables con auditoría | No eliminar historial irreversible |
| C09-11 | Rendimiento y accesibilidad del editor | Pruebas móviles/teclado/navegador | No bundles excesivos ni motion obligatoria |
| C09-12 | Auditorías integrales y acta C09 | A/B/C, ataque de bloques y aceptación | No iniciar C10 sin editor aprobado |

## Puertas por microfase y capítulo

Cada C09-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c09/C09_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 12/12 microfases cerradas, C09 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C10. Este plan no demuestra implementación.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C09.

- **COM:** COM-004, COM-011, COM-012.
- **Microfases a revisar sin renumerar:** C09-01, C09-02, C09-03, C09-04, C09-07, C09-08, C09-11 y C09-12.
- **Aceptación futura:** Componentes editables y accesibles para tarjetas de ofertas, selector de plazos, avisos y slots publicitarios; preview segura sin alterar reglas de contrato.
- **Negativos:** No ejecutar HTML/JS arbitario en anuncios ni convertir edición visual en autorización de catálogo global.
- **Pruebas definidas y NO ejecutadas:** COM-T02, COM-T08, COM-T13 y COM-T14.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
