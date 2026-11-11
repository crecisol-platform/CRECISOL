# ADR-015 — Cloudflare: almacenamiento de archivos y multimedia de CRECISOL

**Fecha de la decisión del propietario:** 11 de noviembre de 2026.
**Estado:** DIRECTRIZ APROBADA / NO IMPLEMENTADA.
**Alcance autorizado:** documentación y diseño; **NO** crear buckets, cuentas, tokens, despliegues, contratos, compras, uso de producción ni cobros.
**Autoridad:** Documento Maestro Único CRECISOL, capítulos C21 (Cloudflare R2: imágenes, videos, documentos y archivos) y C32 (Cloudflare R2 y gestión segura de todos los archivos), seguridad multitenant y panel administrativo parametrizable.
**Identidad y alcance:** solo solqaryn-platform/CRECISOL. SOLQARYN es únicamente referencia de lectura.

## Decisión de negocio y restricción de almacenamiento

**Todos los archivos cargados en tiempo de ejecución por administradores y usuarios de CRECISOL se alojarán en Cloudflare**: logotipos de empresa, fotografías, imágenes de productos, documentos, contratos, facturas, evidencias de expedientes, adjuntos y videos. Ningún binario de usuario se conservará de modo permanente en el repositorio, frontend, backend o MySQL (sin BLOB, base64 o videos en tablas).

**MySQL almacenará la referencia estable del objeto y los metadatos mínimos de control**; guardar solamente una URL pública permanente no permite revocación ni aislamiento privado. Las URL de acceso privado se emitirán temporalmente desde referencias y permisos verificados. El panel administrativo controlará todas las operaciones de carga, búsqueda, organización, publicación, sustitución, versiones, cuotas y eliminación con autorizaciones del backend.

## Proveedores y decisiones pendientes de costo

- **Cloudflare R2:** almacenamiento de objetos como imágenes, PDFs, adjuntos y originales de video. Bucket privado por defecto, claves opacas y acceso directo de carga con URLs temporales firmadas. Descargas públicas solo si expresamente publicadas; privadas siempre autorizadas.
- **Cloudflare Stream:** opción futura para videos que requieran transcodificación/reproducción adaptativa o visualización embebida. Si también se conserva el original en R2, evaluar copia duplicada, retención y precio. Stream cobra por minutos almacenados/entregados; no se activará ahora.
- **Cloudflare Images/transformaciones:** opción futura para optimización, variantes y miniaturas, condicionada a política de costos, privacidad y necesidad real.
- El propietario ha elegido el proveedor Cloudflare, **no una compra ni un servicio de pago concreto**. La elección final de Stream/Images y sus costos requiere revisión explícita en la fase competente.

## Modelo de referencia futuro, no migración ni DDL ahora

La entidad de metadatos tendrá un identificador opaco de recurso, empresa o scope PLATFORM autenticado, identificador del dueño/registro, categoría de medio, proveedor, bucket y clave de objeto o UID de Stream, tipo MIME verificado por contenido, tamaño real, huella, nombre sanitizado, estado de validación y publicación, versión, visibilidad, usuario autorizante y fechas. Agregar duración, relaciones de thumbnails, retención y borrado si corresponden.

No persistir URL presignada temporal, token de reproducción, credenciales Cloudflare, URL externa arbitraria ni contenidos binarios. El backend resolverá la URL entregable según autorización y vigencia. Las referencias internas no se consideran permiso de lectura por sí mismas.

## Flujo de upload y consulta

1. Administrador o usuario autenticado inicia la carga en el panel. Backend comprueba tenant real, rol, membresía, registro propietario, tipo de documento, sucursal cuando aplique, cuota, tamaño y límites del plan.
2. Backend asigna identificador opaco y genera autorización temporal limitada a una única clave de destino. El navegador sube el archivo directamente a Cloudflare; ninguna credencial permanente se entrega al cliente. Para archivos grandes, planificar carga multipart reanudable.
3. Se verifica la subida real, tamaño, tipo MIME por contenido, hash y reglas antimalware. El objeto permanece en cuarentena si procede y **solo luego de validarse** pasa a activo y se confirma la referencia en MySQL. Limpiar cargas huérfanas.
4. Para lectura pública aprobada: generar la URL de entrega/CDN y servir variantes optimizadas. Para lectura privada: autorizar cada solicitud desde backend, emitir enlace de corta vida, evitar caché compartida y registrar accesos relevantes. No almacenar el enlace temporal como fuente de autoridad.
5. Para reemplazar/borrar: versionado, auditoría de usuario y entidad, invalidación de caché, retiro de accesos y retención legal aplicable, con manejo de fallos parciales y comprobación de eliminación.

## Seguridad transversal obligatoria

- Aislamiento tenant en metadatos, objetos, vistas previas, thumbnails, caches, streaming, auditorías, colas, eventos, backup y exportación. **Un prefijo de carpeta no sustituye autorización del backend.**
- Pruebas negativas: IDOR/BOLA, URL expirada o reutilizada, suplantación de tenant, MIME falso, SVG activo/XSS, macro, malware, SSRF, ZIP bomb, tamaño/cuota excedida, fuga por CDN, URLs adivinables, carga cruzada, sobreescritura, huérfanos, carreras de borrado/lectura, revocación y acceso indebido del superadministrador.
- Configuración por tipo de archivo, tenant, paquete/plan, perfil, finalidad y retención: extensiones permitidas, tamaños máximos, cantidad, duración de video, permisos de descarga, privacidad, versión y eliminación. Nunca confiar en el nombre/extensión proporcionados por cliente.
- Probar con auditores A (trazabilidad y negocio), B (adversarial multitenant), C (arquitectura, UX y rendimiento real) **solo cuando llegue el capítulo autorizado**. Este documento NO demuestra pruebas de carga ni integración ya ejecutadas.

## Rendimiento, administración y activos históricos

Guardar referencias evita inflar MySQL y no exige que backend procese continuamente archivos pesados. **Seguirán existiendo bytes de red para imágenes/videos**: prever CDN, formatos optimizados, tamaños adaptativos, caché privada/pública separada, lazy loading y streaming adaptativo cuando sea rentable. No prometer cifras sin benchmarks.

Panel SuperAdmin: políticas globales parametrizables, catálogo de tipos, límites por paquete, cuota por tenant, almacenamiento por proveedor, roles/permisos y registros. Panel empresa: solo sus archivos, logos, multimedia y documentos dentro del plan; administradores autorizados gestionan altas, sustituciones y bajas.

**Excepción histórica acotada:** las imágenes oficiales extraídas del Word canónico para compilación de C01/C02 constituyen activos estáticos aprobados, no archivos cargados por usuarios. Mantenerlos y preservar sus hashes hasta una migración real de branding dinámico validada en la microfase que corresponda; esta decisión no los elimina ni reescribe el Documento Maestro.

## Secuencia y no activación anticipada

- C03: únicamente registra esta arquitectura transversal; no implementa el módulo de archivos.
- C04 en adelante: conservar aislamiento multitenant y permisos al introducir entidades/rutas.
- C21 y C32: capítulos previstos por el Documento Maestro para ejecutar Cloudflare R2 y seguridad integral de archivos. Su diseño detallado reconcilia esta directriz con versiones vigentes de R2, Images/Stream, tarifas y reglas aplicables.
- NO se modifican frontend, backend, database ni DOCX/PDF; NO se crean servicios de Cloudflare. Sin despliegue, VPS o cobros. C03 sigue NO CERTIFICADO y C04 BLOQUEADO hasta aceptación formal del capítulo.

## Referencias oficiales consultadas (versión vigente a noviembre 2026)

- R2 URL firmadas: https://developers.cloudflare.com/r2/api/s3/presigned-urls/
- R2 carga simple/multipart: https://developers.cloudflare.com/r2/objects/upload-objects/
- Stream cargas de video: https://developers.cloudflare.com/stream/uploading-videos/
- Stream videos privados: https://developers.cloudflare.com/stream/viewing-videos/securing-your-stream/
- Images precios: https://developers.cloudflare.com/images/pricing/
- Stream precios: https://developers.cloudflare.com/stream/pricing/

**Estado de salida ADR-015:** concepto aprobado por el propietario; implementación, costos, políticas de retención y pruebas siguen pendientes. No atribuir capacidades operativas inexistentes.
