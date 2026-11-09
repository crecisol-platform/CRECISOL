# CRECISOL — Capítulo 02 · Personas, experiencias y arquitectura de producto

**Estado:** C02 EN DESARROLLO; **C02-01, C02-02, C02-03, C02-04 y C02-05 CERRADAS TÉCNICAMENTE (5/9)**; actas [C02-01](c02/C02_01_ACTA_MICROFASE.md), [C02-02](c02/C02_02_ACTA_MICROFASE.md), [C02-03](c02/C02_03_ACTA_MICROFASE.md), [C02-04](c02/C02_04_ACTA_MICROFASE.md) y [C02-05](c02/C02_05_ACTA_MICROFASE.md). Solo C01 certificado (**1/37 capítulos**); C02 **NO CERTIFICADO**, C03 bloqueado. C02-05 implementó solo estados públicos carga/error/offline; vacío y confirmación todavía no operativos. **C02-06 EN DESARROLLO**: cuatro patrones de formulario/borrador y tabla de solo lectura comprobables en harness Angular aislado; **5/9** permanece hasta A/B/C, pruebas, merge, CI postmerge y acta.

**Puerta de entrada ya comprobada:** C01 certificado en PR [#29](https://github.com/solqaryn-platform/CRECISOL/pull/29), SHA integrado `c4f688edeb63d8b032ea40a85559486cddd00809`, con 4 GitHub Actions postmerge SUCCESS, incluida [C01-08 #42613271746](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42613271746). Aceptación expresa del propietario en acta `docs/c01/C01_08_AUDITORIA_Y_ACTA.md`. Fuente maestra única Word SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`.

## Alcance y precedencia

El capítulo 02 del Word establece seis experiencias: landing SaaS, contratación/registro, consola SuperAdmin, panel negocio, puesto empleado y portal consumidor; el centro demo es modo ilustrativo sintético, no actor con privilegios. Los cinco contextos de persona de la tabla original son visitante, SuperAdmin, propietario, empleado y cliente. Identidad visual por tenant con componentes nucleares comunes; seguridad y permisos siempre desde backend. La regla de **NO selector de empresa en login** prevalece sobre textos antiguos y la ambigüedad implica rechazo seguro.

**Discrepancia DIF-C02-01:** `MAE-C02-01..08` son ocho puertas normativas de proceso del Word (extraer, diseñar, codificar, probar, auditorías A/B/C, acta); `C02-01..09` son nueve microentregables técnicos provisionales para implementación secuencial. C02-01 (inventario) satisface la fase de extracción MAE-C02-01, pero las demás puertas no están ejecutadas. El detalle está en [C02-01 especificación y matriz](c02/C02_01_ESPECIFICACION.md). No declarar que la propuesta técnica reemplaza la fuente ni habilita C03.

## Orden secuencial de entregas previsto

| Microfase | Objetivo | Estado |
| --- | --- | --- |
| C02-01 | Inventario de personas, contextos, seis experiencias y restricciones | **CERRADA TÉCNICAMENTE**: PR #30, merge `f40ab1ec47731eddd4b55a4f28a311b3800e68f4`, PR acta #31 y CI postmerge #42617017733 SUCCESS |
| C02-02 | Necesidades y tareas por experiencia | **CERRADA TÉCNICAMENTE**: PR #32, merge `455987893827cb5b25d4b127457efd81225267b7`; 6 experiencias, 18 necesidades, 7 pasos, 11 negativos de contrato, A/B/C y CI postmerge #42619923854 SUCCESS, acta documentada (2/9) |
| C02-03 | Arquitectura de información y rutas públicas | **CERRADA TÉCNICAMENTE**: PR #34, merge `6ccd98960f36dce3407392fd26406366b4cd0650`, 4 rutas públicas (3 lazy informativas), 8/8 Chromium y A/B/C; CI postmerge #42624454291 SUCCESS, C01-08 integral #42624454085 SUCCESS; acta de cierre (3/9) |
| C02-04 | Navegación privada propuesta y controles backend requeridos | **CERRADA TÉCNICAMENTE (diseño)**: PR #36, SHA `ecb7807e79427d5059c8b5f4ee0c5dd2195b7cb5`; 4 contextos E03–E06 y 12 necesidades, 18 negativos de contrato, 12/12 Chromium, auditorías A/B/C y CI postmerge #42630487933 SUCCESS; **sin rutas privadas, backend auth ni DB** (4/9) |
| C02-05 | Estados universales de interacción | **CERRADA TÉCNICAMENTE (5/9)**: PR #38, SHA `24e3b3c73133a35bdb336269517f123765734e86`, cinco estados tipados, tres públicos activos, 18 negativos de contrato, 19/19 Chromium, A/B/C y seis CI postmerge SUCCESS incluyendo #42639836939; **sin autorización privada, operaciones, cobros ni DB** |
| C02-06 | Patrones de formularios y tablas UX | **EN DESARROLLO**: campos y validación local, descarte seguro de borradores, tabla accesible y filtro/orden local, prueba real en harness separado de producción; A/B/C y cierre pendientes, sin rutas privadas, registros ni cobros |
| C02-07 | Diseño móvil y accesibilidad basal | PENDIENTE |
| C02-08 | Recorridos y contratos de experiencia; tests reales | PENDIENTE |
| C02-09 | Auditorías integrales del capítulo, acta y aceptación expresa | PENDIENTE |

Cada microfase requiere objetivo, análisis de impacto, PR en `feature/c02-xx-*`, pruebas positivas/negativas **realmente ejecutadas**, tres auditorías A/B/C, diff acotado, CI último HEAD/postmerge y acta. Un cierre técnico no certifica el capítulo; C02 requiere acta de capítulo y aceptación humana expresa antes de C03.

## Riesgo rector y límites de C02-01

Ningún shell frontend, dominio, slug, URL, ID aleatorio o selector entrega permisos. El backend debe comprobar identidad, pertenencia, rol/acción, tenant, sucursal, asignación y propiedad antes de acceder a datos o acciones privadas; el SuperAdmin requiere acceso excepcional a PII privada con MFA, motivo, periodo y auditoría. Demostraciones estrictamente sintéticas; consumidores aislados por negocio. Requisitos de identidad, multimedia, footer, cabecera y datos editables se trasladan a sus capítulos mediante contratos versionados, sin considerar implementado el panel.

C02-01 solo modifica **documentación y QA** para inventariar el contenido autoritativo. Sin cambios en `frontend/`, `backend/` ni `database/`, migraciones o permisos; las pruebas ejecutables de autorización/IDOR/tenancy en superficies privadas son futuras, no falsamente aprobadas aquí. No VPS, producción, DNS, servicios externos ni cobros. SOLQARYN solo puede ser referencia de lectura; nunca se escribe en su repositorio.
