# CRECISOL · C02-05 — Estados universales de interacción

**ESTADO: EN DESARROLLO, 4/9 microfases C02 previamente cerradas. C02-05 no cerrada.** C02 **NO CERTIFICADO**, únicamente C01 certificado (1/37). C03 BLOQUEADO. No crear paneles privados, credenciales, pagos ni confirmaciones de negocio inexistentes.

## 1. Fuente y puertas de entrada

La autoridad es el Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; capítulo 02 (P01–P11, T01-R01..R06, precedencia sin selector de empresa; flujo P03–P09), verificador ejecutable `qa/c02_01_source.py`. Ocho puertas normativas `MAE-C02-01..08` **no son** las nueve microfases operativas `C02-01..09`. Se usan [necesidades C02-02](C02_02_MAPA_NECESIDADES.json), [sitemap C02-03](C02_03_SITEMAP.json), [navegación privada propuesta C02-04](C02_04_NAVEGACION_PRIVADA.json) y [acta C02-04](C02_04_ACTA_MICROFASE.md).

**Puerta técnica anterior:** código y acta C02-04 en PR #36/#37, `dev` SHA `a57dcb2ae7fb533f10beae8afb59656b301318d3`, **siete CI POSTMERGE SUCCESS** incluyendo [C02-04 #42631972825](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631972825), [C01-07 #42631972685](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631972685) y [C01-08 #42631973073](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42631973073). Estas regresiones no se presentan como pruebas de un backend de negocio todavía inexistente.

## 2. Semántica de cinco estados

El [contrato derivado C02-05](C02_05_ESTADOS_INTERACCION.json) define cinco estados `ST01–ST05`, sus disparadores, visibilidad permitida, accesibilidad, recuperación, negativas y relación con E01–E06 / N01–N18.

| Estado | Regla de aparición y accesibilidad | Negativo obligatorio | Implementación presente |
| --- | --- | --- | --- |
| **ST01 Loading / carga** | Empezó lectura nueva; `role=status`, `aria-busy=true`, mensaje breve sin PII | Nunca mostrar resultados o metadatos de otro tenant mientras espera | **Activa** en la página pública informativa |
| **ST02 Empty / vacío** | Solo después de HTTP exitoso **y autorizado**, conjunto vacío del ámbito actual; `role=status` | No confundir 401/403 con vacío ni enseñar conteo de otra sucursal | **Modelo y presentación reutilizables**, sin datos privados operativos |
| **ST03 Error** | API fallida, publicación no válida o acceso denegado; `role=alert` y reintento explícito idempotente | Nunca reutilizar snapshot previo ni mostrar cuerpo interno del error | **Activa** en la página pública informativa |
| **ST04 Offline** | Falló la lectura y el navegador informa `navigator.onLine=false`; `role=alert` | No interpretar aviso del navegador como permiso, caché privado o confirmación de guardado | **Activa** como pista de conectividad de la página pública |
| **ST05 Confirmation / confirmación** | **Solo futura** después de respuesta de persistencia autorizada y comprobable en backend (referencia, versión, consentimiento si aplica); `role=status` | Ningún éxito optimista antes de commit/acuse del servidor ni cobro no ejecutado | **Modelo y presentación reutilizables**, sin acción transaccional |

`ready` es la condición de contenido fresco, publicado/autorizado, **no** un sexto estado de fallback. Los cinco estados no son servicios operativos: `empty` y `confirmation` son presentaciones listas para recibir una fuente validada en capítulos posteriores, sin que exista hoy una pantalla que los active. La página pública actual usa solo `loading`, `error` y `offline`.

## 3. Separación de contexto y transición segura

La máquina de estados distingue **solicitud**, **resultado vacío autorizado**, **error**, **aviso de red**, **confirmación real** y **cambio de contexto**. En cada nueva lectura se limpia la vista anterior. Error, timeout, JSON malicioso, desconexión y cambio de ruta **no deben mostrar datos anteriores de otro tenant**, ni reutilizar títulos/metas/favicon de la publicación previa. Al reconectar, el mensaje cambia, pero **no se declara conexión/autorización exitosa ni se rellenan datos**: es necesario un **nuevo GET validado**.

El servicio `PublicBrandService` mantiene un único GET same-origin `/api/v1/public/branding/platform`, sin URL de tenant, sin token, sin cabecera arbitraria y sin persistencia local; nunca permite retorno de snapshot previo después de fallo. C02-05 **no altera el endpoint ni modifica C01**. La señal de conectividad es solo UX, no evidencia de red confiable.

La navegación futura E03–E06 debe revalidar identidad, sesión, dominio/tenant, pertenencia, rol, acción, sucursal, asignación, propiedad de recurso y estado de revocación en **backend** para toda acción, archivo, exportación, cache, evento y bitácora. El menú o guard Angular nunca otorga permiso. SuperAdmin no obtiene PII privada por defecto; acceso excepcional requiere MFA, motivo, caducidad y auditoría. Shared/Dedicated seleccionados por catálogo backend, no URL. BOLA/IDOR y escenarios privados **no probados aquí** porque no existen endpoints privados.

**Reintento:** acción explícita con control single-flight, limpia estado previo, conserva la navegación pública, y solo recupera vista después de un snapshot fresco que pase el parser. No se reinicia envío de una compra/pago; esa idempotencia se diseñará y probará en su capítulo.

## 4. UI reusable y parametrización

Se introduce el modelo tipado `frontend/src/app/ui/interaction-state.model.ts` y componente Angular standalone `InteractionStateComponent` (HTML/CSS local) con cinco variantes, texto **interpolado** (no HTML ejecutable), controles de teclado, alto táctil mínimo 44 px, foco visible, diseño desde 320 px, movimiento reducido, jerarquía visual clara y sin nuevas dependencias. El componente solo recibe **un modelo de presentación permitido**; no consulta API ni realiza navegación, autorización, escritura, facturación o decisiones de acceso. El host controla anuncio `role=status/alert` y callback de reintento. La integración real se limita a la rama de error/carga de `PublicInformationComponent`, sin modificar las cuatro rutas allowlist, landing C01, backend ni base de datos.

**Parametrización futura integral:** título, descripción, etiqueta de acción, idioma y variaciones gráficas deben provenir de configuraciones publicadas/versionadas por `platform/tenant/branch/user`, administrables bajo backend seguro y auditadas con preview/aprobación/publicación/rollback. **No incrustar copy comercial tenant, PII, menús ni permisos en componentes**. Los textos genéricos accesibles de recuperación de la plataforma actual son fallback técnico exclusivamente; la implementación administrativa de copy todavía no existe y **no se afirma que ya sea editable**. Los tokens de estado, titularidad del recurso, identidad, reglas de permiso y prueba de transacción nunca pueden ser editados por el cliente. Se rechazan scripts, HTML/CSS/JS/SQL arbitrarios y configuraciones con ejecución.

## 5. Verificaciones y auditorías

**A — funcional:** SHA del Word, 11 párrafos originales, seis personas/contextos, seis experiencias con N01–N18, cinco estados exactos ST01–ST05, transiciones, contrato y dependencias anteriores. Mutaciones de estados duplicados, falta de estado, fuente alterada y vacíos no autorizados deben FALLAR.

**B — seguridad:** código/browser verifican no snapshot previo ante timeout/inyección, ausencia de PII, metadatos limpios, offline sin reutilizar cache, único endpoint, retry single-flight, ninguna ruta privada o confirmación transaccional. Mutaciones de `confirmation` sin acuse, `error` con stale, `offline` como auth, `empty` después de 403, permisos cliente deben FALLAR. **No** reclamar pruebas de backend MFA/BOLA/IDOR sobre funciones no implementadas.

**C — arquitectura/UX:** componentes standalone con design system propio, texto interpolado, host correcto `role=status/alert`, keyboard/foco/WCAG automatizado, 320 px, reduced motion, Angular22 typecheck/build/assets, .NET10 Release sin cambios, dependencias, Playwright de rutas C02-03/04 y estados C02-05; protección de cierres previos y de 4/9 mientras falte acta.

**Impacto:** `frontend/src/app/ui/` (nuevo patrón), `frontend/src/app/pages/public-information.component.{ts,html}` (uso público), `frontend/tests/` (nuevos negativos), `docs/` y `qa/` + workflow. **Ningún cambio en `backend/`, `database/`, contratos API/DTO ni migraciones.** No activar demo con datos reales, formularios de negocio, login, cobros, VPS, producción o SOLQARYN. Revisión de PR, secrets, SHA, A/B/C y CI pre/postmerge son obligatorias.

**Cierre y rollback:** no pasar de **4/9 a 5/9** hasta fusionar implementación probada, CI postmerge y PR independiente de acta con auditorías finales. Revert de ambos PR en `dev` restaura exactamente los fallbacks públicos C02-03/04; sin rollback de datos o producción. Siguiente C02-06 solo quedará habilitable, no iniciado. C02 continúa NO CERTIFICADO y C03 bloqueado.
