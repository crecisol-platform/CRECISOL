# C32 · Cloudflare R2 y gestión segura de todos los archivos — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C31 certificado, acta y aceptación expresa. **Riesgo rector:** Seguridad compartida de todos los tipos de archivo; R2 productivo condicionado.

## 10 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C32-01 | Inventario universal de adjuntos | Tipos y dueños por tenant/expediente | No objetos sin owner/scope |
| C32-02 | Políticas de tamaño y cuotas | Límites por plan, tenant y usuario | No DoS por carga sin límite |
| C32-03 | Identificación real de contenidos | Sniffing vs extensión y firmas | No aceptar ejecutables disfrazados |
| C32-04 | Antivirus y cuarentena local | Fixture malicioso y estados de revisión | No permitir descarga antes de aprobar |
| C32-05 | Cifrado y metadatos de acceso | Referencias opacas, minimización y retención | No URL pública fija de documento privado |
| C32-06 | Previsualización segura | PDF/imagen/video aislados, sin scripts | No SVG activo, macro o SSRF |
| C32-07 | URLs temporales revocables | Actor/tenant/recurso, caducidad y propósito | No acceso BOLA por token |
| C32-08 | Ciclo de vida y eliminación | Versiones, legal-hold y purga verificables | No borrar adjuntos de otro tenant |
| C32-09 | Adapter R2 diferido | Contrato y pruebas contra emulador local | No conexiones Cloudflare reales sin permiso |
| C32-10 | Auditorías integrales y acta C32 | A/B/C y aceptación con gate de proveedor | No certificar R2 en producción con mocks |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c32/C32_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 10/10, la certificación integral C32 requiere 3 auditorías de capítulo y aceptación expresa antes de habilitar C33. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.
