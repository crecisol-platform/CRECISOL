# C27 · Aceptación funcional y seguridad: pruebas — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C26 con triple auditoría integral, acta y aceptación. **Riesgo clave:** Evidencia end-to-end, escenarios de negocio y penetración local reproducible.

## 10 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C27-01 | Inventario integral de casos de aceptación | Criterios de los capítulos implementados trazados | No marcar requisitos no probados |
| C27-02 | Fixtures multitenant deterministas | Actores A/B/branches con limpieza segura | No datos reales |
| C27-03 | Pruebas e2e de navegación y API | Recorridos completos con dependencias reales locales | No mocks para afirmar integración total |
| C27-04 | Pruebas de seguridad BOLA/IDOR | Matriz de actores/acciones/recursos cruzados | No acceso horizontal |
| C27-05 | Pruebas de sesiones y recuperación | Tokens, MFA y revocación bajo ataque | No reutilización |
| C27-06 | Abuso, concurrencia e idempotencia | Carreras, retries y duplicados probados | No cobro/reserva doble |
| C27-07 | Medios, workers y backups | Ataques de URLs, archivos y contextos | No fuga por exportación |
| C27-08 | Performance y accesibilidad integral | Presupuestos medidos y WCAG automatizado/manual | No declarar conformidad manual no ejecutada |
| C27-09 | Trazabilidad de fallos y regresiones | Evidencia de defectos y soluciones con SHA | No silenciar tests |
| C27-10 | Auditorías integrales y acta C27 | A/B/C y aceptación del capítulo | No abrir C28 con controles críticos rojos |

## Regla de avance y cierre

C27-XX se habilita solo tras C27-10 anterior cuando aplique, con especificación individual en `docs/c27/C27_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C27** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta aceptada y certificación expresa antes de habilitar C28. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.
