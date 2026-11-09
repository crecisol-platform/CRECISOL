# C22 · Finanzas ligeras, analítica y dashboards — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C22, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C21 y CI dev verde. **Riesgo rector:** Indicadores segregados y contabilidad funcional no fiscal.

## 10 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C22-01 | Catálogo de métricas por ámbito | Fórmulas, fechas y fuentes rastreables | No mezclar tenants |
| C22-02 | Modelo de eventos financieros locales | Tipos de entrada/salida idempotentes | No cargar datos bancarios reales |
| C22-03 | Agregaciones y cierres de período | Fixtures reconciliables y decimales | No doble contabilización |
| C22-04 | Estados de cuenta internos | Proyecciones por sucursal/tenant | No exponer precios de otro negocio |
| C22-05 | Reportes filtrados y exportables | Paginación, roles y cargas sintéticas | No fórmula CSV ejecutable |
| C22-06 | Dashboards accesibles | Gráficos con tablas y formatos alternativos | No depender solo del color |
| C22-07 | Forecasting parametrizado | Escenarios reproducibles sin promesas | No presentar estimaciones como hechos |
| C22-08 | Carga, consultas e índices | Benchmark local con volumen sintético | No consultas N+1 en reportes |
| C22-09 | Pruebas de privacidad de métricas | Intentos de BOLA y caché cruzada | No inferir ingresos de otros tenants |
| C22-10 | Auditorías integrales y acta C22 | A/B/C, conciliación y aceptación | No comenzar C23 sin controles |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c22/C22_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 10 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C23. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.
