# C24 · VPS Hostinger y Docker: front, API y MySQL privados — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C24, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C23 y CI dev verde. **Riesgo rector:** PROHIBIDO desplegar en VPS/producción hasta autorización futura expresa.

## 8 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C24-01 | Modelo de topología y fronteras | Plano de redes y separación de servicios | No exponer puerto MySQL públicamente |
| C24-02 | Contenedores de desarrollo endurecidos | Builds reproducibles sin credenciales | No imágenes con secretos incrustados |
| C24-03 | Reverse proxy y TLS simulados | Configuración testeada contra host malicioso | No declarar certificados reales |
| C24-04 | Volúmenes y seguridad de datos | Backups locales desechables y control de permisos | No montar discos productivos |
| C24-05 | Variables y secretos de despliegue | Esquemas sin valores y política de rotación | No registrar credenciales reales |
| C24-06 | Observabilidad y health probes locales | Errores/latencia/arranque con red emulada | No enviar telemetría externa |
| C24-07 | Plan de cutover y recuperación | Runbook, dry-run y reversión reproducible | No ejecutar DNS, VPS ni tráfico real |
| C24-08 | Auditorías integrales y acta C24 | A/B/C y pendiente explícita de validación productiva | No certificar despliegue real mientras siga prohibido |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c24/C24_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 8 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C25. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.
