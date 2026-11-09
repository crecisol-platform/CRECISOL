# C01-07 — Regresión integral de identidad pública, seguridad y separación de tenant

**Estado inicial:** EN DESARROLLO; no cerrar hasta CI real del último HEAD, auditorías finales A/B/C, revisión, merge y CI post-merge. **C01 6/8, capítulos 0/37 certificados.**

## Autoridad y entrada

Documento Maestro Único: docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e; cotejar mediante qa/c01_verify_source.py en CI. Matriz C01 P04 (aislamiento), P06 (URL/ID no autorizan), P07 (configuración como datos), P14/P25 (identidad publicada), P28 (host/suplantación) y T03-R02/T03-R04. C01-06 quedó cerrada mediante PR #24 y #25, CI final y post-merge verificadas. C01-08 NO habilitada antes de cerrar esta fase.

## Entregable y arquitectura

Esta microfase añade **regresión ejecutable**, no modifica contrato ni habilita endpoints de tenant: MySQL 8.4 desechable en Docker --network none con socket local; datos sintéticos de PLATFORM publicada, PLATFORM borrador y TENANT privada; usuario SQL limitado a SELECT; servidor ASP.NET Core 10 real y Angular 22 real en el mismo runner con proxy HTTP exclusivo de desarrollo. Chromium consulta la API REAL: no se interceptan ni simulan respuestas. Node 24 instala dependencias desde package-lock; se exige build de producción y presupuesto de bundles.

La API sigue con única ruta pública, sin login/admin ni autorizaciones de consumidor implementadas. Las pruebas negativas de host, autorización falsa, tenant-id, CORS, métodos de escritura, rutas inexistentes y grants SQL acreditan **denegación**, no acreditan la implementación futura de RBAC/ABAC, sucursales o BOLA sobre recursos privados inexistentes. No se crean endpoints o datos privados, ni configuración de despliegue.

## Casos positivos y negativos obligatorios

- Positivas: respuesta de publicación PLATFORM revision 2 y DTO exclusivo; presencia de logotipo exacto, descriptor y SEO en Angular; móvil 320 sin overflow, axe WCAG 2.2 AA automatizado; revisión 3 publicada sustituye datos antiguos; retorno a revisión 2 sin cache persistente.
- Negativas servidor: host manipulado y headers forwarded, query con tenant, Authorization inventada, métodos POST/PUT/PATCH/DELETE, CORS preflight/origin, rutas admin/tenant/preview inexistentes, denegación de UPDATE al lector SQL.
- Negativas datos/navegador: TENANT publicada jamás sustituye PLATFORM; borrador jamás se expone; eliminación de la publicación PLATFORM produce 404 y fail-closed; nombre XSS insertado en la BD es rechazado con 503, sin HTML ejecutado ni metadatos obsoletos.
- Pruebas de regresión .NET: 51 aserciones de contratos y suite Application; Angular build real, assets oficiales byte-exactos y auditorías finales separadas A, B, C.

## Regresión histórica adicional

La CI debe **volver a ejecutar** 11 casos Chromium C01-05 y 9 de C01-06 (incluidos assets aprobados y negativas visuales) con el HEAD vigente, además de los casos de navegador contra MySQL/API reales. Son conjuntos separados: los mocks de las pruebas históricas no se cuentan como la nueva prueba de integración real. La auditoría C exige prueba de ejecución de ambos.

## Gates, riesgos y rollback

**A:** relación matriz, fuentes canónicas y cobertura real. **B:** SQL solo lectura, tenant/draft aislados, pruebas HTTP negativas, XSS y fail-closed. **C:** Angular/.NET/MySQL, presupuesto real del build, Chromium móvil y WCAG automatizada; sin extrapolar a navegadores/lectores de pantalla no probados. Cero P0/P1 de esta entrega, sin desactivar ni reducir pruebas. Las pruebas de CI se ejecutan con cuenta GitHub read-only, sin credenciales productivas; contraseña MySQL efímera generada en runner y no impresa.

Fuera de alcance: autenticación privada y permisos de tenant, dedicated DB real, DNS, VPS, R2, producción, pagos, tráfico real y carga/lectores asistivos manuales. Se mantienen como gates de capítulos posteriores. **Reversión:** revertir PR C01-07 (solo qa, frontend/tests, workflow y documentación); no existe estado productivo persistente.

**Acta final de esta microfase:** pendiente de completar tras pruebas y CI post-merge; ningún PASS se atribuye antes de revisar los logs correspondientes.
