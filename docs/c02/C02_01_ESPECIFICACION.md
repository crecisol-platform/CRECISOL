# CRECISOL — C02-01 · Inventario de personas, experiencias y contextos

**Estado:** ESPECIFICACIÓN Y MATRIZ DE TRAZABILIDAD de la primera microfase C02. No equivale a pantallas privadas, autenticación, autorización implementada ni certificación del capítulo 02. La evidencia y el acta técnica de esta microfase se cierran solo tras CI A/B/C y postmerge.

**Autoridad:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, capítulo «02. Personas, experiencias y arquitectura de producto», hasta inicio del capítulo 03. Fuente validada con `qa/c02_01_source.py`, sin modificar el Word ni crear otro maestro. C01 certificado: [PR #29](https://github.com/solqaryn-platform/CRECISOL/pull/29), commit `c4f688edeb63d8b032ea40a85559486cddd00809`, CI integral postmerge [#42613271746](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42613271746).

## 1. Límite exacto y discrepancia de numeración

La fuente original del capítulo aporta **11 párrafos (incluidos títulos de subsección) y una tabla de 7 filas contando encabezado**. El anexo de implementación, las reglas de precedencia de acceso contextual sin selector, las tablas suplementarias y la integración G.7 se conservan como requisitos transversales, sin contarlos indebidamente como filas de la tabla original. El texto anterior que permitía seleccionar empresa durante login está expresamente sustituido por la precedencia normativa del 7 de noviembre de 2026.

**DIF-C02-01:** el Word integra ocho **puertas de proceso** `MAE-C02-01` (Extraer), `02` (Diseñar), `03` (Codificar), `04` (Probar), `05` (Auditar A), `06` (Auditar B), `07` (Auditar C), `08` (Acta), mientras `docs/planificacion-futura/C02_MICROFASES_PROPUESTAS.md` desglosa **nueve microfases técnicas provisionales** `C02-01..C02-09`. La presente microfase operativa C02-01 materializa MAE-C02-01 (extracción/inventario); **no** significa que las otras ocho microfases o las siete puertas restantes estén ejecutadas ni habilita C03. La nomenclatura se mantiene separada para no alterar el maestro.

## 2. Trazabilidad completa: once párrafos originales

| Ref. | Contenido fiel del maestro | Resultado de inventario / dependencia |
| --- | --- | --- |
| P01 | Seis experiencias coherentes, identidad visual variable por tenant y controles/contratos comunes | Separar *experiencia* de *persona*; navegación y backend por contexto, sin seis aplicaciones aisladas. C02-02/03/04; C03/04/06 |
| P02 | 2.1. Flujo omnicanal representativo | Orden de siete pasos P03–P09, aún no implementados |
| P03 | Visita al sitio del negocio por ruta, subdominio o dominio propio | Contexto público; el dominio no autoriza datos privados. C04/C20 |
| P04 | Backend resuelve dominio verificado y configuración publicada | Contexto tenant resuelto del lado servidor; rechazar host falso. C04/C20 |
| P05 | Consulta pública de catálogo, mapa, horarios y servicios | Solo publicaciones; jamás PII ni borradores. C10/C13/C20 |
| P06 | Reserva, compra o login requieren identidad y permisos backend | No tomar roles del navegador, URL ni guard Angular. C05/C06/C12/C13 |
| P07 | Persistencia transaccional con evento recuperable para notificar | Outbox/idempotencia y trazabilidad futuros. C03/C19/C26 |
| P08 | Negocio ve operaciones; consumidor únicamente sus comprobantes e historial | Proyecciones aisladas por tenant, propiedad del recurso y publicación. C06/C17 |
| P09 | Operación alimenta métricas, auditoría y eventualmente pago/fidelidad | Sin cobros reales ni acceso global implícito. C07/C18/C22/C31 |
| P10 | 2.2. UX no negociables | Requisitos transversales para las superficies posteriores, C02-05/06/07 |
| P11 | Iconos con nombre accesible, tooltip y alternativa móvil; formularios por pasos, modales focalizados, vacíos/carga comprensibles, teclado, lectores de pantalla, WCAG 2.2 AA y movimiento reducido | Mantener texto accesible en toda interacción; C02-05/06/07/08, C09/C34 |

Las referencias P01..P11 son ordinales del **contenido original** del capítulo; el prefacio de precedencia y las notas `OriginalNote` son anexos, y se controlan por separado.

## 3. Trazabilidad completa: seis filas de la tabla original

| Ref. | Persona o contexto de fuente | Alcance permitido a diseñar, NO permisos activos |
| --- | --- | --- |
| T01-R01 | Visitante del SaaS | Landing, ofertas, testimonios moderados, demostraciones y contratación; datos públicos seguros, SEO/consentimiento. Futura edición editorial SuperAdmin autorizada |
| T01-R02 | SuperAdmin de plataforma | Empresas, planes, contratos, métricas, dominios, seguridad y soporte en consola global; MFA resistente al phishing, segregación de funciones y acceso a PII privada únicamente excepcional/auditado |
| T01-R03 | Propietario de negocio | Marca, sucursales, empleados, permisos, catálogo, reservas y operaciones **solo de su tenant**; paso reforzado para finanzas, actos destructivos o datos sensibles |
| T01-R04 | Empleado | Tareas y recursos solamente por acción, tenant, sucursal y asignación; sin finanzas ni expedientes ajenos por defecto; revocación inmediata |
| T01-R05 | Cliente final | Reservas, compras, cotizaciones, consentimiento, valoraciones e historial **propio** y **contextual al negocio**; sin mezcla de historiales entre tenants |
| T01-R06 | Centro de demostración | **No es una persona ni una consola con permisos reales.** Escenarios por sector y rol, escritorio/tablet/móvil, exclusivamente datos sintéticos sin credenciales ni cobros reales |

No equivalen a seis grupos de autorización ni a seis aplicaciones independientes. Una cuenta humana podrá tener más de una membresía/contexto, pero ninguna se hereda por analogía: backend resuelve identidad, pertenencia y acción; ante ambigüedad no otorga acceso.

## 4. Matriz: seis experiencias y sus actores

| Experiencia canónica | Actor previsto | Ámbito | Límite de seguridad no negociable | Capítulo de implementación |
| --- | --- | --- | --- | --- |
| E01 Landing pública SaaS | Visitante público | Plataforma pública | Nada privado; catálogo demostrativo con datos sintéticos | C02-03/C20 |
| E02 Registro/contratación de empresas | Visitante futuro propietario | Plataforma, alta controlada | Sin tenant privilegiado antes de verificación/alta, consentimiento y validación | C05/C08 |
| E03 Consola central SuperAdmin | Personal institucional autorizado | Plataforma global | MFA, privilegio mínimo, motivo/tiempo y auditoría en acceso excepcional a PII | C05/C06/C08 |
| E04 Panel propietario/negocio | Propietario autorizado | Tenant, sucursal y recurso | No consulta ni administra otro tenant; derechos efectivos en backend | C02-04/C05/C06/C09 |
| E05 Puesto del empleado | Empleado asignado | Tenant, sucursal y acción | No hereda finanzas ni ve expedientes ajenos; revocación | C02-04/C05/C06 |
| E06 Sitio/portal consumidor | Visitante/consumidor contextual | Sitio tenant público + historial privado propio | Publicación segura y propiedad verificada; nunca unir historias de empresas | C02-03/04/C05/06/C17/C20 |

El centro de demostraciones de T01-R06 es un **modo ilustrativo transversal**, no E07 ni una identidad especial que atraviese datos reales. No existe selector de empresa en login normal; ante resolución ambigua, denegar y solicitar el sitio oficial del negocio (precedencia `AUTO-001..012`, `G.6`).

## 5. Contextos, límites y dependencias de seguridad

- **Plataforma (public/global):** marketing público distinto de consola central; la facultad de administrar la plataforma no implica leer expedientes privados.
- **Empresa/tenant:** identidad institucional y datos tenant separados; su nombre, logo, encabezados, pie, CTA y parámetros tipados serán editables por actor autorizado según la matriz PAR-01..PAR-10 de C01; no existen aún editores ni publicación administrativa.
- **Sucursal y propiedad:** un empleado o propietario no recibe permiso por conocer un ID; cada acción se evaluará en backend con tenant, membresía, rol/acción, sucursal, asignación y propiedad de recurso.
- **Consumidor:** historial/consentimiento propio y contextual; no se filtra por cache, URL, QR, header, borrador, exportación ni bitácora.
- **Demostración:** aislada de datos, credenciales, cargos, sesiones y medios reales; visualmente reconocible y accesible.
- **Tenancy compartido/dedicado:** misma política de autorización; ubicación del dato decidida por catálogo protegido en backend, nunca por navegador, URL o cookie.
- **Seguimiento G.7:** futuro tablero por rol de expedientes con historia inmutable; cliente ve solo eventos publicados propios, empleado solo lo asignado y propietario su ámbito. Pruebas `UC-036..UC-040` y `TEN-001/002/008/009/010/021/032` quedan en capítulos que implementen esas superficies.

## 6. Evidencias y negativos exigidos a C02-01

La entrega es **inventario documental trazado**, no API privada. Ejecutar pruebas de origen/cobertura/consistencia en CI; pruebas funcionales de autenticación, BOLA/IDOR, MFA, caché, eventos y bases de datos privadas **NO EJECUTADAS en C02-01 por ausencia de tales superficies**: obligatorias y bloqueantes cuando se implemente cada función.

| ID | Criterio positivo documental | Negativa obligatoria al diseñar/programar después |
| --- | --- | --- |
| NEG-01 | Distinguir E01–E06 de actores y de demo | No crear «persona demo» con permisos privados |
| NEG-02 | Contexto tenant automático desde identidad/host verificados | Rechazar selector de empresa durante login normal o `tenant_id` del cliente como autorización |
| NEG-03 | Consola global sin PII privada por defecto | Rechazar bypass SuperAdmin sin MFA, motivo, tiempo y auditoría |
| NEG-04 | Propietario limitado a su tenant y recursos | Rechazar IDOR y enumeración horizontal de otro tenant |
| NEG-05 | Empleado limitado a sucursal, acción y tarea | Rechazar expedientes ajenos, finanzas por defecto y sesiones revocadas |
| NEG-06 | Consumidor ve solo su historia y consentimiento | Rechazar mezcla entre tenants, exposición de borradores y cachés cruzadas |
| NEG-07 | Demo solo datos sintéticos | Rechazar credenciales, cobros, llamadas a datos reales |
| NEG-08 | API valida host, membresía, rol/acción, sucursal y propiedad | Rechazar confiar en `X-Tenant-Id`, URL, claims no validados o guard frontend |
| NEG-09 | Shared/dedicated siguen la misma política | Rechazar DSN, placement o ruta de datos elegidos por frontend |
| NEG-10 | Experiencias accesibles y personalización tipada | Rechazar HTML/CSS/JS/SQL arbitrarios, controles sin nombre/teclado o movimiento forzado |

## 7. Evidencia, límites de implementación y rollback

| Capa | Efecto en C02-01 | Trabajo posterior |
| --- | --- | --- |
| `frontend/` Angular 22 | Sin rutas privadas, componentes o cambios UI | Shells lazy, accesibilidad y estados en C02-03..08 |
| `backend/` .NET 10 | Sin endpoints ni permisos nuevos | Proyección autorizada en backend al implementar experiencias |
| `database/` MySQL 8.4 | Sin migraciones ni datos reales | Modelar pertenencias, layouts, integridad multiempresa en capítulos adecuados |
| `qa/` | Extractor SHA/Word y auditorías A/B/C documentales, con negativos de cobertura | Pruebas reales API y E2E cuando exista superficie |
| `docs/` | Inventario, fronteras, trazabilidad completa y decisiones abiertas | Evolucionar solo al cerrar microfase por PR y CI |

**Puerta C02-01:** comprobar 11 párrafos, 6 filas originales y anexos; seis experiencias, cinco tipos de actor más demo, prohibiciones y límites; pruebas reales A/B/C de inventario en CI; revisar diff, SHA, ausencia de P0/P1 atribuibles a este alcance y CI postmerge; acta posterior. **NO certificar el capítulo 02**, ni declarar pasos C02-02..09 construidos. Reversión: revert del PR de esta microfase en CRECISOL, sin migraciones ni efecto en producción.
