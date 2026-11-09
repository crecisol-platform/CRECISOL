# CRECISOL — Capítulo 02 · Personas, experiencias y arquitectura de producto

**Estado:** C02 EN DESARROLLO; C02-01 **CERRADA TÉCNICAMENTE** con [acta](c02/C02_01_ACTA_MICROFASE.md), **1/9 microfases C02 cerradas**, 1/37 capítulos certificados (únicamente C01), C02 **NO CERTIFICADO**. C02-02 **EN DESARROLLO DOCUMENTAL** (mapa de 18 necesidades y seis experiencias; [especificación](c02/C02_02_ESPECIFICACION.md)); aún **1/9** cerrada, C03 bloqueado. La finalización documental exige CI premerge/postmerge del acta.

**Puerta de entrada ya comprobada:** C01 certificado en PR [#29](https://github.com/solqaryn-platform/CRECISOL/pull/29), SHA integrado `c4f688edeb63d8b032ea40a85559486cddd00809`, con 4 GitHub Actions postmerge SUCCESS, incluida [C01-08 #42613271746](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42613271746). Aceptación expresa del propietario en acta `docs/c01/C01_08_AUDITORIA_Y_ACTA.md`. Fuente maestra única Word SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`.

## Alcance y precedencia

El capítulo 02 del Word establece seis experiencias: landing SaaS, contratación/registro, consola SuperAdmin, panel negocio, puesto empleado y portal consumidor; el centro demo es modo ilustrativo sintético, no actor con privilegios. Los cinco contextos de persona de la tabla original son visitante, SuperAdmin, propietario, empleado y cliente. Identidad visual por tenant con componentes nucleares comunes; seguridad y permisos siempre desde backend. La regla de **NO selector de empresa en login** prevalece sobre textos antiguos y la ambigüedad implica rechazo seguro.

**Discrepancia DIF-C02-01:** `MAE-C02-01..08` son ocho puertas normativas de proceso del Word (extraer, diseñar, codificar, probar, auditorías A/B/C, acta); `C02-01..09` son nueve microentregables técnicos provisionales para implementación secuencial. C02-01 (inventario) satisface la fase de extracción MAE-C02-01, pero las demás puertas no están ejecutadas. El detalle está en [C02-01 especificación y matriz](c02/C02_01_ESPECIFICACION.md). No declarar que la propuesta técnica reemplaza la fuente ni habilita C03.

## Orden secuencial de entregas previsto

| Microfase | Objetivo | Estado |
| --- | --- | --- |
| C02-01 | Inventario de personas, contextos, seis experiencias y restricciones | **CERRADA TÉCNICAMENTE**: PR #30, merge `f40ab1ec47731eddd4b55a4f28a311b3800e68f4`, CI integral A/B/C postmerge #42616163656 SUCCESS; acta de cierre documental por integrar |
| C02-02 | Necesidades y tareas por experiencia | **EN DESARROLLO**: 18 recorridos, seis experiencias, criterios given/when/then, negativas y siete pasos omnicanal; exige auditorías A/B/C y CI real antes de cierre |
| C02-03 | Arquitectura de información y rutas públicas | PENDIENTE |
| C02-04 | Navegación privada propuesta y controles backend requeridos | PENDIENTE |
| C02-05 | Estados universales de interacción | PENDIENTE |
| C02-06 | Patrones de formularios y tablas UX | PENDIENTE |
| C02-07 | Diseño móvil y accesibilidad basal | PENDIENTE |
| C02-08 | Recorridos y contratos de experiencia; tests reales | PENDIENTE |
| C02-09 | Auditorías integrales del capítulo, acta y aceptación expresa | PENDIENTE |

Cada microfase requiere objetivo, análisis de impacto, PR en `feature/c02-xx-*`, pruebas positivas/negativas **realmente ejecutadas**, tres auditorías A/B/C, diff acotado, CI último HEAD/postmerge y acta. Un cierre técnico no certifica el capítulo; C02 requiere acta de capítulo y aceptación humana expresa antes de C03.

## Riesgo rector y límites de C02-01

Ningún shell frontend, dominio, slug, URL, ID aleatorio o selector entrega permisos. El backend debe comprobar identidad, pertenencia, rol/acción, tenant, sucursal, asignación y propiedad antes de acceder a datos o acciones privadas; el SuperAdmin requiere acceso excepcional a PII privada con MFA, motivo, periodo y auditoría. Demostraciones estrictamente sintéticas; consumidores aislados por negocio. Requisitos de identidad, multimedia, footer, cabecera y datos editables se trasladan a sus capítulos mediante contratos versionados, sin considerar implementado el panel.

C02-01 solo modifica **documentación y QA** para inventariar el contenido autoritativo. Sin cambios en `frontend/`, `backend/` ni `database/`, migraciones o permisos; las pruebas ejecutables de autorización/IDOR/tenancy en superficies privadas son futuras, no falsamente aprobadas aquí. No VPS, producción, DNS, servicios externos ni cobros. SOLQARYN solo puede ser referencia de lectura; nunca se escribe en su repositorio.
