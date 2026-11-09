# CRECISOL · C02-06 — Patrones de formularios y tablas UX

**ESTADO: C02-06 EN DESARROLLO, 5/9 microfases C02 cerradas anteriormente.** Capítulo C02 **NO CERTIFICADO**, únicamente C01 certificado (1/37); C03 bloqueado. Los patrones aquí implementados son **componentes de presentación y un laboratorio sintético exclusivo de CI**, sin publicarse en rutas ni crear APIs, formularios de contratación o datos privados.

## 1. Autoridad y puerta secuencial

- Documento Maestro Único aprobado: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, capítulo 02 (P01–P11, cinco perfiles T01-R01..R06, seis experiencias, contexto sin selector de empresa). Extractor QA real y `docs/MASTER_REFERENCE.md`; **8 puertas normativas MAE-C02-01..08 no son las 9 microfases técnicas C02-01..09**.
- Cadena heredada: [mapa C02-02](C02_02_MAPA_NECESIDADES.json), [sitemap C02-03](C02_03_SITEMAP.json), [propuesta privada C02-04](C02_04_NAVEGACION_PRIVADA.json), [estados C02-05](C02_05_ESTADOS_INTERACCION.json), [acta C02-05](C02_05_ACTA_MICROFASE.md). **Puerta anterior:** PR #38 y #39 fusionados en `dev` SHA `cf751709f4cd5dac5e579dccc50c060bf20ae396`. Ocho GitHub Actions POSTMERGE completadas `SUCCESS`, incluyendo [C02-05 #42641529918](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641529918), [C01-07 #42641530194](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641530194) y [C01-08 #42641529853](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42641529853).
- [Contrato de patrones y seguridad](C02_06_PATRONES_FORM_TABLA.json) deriva E01–E06/N01–N18, cuatro contratos FP01/FP02/TP01/TP02 y 14 escenarios negativos; ningún contrato da permisos. C02-06 se limita a prototipos seguros de UX **reales en Angular para pruebas**, no módulos de negocio.

## 2. Alcance técnico de los cuatro patrones

| Patrón | Función y accesibilidad comprobable | Límite P0 |
| --- | --- | --- |
| **FP01 — campo con borrador local** | `label/for`, ayuda asociada con `aria-describedby`, requerido y límite, feedback `aria-invalid`, `role=alert`, validar sintaxis local sin envío | No crear ni modificar recurso, no `fetch`, formulario `novalidate` con `preventDefault`, no aceptación empresarial |
| **FP02 — prevención de pérdida de edición** | Borrador en signal efímera, `beforeunload` mientras sucio, diálogo nativo `showModal`, cancelar/Escape/descartar; `dirtyChange` notifica host | Un cambio de contexto/schema exige confirmación del host **antes**; al aplicarse, se descarta el borrador anterior por aislamiento; nunca retener datos de otra empresa |
| **TP01 — tabla semántica accesible** | `table/caption/th scope/td`, `aria-sort`, ordenación estable, filtro local, teclado, contenedor con scroll horizontal desde 320 px, estado vacío explícitamente local | No construir consultas/IDs de tenant, no editar/exportar, no interpretar cero filas como HTTP 403 |
| **TP02 — tabla contextual** | `scopeKey` reinicia filtro y orden; filas provistas por el host, con interpolación de texto que no ejecuta HTML | Host debe reemplazar filas con respuesta **autorizada fresca** al cambiar contexto: el componente no autentica ni certifica ausencia de fuga de un backend inexistente |

Archivos de producto reusable, **sin importar ni montar desde el árbol de rutas de producción**:
- `frontend/src/app/ui/patterns/pattern-contract.ts`: tipos inmutables, allowlist de reglas, límite de texto, configuración de columnas, validador puro de sintaxis; estas verificaciones son UX **nunca** validación de backend.
- `draft-form-pattern.component.{ts,html,css}`: Angular22 standalone OnPush, control de borrador/errores, modal nativo, advertencia de salida, limpieza de contexto; texto por interpolación, sin `innerHTML`.
- `table-pattern.component.{ts,html,css}`: tabla de solo lectura, filtro y sort, `aria-sort`, no caché persistente, ninguna petición HTTP.

**No hay hardcodes comerciales en el producto:** los únicos literales sintéticos de ejemplo están en `frontend/tests/pattern-harness/`, que **no** entra al build `production`. En el futuro las etiquetas, ayudas, límites, tokens de validación, rótulos de columna, orden, idioma y copias vacías serán administrables desde configuración versionada `platform/tenant/branch/user`, con previsualización, aprobación, publicación, auditoría y rollback. Las reglas de confianza **NO** son parametrizables por quien edita la UI: tenant, permisos, propiedad del recurso, backend policy, DSN, prueba de confirmación y validación de escritura. No permitir HTML/JS/CSS/SQL arbitrario, scripts, funciones ni expresiones regulares de usuarios.

## 3. Laboratorio de pruebas exclusivo de CI (no producción)

El `frontend/angular.json` conserva `crecisol-web:build:production` con `src/main.ts` y `src/index.html` sin modificación en su entrada predeterminada. La **configuración separada** `crecisol-web:build:pattern-harness` usa `tests/pattern-harness/main.ts`, `index.html` propio, `tsconfig.patterns.json`, salida `dist/crecisol-pattern-harness` y puerto 4201 SOLO con `ng serve --configuration pattern-harness`; sin backend/proxy, sin cambios de rutas, sin activos productivos del negocio. `playwright.patterns.config.ts` arranca el harness y ejecuta ocho pruebas reales del código Angular montado, no HTML imitado.

El laboratorio contiene **solo** códigos ficticios A2/A10/B1, un ejemplo de inyección textual inocuo y botones de intercambio de conjunto/schema. Incluye confirmación de cambio cuando existe borrador, además del diálogo de descarte dentro del componente. Los IDs A/B de este laboratorio no son tenants, sucursales ni cuentas de CRECISOL. **No exponer este harness como ruta pública de plataforma ni publicarlo en producción.** El gate CI verifica además que el bundle de producción no contenga el texto del harness.

## 4. Máquina de interacción, permisos y seguridad

- **Inicio/edición:** el formulario local comienza limpio y solo calcula `draftIssue`. Cualquier `submit` de ejemplo se intercepta y muestra `Formato del ejemplo válido; ningún dato enviado/guardado`. Nunca es confirmación de base de datos, cuenta ni pago. En un módulo real, el backend deberá revalidar valores, permisos, versión de contrato, CSRF/protección equivalente, idempotencia y concurrencia antes de confirmar.
- **Error:** campo requerido ausente, límite excedido, token de identificador inválido o caracteres de HTML producen error accesible y `aria-invalid`. La etiqueta/config se acota y verifica antes de mostrar formulario; Angular escapa todas las celdas de tabla.
- **Descarte:** el borrador sucio activa advertencia de salida; botón de descartar abre diálogo modal reversible; cambiar esquema o contexto limpio es inmediato, pero si hay edición pendiente el host pregunta primero. Tras aceptar un cambio de contexto, no queda texto, filtro ni sort anteriores. La futura navegación privada deberá aplicar la misma guarda **antes de cambiar la membresía**, no suponer que el control frontend autoriza.
- **Tabla:** solo se filtran y ordenan como máximo 200 filas que el host entregó, sin exportación, descarga, API, caché ni selector de empresa. `empty` es la ausencia de coincidencias de la muestra, **NO** sustituye el estado ST02 de una consulta autorizada ni transforma `401/403` en éxito.
- **Seguridad multitenant futura:** el backend valida sesión, host verificado, membresía, rol/acción, sucursal, asignación, propiedad, revocación, medias/previews/cachés/exportaciones/logs y DB shared/dedicated desde catálogo servidor. SuperAdmin no accede a PII privada por defecto y requiere MFA, motivo, expiración y auditoría de excepción. **No existen endpoints privados aquí; no se afirman pruebas BOLA/IDOR/MFA reales**.

Los casos **NEG01–NEG14** del contrato cubren envío ficticio, campo obligatorio, inyección, descartes, cambios de ámbito, lectura cruzada y exposición accidental del laboratorio. Cada negativo conserva su ID y expectativa en el JSON verificable.

## 5. Pruebas, auditorías y gates

**A (trazabilidad/funcional):** comprobar Word original y SHA, seis experiencias con N01..18, FP01/FP02/TP01/TP02, 14 casos negativos, semántica futura, rutas públicas intactas. Mutaciones de fuente, patrón, necesidad/estado y schema deben fallar.

**B (seguridad):** revisar ausencia de backend/DB/producción, prohibición de HTML evaluado, permiso/browser/DSN dinámico, validación input/config, formulario sin submit de negocio, cambios de contexto con confirmación; mutaciones de backend, seguridad PII, confirmación y tenant deben fallar. La prueba real de navegador comprobará XSS/errores/descartes/reemplazo de filas sin llamadas de API.

**C (arquitectura/UI/rendimiento):** producción unchanged `src/main.ts` y rutas allowlist, harness aislado, strict typecheck y build Angular22 producción + harness, .NET10 existente, auditoría de paquetes, Playwright Chromium 19 regresiones previas más 8 del harness, 320px, teclado/modal, axe WCAG 2.2 AA automatizado, diseño sin scroll de documento, reducido movimiento. **Limitación:** no se prueba carga masiva real de tablas privadas ni servidor con RBAC/ABAC.

**Riesgos y rollback:** ningún P0/P1 real detectado puede quedar abierto para cerrar. Para cancelar, revertir el PR técnico y su futura acta en `dev`; no hay migraciones, datos, cambios API ni producción que revertir. `main` intacto, SOLQARYN prohibido.

**Puerta de salida:** PR técnico, auditar A/B/C en último HEAD, pruebas negativas/positivas y CI, revisar diff/secretos, fusionar a `dev` por SHA esperado, comprobar CI postmerge real. Solo entonces crear acta separada y actualizar de **5/9** a **6/9** con pruebas y CI de acta. C02-07 (móvil/accesibilidad) queda siguiente, **no iniciada**, C02 global **NO CERTIFICADO**, C03 bloqueado.
