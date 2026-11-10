# C21 · Cloudflare R2: imágenes, videos, documentos y archivos — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C21, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C20 y CI dev verde. **Riesgo rector:** Toda integración de medios externa sigue prohibida hasta autorización adicional.

## 10 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C21-01 | Modelo de metadatos y políticas | Propiedad, tenant, clasificación y retención | No URL pública persistente para privados |
| C21-02 | Puertos de almacenamiento abstractos | Contratos backend independientes del proveedor | No SDK ni credenciales de producción en frontend |
| C21-03 | Adapter local emulado | Pruebas de upload, lectura y eliminación sintéticos | No comunicación R2 real |
| C21-04 | Validación MIME y contenido | Sniffing, tamaños y tipos permitidos | No macros, SVG activo o polyglots |
| C21-05 | Escaneo y cuarentena | Estados antes de publicar y pruebas de rechazo | No descarga de archivo sin escaneo |
| C21-06 | Rutas firmadas y caducidad | Tokens acotados por actor/tenant/recurso | No enlaces reusables fuera de permiso |
| C21-07 | Transformaciones y previews seguras | Previews aisladas y originales inmutables | No EXIF sensible ni SSRF |
| C21-08 | Cuotas, lifecycle y eliminación | Pruebas de límites, versionado y purga | No borrar objetos ajenos |
| C21-09 | Integración R2 condicionada | Plan de acceso y pruebas requeridas sin ejecutar | No activar Cloudflare ni comprar servicios |
| C21-10 | Auditorías integrales y acta C21 | A/B/C y aceptación; señalar pruebas externas pendientes | No certificar integración real sin evidencia autorizada |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c21/C21_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 10 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C22. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C21.

- **COM:** COM-011, COM-014.
- **Microfases a revisar sin renumerar:** C21-01, C21-04, C21-05, C21-06, C21-07, C21-08 y C21-10.
- **Aceptación futura:** Validación y preview segura de banners, imágenes, videos y archivos de campañas, con autorización según plano/tenant.
- **Negativos:** No publicar SVG/HTML activo, ni un asset privado de otro tenant; no usar enlace temporal como permiso permanente.
- **Pruebas definidas y NO ejecutadas:** COM-T07 y COM-T14.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
