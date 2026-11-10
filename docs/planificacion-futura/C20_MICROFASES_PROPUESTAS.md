# C20 · Sitios públicos, mapas y contenido editorial — propuesta de microfases

> **PREPLAN DOCUMENTAL, NO HABILITADO.** Deriva del índice vigente y las reglas técnicas; antes de activar C20, contrastar el texto completo del capítulo en el Documento Maestro Único y fijar matriz de trazabilidad, dependencia y pruebas. Este documento no modifica el Word ni acredita software.

**Entrada:** certificación/aceptación del capítulo C19 y CI dev verde. **Riesgo rector:** Contenido público solo publicado; SEO productivo depende de host verificado.

## 9 entregas pequeñas propuestas

| Código | Alcance | Prueba positiva requerida | Prueba negativa/condición NO GO |
|---|---|---|---|
| C20-01 | Modelo de páginas y publicación | Revisiones por tenant con permisos backend | No mostrar borradores anónimamente |
| C20-02 | Rutas y slugs públicos | Resolución sin colisiones y por dominio autorizado | No secuestro de slug ajeno |
| C20-03 | Plantillas de contenido editorial | Bloques allowlist y campos tipados | No código JS/HTML libre |
| C20-04 | Mapas y ubicación consentida | Datos sintéticos y permisos para geodatos | No exposición de ubicación privada |
| C20-05 | SEO estructurado y metadatos | Títulos, robots, sitemap y canonical según origen válido | No afirmar dominio adquirido sin verificar |
| C20-06 | Rendimiento de cargas públicas | Lighthouse local y presupuestos CSS/JS | No solicitud externa no autorizada |
| C20-07 | Accesibilidad y responsive | Flujos con teclado y lectores simulados | No navegación bloqueada en móvil |
| C20-08 | Protección anti-XSS y cache | Pruebas de contenido malicioso y tenant de origen | No cache compartida |
| C20-09 | Auditorías integrales y acta C20 | A/B/C, SEO condicionado y aceptación | No abrir C21 sin aprobación |

## Gobierno y evidencia

Una sola microfase activa. Antes de cada una: releer el Word canónico (`docs/MASTER_REFERENCE.md`), el acta anterior y el diff/CI actuales. Crear al activarla `docs/c20/C20_XX_ESPECIFICACION.md` con requisitos, impacto arquitectónico, tests positivos/negativos, A/B/C, autorización backend/tenant, reversión y bloqueo P0/P1. Ejecutar build real y tests, registrar SHA y fallos, PR a dev, CI del HEAD final, merge y CI posterior. **Ninguna prueba simulada sustituye una integración externa obligatoria si sigue prohibida**. No escribir en SOLQARYN ni tocar VPS/DNS/pagos reales.

**Puerta de capítulo:** 9 microfases cerradas no equivalen a certificado. Requiere pruebas integrales, auditorías A/B/C independientes, acta de riesgos, cero P0/P1 sin resolver y aceptación expresa antes de C21. Mantener BLOQUEADO si el capítulo exige validación externa/productiva que no está autorizada. Estado de todo el capítulo: **PENDIENTE**.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF ya fue incorporada a la rama del PR #73 con SHA y blob verificados; su aprobación normativa sigue pendiente de CI y fusión a dev**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C20.

- **Requisitos relacionados:** COM-001, COM-004, COM-011.
- **Microfases técnicas a revisar:** C20-01, C20-02, C20-03, C20-05, C20-08 y C20-09 (no equivalen a puertas normativas `MAE-C20-xx`).
- **Evidencia positiva futura exigida:** Resolver ruta/subdominio/dominio verificado y exponer solamente ofertas publicadas; reservar bloques de promoción/autopublicidad propios y, por acuerdo, espacios autorizados en sitios de empresas.
- **Negativos obligatorios:** No secuestro de slug ni trust de Host; sitio tenant no publica publicidad no autorizada; campañas y caché no cruzan tenants; payload editorial inseguro bloqueado.
- **Pruebas propuestas:** COM-T02, COM-T09, COM-T13 y COM-T14; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C20 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
