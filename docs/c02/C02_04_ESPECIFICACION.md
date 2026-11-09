# CRECISOL · C02-04 — Arquitectura de navegación privada propuesta

**ESTADO: DISEÑO EN DESARROLLO; C02-04 NO CERRADA.** C02-01, C02-02 y C02-03 están cerradas técnicamente (**3/9**). Esta especificación **no** implementa identidad, API privada, autorización backend, rutas de negocio ni la futura configuración administrativa. **Capítulo 02 NO CERTIFICADO; únicamente C01 certificado (1/37); C03 BLOQUEADO.**

## 1. Fuente, precedencia y alcance

- Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, capítulos y anexos de C02 verificados por `qa/c02_01_source.py`: 11 párrafos originales P01–P11, seis contextos T01-R01–R06, flujo P03–P09 y precedencia «login habitual **sin selector** de empresa». No modificar Word ni reinterpretar 8 puertas normativas de proceso MAE-C02-01..08 como 9 microfases operativas.
- [Mapa de 18 necesidades](C02_02_MAPA_NECESIDADES.json): seis experiencias E01–E06, necesidades N01–N18 y riesgos; el presente trabajo solo trata **E03–E06 / N07–N18**, con dependencia histórica intacta de E01/E02. [Sitemap público C02-03](C02_03_SITEMAP.json): cuatro rutas públicas reales (`/`, `/experiencias`, `/demostraciones`, `/alta-empresas`).
- **Puerta anterior comprobada:** [Acta C02-03](C02_03_ACTA_MICROFASE.md), PR #34 y PR #35, integración de `dev` `e6329e874ae44721dda0bfa2eb9a30acfaf0b990` y **seis GitHub Actions postmerge SUCCESS** ([C02-03 #42626138512](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42626138512), C02-02, C02-01, C01-07, C01-08, Foundation).
- [Contrato C02-04 en JSON](C02_04_NAVEGACION_PRIVADA.json) es **artefacto derivado de diseño**, no importable como rutas de ejecución: 4 contextos, 12 entradas vinculadas a N07–N18, 12 riesgos adversariales DEN01–DEN12 y reglas de configuración futura. El sitemap público permanece intacto.

## 2. Mapa de contexto privado y 12 tareas propuestas

**Los paths de esta tabla son candidatos reservados, NO URLs operativas**. Actualmente cualquier navegación a ellos debe llegar exclusivamente al 404 seguro de Angular, sin consultar contenidos privados, invocar APIs nuevas ni aceptar parámetros de tenant. No hay `canActivate` ficticio ni selector de empresas.

| Contexto y responsable | Ruta simbólica futura | Necesidades del maestro | Vista/tarea prevista, condicionada al backend |
| --- | --- | --- | --- |
| **PV01 — E03 SuperAdmin** (T01-R02, P01/P06/P11) | `/consola` | N07, N08, N09 | Indicadores institucionales agregados sin PII; configuración SaaS versionada; soporte excepcional MFA, motivo, caducidad, revocación y bitácora |
| **PV02 — E04 Propietario** (T01-R03, P01/P06/P08/P09/P11) | `/negocio` | N10, N11, N12 | Marca, encabezado, footer y contenidos editables; sucursales/empleados dentro de pertenencia; pedidos, indicadores y expedientes **solo propios** |
| **PV03 — E05 Empleado** (T01-R04, P06/P07/P08/P11) | `/trabajo` | N13, N14, N15 | Cola asignada por sucursal; seguimiento cronológico inmutable; perfil, sesión y permisos efectivos |
| **PV04 — E06 Consumidor** (T01-R05, P03/P04/P05/P06/P07/P08/P09) | `/mi-cuenta` | N16, N17, N18 | Enlace al catálogo **público publicado** de dominio verificado (N16, no es dato privado), solicitudes propias y comprobantes/historial **solo propios** (N17–N18) |

Los 12 nodos `NAV01..NAV12` incluyen `route_key` estable de diseño, `proposed_path`, `need_ref`, fuente P/T original, `dependency_chapters`, situación `not-implemented`, criterio `given/when/then`, caso negativo, riesgo P0 y **comprobaciones de backend obligatorias**. La entrada N16 es una transición pública futura —catalogación verificada y filtrada de publicaciones— y **nunca puede convertirse en autorización de recursos privados** por aparecer en un menú contextual.

## 3. Máquina de decisión de autorización futura

La visibilidad del menú es solo **presentación**; no autentica, no autoriza, no garantiza existencia de recursos, ni produce un token confiable. En cada lectura/escritura/descarga/exportación el backend deberá comprobar, en este orden lógico:

1. **Identidad y sesión verificadas** (firma/cookie segura, expiración, revocación, MFA/step-up cuando aplique). El login habitual **no contiene selector de empresas**. El contexto se infiere de identidad, host verificado y membresía, o se niega si es ambiguo.
2. **TenantContext resuelto exclusivamente en servidor**, con hostname/dominio verificado, membresía y asignaciones vigentes. `tenant_id`, claim, cookie, header, slug, QR o parámetro URL **no son autoridad**. Backend decide shared/dedicated según catálogo protegido; cliente no selecciona DSN.
3. **Acción y recurso**, con RBAC/ABAC backend específico: rol permitido, sucursal, recurso propio/asignado, propósito y estado. No existe rol «empleado» con acceso financiero implícito. Consumidor solo a transacciones/archivos propios y de su negocio.
4. **Privacidad, caché y logs**: proyecciones, índices, archivos, previews, realtime, backups y auditorías separados por tenant, sucursal, recurso y usuario. No cachear ni reutilizar la respuesta de un tenant después de un cambio de contexto o revocación.
5. **SuperAdmin excepcional**: panel global de plataforma con agregados; ningún acceso privado a expedientes o PII por defecto. Solo con MFA/step-up, motivo autorizado, tiempo corto, revocable y registro auditado independiente.
6. **Fallo seguro**: ante falta de sesión, permiso, host, pertenencia, recurso, publicación o consentimiento se deniega sin revelar ni enumerar recursos de otros. Un guard Angular puede mejorar UX cuando existan rutas; **no sustituye una prueba backend**.

Cualquier ruta privada que se implemente en C05/C06 y capítulos funcionales deberá incluir su política backend y pruebas reales BOLA/IDOR, claims forjados, alteración de URL, revocación, sucursal, medios, multitenancy shared/dedicated y acceso directo a API. **Ninguna de esas pruebas de explotación se considera ejecutada por una especificación.** En C02-04 la garantía operativa verificable es más modesta: las rutas privadas **no se registraron ni se sirven**.

## 4. Parametrización real prevista, nunca valores comerciales rígidos

Toda **presentación** del menú será administrable desde el panel con configuración versionada por plataforma, empresa, sucursal y usuario, respetando prevalencia, herencia, permisos y contexto autorizado. Deben poder administrarse **label, descripción, icono por token permitido, grupo, orden, visibilidad, locale, ayuda y preferencia de inicio**, con previsualización, publicación, auditoría, versiones y rollback. Logos, encabezados, pies de página y datos comerciales provendrán de las configuraciones publicadas de ámbito correcto; este contrato **no almacena hardcodes de branding** ni publica datos tenant.

La matriz de **roles/permisos efectiva también deberá poder gestionarse desde el backend administrativo autorizado**, con mínimo privilegio, separación de funciones, MFA cuando corresponda, auditoría y revocación inmediata. Un usuario/empresa jamás puede editar por configuración los invariantes: pertenencia del tenant, propiedad del recurso, host validado, política backend de acceso, destino de DB o `route_key` que apunte a código arbitrario. «Todo parametrizable» no significa «un campo de menú puede otorgar acceso a otra empresa». Configuración publicada como JSON validado: **no inyectar HTML/JS/CSS/SQL, URL dinámica o código**. No incluir controles aparentes de configuración en frontend sin casos de uso y seguridad implementados.

## 5. Matriz adversarial y pruebas exigidas

La tabla siguiente define **expectativas futuras** en backend, no tests de un backend privado inexistente.

| Control | Amenaza | Resultado exigido |
| --- | --- | --- |
| DEN01/DEN02 | Host, slug, query, claims, IDs o tenant alterados; BOLA/IDOR | Bloquear acceso horizontal incluso vía URL directa |
| DEN03 | SuperAdmin intenta leer PII sin MFA/motivo/plazo/auditoría | Denegar sin filtración o privilegio implícito |
| DEN04/DEN05 | Empleado revocado o consumidor abre archivos ajenos | Denegar y no recuperar cachés/sesiones anteriores |
| DEN06/DEN07 | Configuración inyectada o cambio de contexto con sesión vieja | Validación estructurada, respuesta fresca, sin elevar privilegios |
| DEN08/DEN09 | DSN manipulado o host de un negocio no publicado | Catálogo protegido; solo contenido público publicado |
| DEN10/DEN11 | Guard frontend sorteado, demo sobre datos reales | API niega; demo solo sintética, sin cobros reales |
| DEN12 | Acceso por URL a rutas privadas aún no creadas | Wildcard de Angular responde exclusivamente 404 y no expone contenido |

**A — funcional/trazabilidad:** comprobar SHA del Word, párrafos/roles, correspondencia exacta de 12 necesidades N07–N18, 4 contextos E03–E06, ruta simbólica única y dependencias; mutaciones adversariales (ruta duplicada, ID falso, fuente falsa, experiencia cambiada) deben fallar.

**B — seguridad:** validar contratos `required_backend_checks` por experiencia; prohibir permisos frontend/selector, acceso privado sin backend, PII global implícita, cross-tenant, cobros, demos reales y configuración arbitraria; probar mutaciones de backend/tenant/MFA/propiedad/configuración; **ejecutar navegador Chromium contra las rutas privadas inactivas**.

**C — arquitectura/UX/rendimiento:** separación frontend/backend/database y permisos, parametrización futura segura, orden de precedencia, estructura de menú accesible (teclado, lector de pantalla, 320px, foco, reduced motion) sin UI privada fingida; conservar el sitemap y la landing pública, build Angular 22 y .NET 10, auditoría de dependencias, regresión Playwright C02-03 y auditoría de C02-01..03. Comprobar que el 404 de rutas propuestas no produce datos privados. No existe benchmarking de menús privados: aún no hay ejecución de esos menús.

## 6. Impacto autorizado, estado y reversión

| Área | Cambio en C02-04 | Lo que **NO** se está haciendo |
| --- | --- | --- |
| `frontend/` | Pruebas negativas de navegador sobre rutas no habilitadas (si se añaden) | Ni nuevas rutas, ni shells, ni guards engañosos, ni login |
| `backend/` | **Ninguno** | No API privada, JWT, MFA, permisos RBAC/ABAC ni DTOs |
| `database/` | **Ninguno** | Ni tablas, ni migraciones, ni tenants de muestra |
| `docs/`, `qa/`, CI | Especificación, JSON derivado, pruebas positivas/negativas de contrato, auditorías A/B/C y evidencia | No implementar antes de C05/C06 ni desplegar |

**Rollback:** revert del PR técnico C02-04 y del PR posterior de acta en `dev`, sin operaciones sobre datos ni producción. Sin escrituras en `main` o en SOLQARYN.

**Puerta de cierre técnico:** ejecutar A/B/C y mutaciones + Playwright de rutas inexistentes, Angular22/.NET10, diff/secretos y CI último HEAD, fusionar a `dev` con SHA esperado, comprobar CI postmerge y redactar acta separada antes de avanzar de **3/9 a 4/9**. C02-05 estados universales solo será **habilitable después**. Capítulo 02 sigue NO CERTIFICADO y C03 bloqueado.
