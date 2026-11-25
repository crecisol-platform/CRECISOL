# C06-09 — Descargas, exportaciones y bitácoras protegidas
Estado: CANDIDATA TÉCNICA NO CERTIFICADA. Base dev da67bfe38940edd25842b2f963090e3d1406ce45. Prerrequisito C06-08 9.5/10, POST técnico 17/17 y documental 6/6 SUCCESS. Maestro Word/PDF y catálogo canónico inalterados. Alcance local sintético exclusivamente CRECISOL.

## Inventario y decisión de activación
| Superficie | Estado y protección |
|---|---|
| identity/profile/photo | POST propio, tenant y cuenta autorizados en transacción; contenedor persistido vuelve a validarse y reconstruirse como PNG RGBA 64x64, sin metadatos ni URL. |
| platform/profile/photo | POST propio de operador, realm separado, misma validación; no herencia de medios. |
| identity/private/read | ABAC C06-04 bajo bloqueo, devuelve referencia opaca ASCII de máximo 120 caracteres, no URL, ruta, nombre de objeto o capacidad. |
| platform/breakglass/read | C06-06 requiere acceso temporal y MFA fresco; misma restricción de referencia, denegación si contenido inválido. |
| identity/security/events | C06-08 conserva tenant/actor exactos, máximo 50; etiquetas del catálogo o evento neutral, resultado 0/1, fecha; no PII, contexto HMAC, identificadores internos o texto libre. |
| platform/abuse-summary | C06-08 solo agregado de plataforma, sin acceso a bitácora individual. |
| GET /api/v1/private/cases/{resourceId} | Solo harness C05 desechable, NO registrado en Program.cs; no habilitar mediante URL o token. |
| media.private.read/export, finance.report.export | Acciones normativas futuras no registradas, no grantables, sin permisos nuevos. Rutas de medios/descargas/exportaciones/logs no registradas, denegadas incluso con bearer real y firma manipulada. No se afirma implementación de R2, almacenamiento, exportadores, URLs firmadas funcionales ni C21/C32. |

No añadir rutas ni permitir GET anónimo para obtener medios; fotos actuales se entregan por contrato POST JSON privado, con nueva autorización por solicitud. Mantener la UI existente. Los prefijos privados (incluidas rutas aún inexistentes) reciben no-store/private y same-origin desde el ingreso, incluso 400/404/405. No se emite Location ni disposición de descarga en denegaciones.

## Contrato de contenido y diagnóstico
Una sola implementación de raster canónico en Application, sin dependencias nuevas. Infraestructura la usa antes de autorizar entrega; API revalida y proyecta. Solo tres chunks IHDR/IDAT/IEND, CRC comprobado, dimensiones y RGBA8 fijas, tamaño 24,576 bytes, descompresión exacta de 16,448 bytes, filtros cero; se reconstruye el contenedor, jamás se reenvían bytes comprimidos originales. No cambiar esquema ni convertir almacenamiento a cifrado; la foto actual permanece en la columna histórica photo_png. No se atribuye protección contra lectura de un administrador DB comprometido. Al entregar una referencia, se rechaza un valor que no sea identificador alfanumérico con guion o guion bajo; no URL, rutas, caracteres de control o navegación. Los datos históricos malformados se deniegan sin devolver ni borrar evidencia.

Proveedor de log de proceso único: fuente clasificada, severidad y EventId numéricos; nunca formatter, mensajes, estado, scopes, excepción, categoría arbitraria ni datos de solicitud. Resultado HTTP numérico conserva observabilidad; auditoría SQL durable independiente, no se desactiva. Esta garantía se refiere al canal ILogger de la API; no sustituye política de acceso a infraestructura, logs de terceros, volcados o respaldos productivos.

## Pruebas y gates
A: maestro, inventario, no activar permisos futuros, diff exacto; B: contratos reales .NET de raster, referencia, acciones y logging más privacidad C06-07; C: .NET10 Angular22 Node24, separación, regresión de interfaz; D: MySQL8.4 real, runtime autenticado y denegaciones SQL exactas para modificar/leer bitácoras según privilegio, FK y estado de fotos, grants intactos; E: Chromium/Angular/Kestrel/MySQL con todas las regresiones C05-C06-08, fotos reales en ambos realms, contexto cruzado, URL/paths persistidos, rutas desactivadas, query de credenciales y canarios de logs. No aceptar fallos de conexión como denegación; no registrar datos de prueba en diagnósticos.

Rollback: revertir cambios de código del PR en dev; no DDL ni nuevos privilegios ni migraciones de datos. Preservar historial, reabrir bloqueo de C06-09 si falla. Alcance y aceptación 9.5/10 pendientes de ejecución real. C06-10 bloqueada; no certificar producción, cambios de frontend, VPS, SOLQARYN ni main.

### Refuerzo del arnés y frontera futura
La prueba de operador consulta la clave del provisionador como 16 bytes hexadecimales (UNHEX); no confundir su representación con un UUID con guiones. Los contratos .NET incluyen contenedores construidos independientemente con CRC correcto: exceso y defecto de descompresión, filtros inválidos, zlib malformado, metadata extra y datos anexados al flujo comprimido (deben rechazarse o reconstruirse sin los datos anexados). Para C21/C32 y exportadores posteriores, no bastará activar una URL firmada bearer de almacenamiento: una entrega privada deberá autenticar al destinatario y revalidar tenant, membresía, acción, sucursal, recurso y revisión en servidor al entregar el contenido, con caducidad y alcance vinculados a la autorización, sin tokens de sesión en URL ni logs. Es una condición de activación futura, no se presenta como capacidad operativa implementada.

### Primera ejecución PRE
PRE #45696887241: A y D SUCCESS; B falló en la comprobación de imagen positiva, después del roundtrip de raster canónico. La aserción comparaba Base64 contra JSON textual, donde el serializador puede escapar '+'; se separan éxito de la proyección y comparación exacta del campo decodificado mediante JsonDocument. No se elimina ningún negativo ni se cambia la implementación de producto. La hipótesis queda pendiente de confirmación en la nueva ejecución B. El resultado E del primer SHA aún no se atribuye como aprobado.

### Segunda ejecución PRE y preservación de C06-04
PRE #45697214588 SUCCESS A/B/C/D/E: 80 comprobaciones .NET de entrega, 528 aserciones integradas, 574 respuestas HTTP de privacidad, 73 formas exitosas, 148 respuestas de relay de navegador y dos enrolamientos MFA. 15 de 16 workflows aplicables SUCCESS; seis omitidos por condición. El workflow C06-04 #45697214149 aprobó E2E, pero A/B/C fallaron en static_guards por buscar el retorno anterior a C06-09. Se actualiza exclusivamente el auditor: exige denegación ABAC explícita, extracción posterior de la referencia y única entrega condicionada por IsDisplayReference, en ese orden, y prueba cinco mutaciones negativas adicionales. El nuevo auditor C06-09 invoca también el control de orden de C06-04. No se modifica ningún código de producto, permiso ni migración; la nueva revisión requiere aprobación completa, sin atribuirle resultados de un SHA anterior.
