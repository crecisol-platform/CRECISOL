# C01-08 — Parametrización integral y editabilidad administrativa: contrato transversal

**Autoridad:** Instrucción expresa del propietario de CRECISOL, 9 de noviembre de 2026. Complemento subordinado al Documento Maestro Único y a la matriz C01-01. **Estado: requisitos de aceptación, NO funcionalidad administrativa implementada.** C01-08 certificado dentro del alcance de lectura pública de marca (1/37 capítulos certificados) con aceptación expresa 2026-11-09. C02 puede iniciar su propio ciclo, sin considerarse implementado ni certificado.

## Objetivo obligatorio

TODO dato de producto editable debe poder administrarse por un usuario autorizado de su ámbito, mediante panel administrativo, sin cambiar código ni redeplegar por un cambio editorial. Incluye: logo completo, isotipo/favicon, nombre, eslogan, paleta, tipografía, portada, header, footer, botones, navegación, orden y enlaces, redes, imágenes/fotografías, contenido, datos del negocio, sucursales, horarios, contacto, textos legales, copyright, SEO, configuración funcional permitida y marca de la plataforma o del tenant. La consola institucional solo permite la marca CRECISOL; la consola de cada empresa solo su propia marca y datos, con permisos de backend por actor, rol/acción, tenant, sucursal y propiedad. No existe derecho global a datos privados por ser SuperAdmin.

Guardar un logo debe permitir validación de firma/MIME/formatos/tamaño, control antivirus y cuarentena, variantes seguras sin alterar el original, borrador y vista previa privada, publicación atómica versionada, auditoría de actor y rollback, URLs versionadas y actualización automática coherente en header, hero, favicon, correos, documentos, SEO y demás superficies que correspondan. El SLA y mecanismo de propagación a sesiones abiertas (push, suscripción o revalidación verificable) se definirán y probarán cuando se implemente la publicación. Cachés deben tener claves que incluyan ámbito y revisión, con invalidación completa. No usar CSS, HTML, JS o SQL arbitrario del cliente como contenido ejecutable.

**No es todo libremente editable:** seguridad, autenticación, autorización, validaciones de backend, infraestructura, secretos y reglas de aislamiento multitenant son controles protegidos. Los administradores editan únicamente parámetros tipados permitidos y auditables.

## Estado real de C01-01 a C01-07 y corrección C01-08

El backend expone actualmente solo una lectura de la marca de PLATAFORMA publicada. Existe contrato tipado con revisión, nombre, colores, lema, descriptor, logoUrl, faviconUrl y SEO, y validación de ruta same-origin, pero NO existen pantalla de edición, carga de medios, endpoint de escritura, publicación administrativa, librería de imágenes ni invalidación de caché entre sesiones.

La cabecera C01-06 usaba una ruta fija para el isotipo y el HTML inicial una ruta fija para favicon. **C01-08 elimina esas referencias fijas:** el encabezado utiliza la URL de favicon (o logo) validada del snapshot publicado; el head recibe solamente el favicon de la publicación. El logo completo en hero, nombre en header/footer, colores, lema y SEO ya dependen de la versión publicada. Las nuevas pruebas C01-08 verifican coherencia de cambios tras una nueva consulta al API (recarga), fallos y rechazo de URLs maliciosas. **No prueban actualización instantánea en otras sesiones ni un panel admin inexistente.**

Quedan textos de plantilla codificados en la portada actual: Inicio, Propuesta, Identidad, Conocer la propuesta, títulos de bloques, textos auxiliares, avisos y navegación. Se deben extraer a bloques parametrizables/versionados en su capítulo de editor; no dar por administrables actualmente. La generación del logo semilla desde Word durante el build es solo bootstrap para desarrollo, NO es el editor final.

## Dependencias y capítulos futuros, NO HABILITADOS

| Capítulo | Entregable condicionado | Prueba negativa |
| --- | --- | --- |
| C02 a C07 | Identidades/UX, sesiones, autenticar, autorizar, tenant context, seguridad y privacidad | Usuario sin rol/pertenencia/MFA no publica marca |
| C08 | Consola de gobierno global y ajustes institucionales versionados | SuperAdmin sin acceso por defecto a datos privados |
| C09 | Panel marca por tenant, editor visual allowlist, configuraciones de header/footer, draft-preview-publicado y CAS | CSS/JS/HTML arbitrarios, IDOR, sobreescritura concurrente o preview público |
| C20 | Sitios públicos, navegación, footer, páginas y SEO administrados por ámbito | Borradores y cache de tenant A no aparecen en B |
| C21 | Biblioteca de medios y variantes, MIME real, antivirus y cuarentena | SVG activo, MIME falso, EXIF sensible, SSRF o archivos ajenos |
| C32 | Gestión universal de archivos, cuotas, retención, firmas temporales y rollback | BOLA, URL persistente privada y borrado ajeno |

Cada capítulo futuro deberá contrastarse con el texto vigente del Word, definir una matriz de requisitos y ejecutar pruebas reales. Esta tabla NO anticipa habilitación, no sustituye la autoridad canónica y no certifica funciones futuras.

## Matriz de aceptación de editabilidad integral

| ID | Resultado positivo exigido | Negativa obligatoria |
| --- | --- | --- |
| PAR-01 | Editor autorizado cambia logo y actualiza hero, otros usos y correos/documentos según alcance | No publicar archivo no aprobado o de otro tenant |
| PAR-02 | Isotipo/favicon cambia y se refleja en header, navegador y SEO | No persistir icono fijo anterior |
| PAR-03 | Cambiar nombre/lema y propagar a encabezado, pie, títulos, mensajes y metadatos | No dejar identidad obsoleta en caché |
| PAR-04 | Header, footer, textos, navegación, CTA y páginas editables y publicables sin redeploy | No código arbitrario o enlaces peligrosos |
| PAR-05 | Paleta y tipografía por tokens con contrastes WCAG comprobados | Rechazar combinaciones inseguras o no accesibles |
| PAR-06 | Publicar revisión con CAS, historial y rollback reproducible | Conflicto entre escritores y preview público rechazados |
| PAR-07 | Refresco de cachés/tabs/sesiones dentro de SLA real probado | No mezclar ámbito/tenant ni mostrar marca antigua |
| PAR-08 | Subida validada, optimización segura, cuotas y cuarentena de medios | No aceptar malware, SVG activo, extensión disfrazada, EXIF o SSRF |
| PAR-09 | Autorizar actor/rol/acción/tenant/sucursal/propiedad y auditar cambio | No conceder permisos por el frontend |
| PAR-10 | Fallback sin marca ajena si asset o publicación no está disponible | No reutilizar logo obsoleto, no mostrar borrador |

**Gate del capítulo 01:** demostrar el read-model, rechazo de activos no seguros, logo dinámico en header/hero/favicon al recibir una revisión publicada y requisitos futuros explícitos. NO confundir con un panel editando la marca. La certificación C01 exige acta, A/B/C integrales, CI final/postmerge y aceptación expresa del propietario; acredita 1/37 desde la aceptación y CI postmerge verificadas el 9 de noviembre de 2026; la matriz futura PAR-01...PAR-10 continúa pendiente de sus capítulos respectivos.
