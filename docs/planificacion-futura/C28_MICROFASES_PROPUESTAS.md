# C28 · Matriz completa de 30 mejoras — microfases propuestas

> **BORRADOR, NO DESARROLLADO, NO CERTIFICADO.** Esta planificación deriva del índice aprobado y principios transversales. Antes de activarla, releer el capítulo íntegro del Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, extraer IDs de requisitos y revisar este reparto por PR. No modifica el maestro.

**Entrada:** C27 con triple auditoría integral, acta y aceptación. **Riesgo clave:** Trazabilidad de 30 mejoras institucionales a requisitos y pruebas.

## 8 microfases secuenciales

| Código | Entregable | Prueba positiva exigida | Bloqueo negativo exigido |
|---|---|---|---|
| C28-01 | Extraer las 30 mejoras del maestro | 30 entradas y origen exacto verificados | No inventar ítems ni renumerar |
| C28-02 | Clasificar mejoras por capítulo | Propietario y dependencia por cada mejora | No duplicar cumplimiento |
| C28-03 | Definir criterios SMART de aceptación | Prueba medible por mejora | No criterio subjetivo sin evidencia |
| C28-04 | Mapear implementación actual | Archivo, PR y SHA para mejoras entregadas | No atribuir código inexistente |
| C28-05 | Inventariar pruebas faltantes | Gaps por P0/P1/P2 y acción correctiva | No ocultar deuda |
| C28-06 | Remediar dentro del alcance autorizado | Correcciones regresadas por PR | No adelantar capítulos fuera de secuencia |
| C28-07 | Auditorías cruzadas de las 30 mejoras | A/B/C, matrices completas | No PASS parcial como total |
| C28-08 | Acta integral y aceptación C28 | Cierre solo con 30/30 verificadas o bloqueo | No iniciar C29 con mejoras sin evidencia |

## Regla de avance y cierre

C28-XX se habilita solo tras C28-08 anterior cuando aplique, con especificación individual en `docs/c28/C28_XX_ESPECIFICACION.md` creada **al comenzar**, trazabilidad real, tests positivos y negativos, auditorías A/B/C, cero P0/P1 sin resolver, revisión de diff/secretos, PR a dev, CI sobre último HEAD y tras merge, acta y rollback. Si no hay herramienta para ejecutar una prueba, marcar **NO EJECUTADA** y bloquear. Nadie puede adelantar otra microfase/capítulo por una CI parcial. No producción ni SOLQARYN.

El **capítulo C28** necesita además pruebas de regresión integral, auditorías A/B/C de capítulo, acta aceptada y certificación expresa antes de habilitar C29. Cuando depende de aprobaciones externas o proveedor real y estos están prohibidos, el estado debe seguir **BLOQUEADO**, nunca certificado artificialmente.
