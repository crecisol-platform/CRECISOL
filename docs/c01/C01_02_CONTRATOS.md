# C01-02 · Contratos de identidad y parametrización

**Estado:** EN DESARROLLO — sujeto a compilación .NET 10, pruebas negativas, triple auditoría y acta. **Fuente única:** Documento Maestro Único CRECISOL (SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e) y [matriz C01-01](C01_01_MATRIZ_REQUISITOS.md). Los requisitos de C02–C37 no se implementan aquí.

## Mapa requisito → contrato → condición de aceptación

| Referencia de la matriz original | Obligación | Contrato / evidencia |
| --- | --- | --- |
| P07, T01-R03 | Solo datos configurables, no ejecutar HTML/JS/CSS | BrandValidation y BrandPresentation con tokens de tipografía/iconos y texto validado |
| P14, P31, T02-R01, T02-R02, T02-R03 | Nombre, lema y descriptor oficiales y consistentes | BrandSnapshot almacena Name, Slogan y Descriptor, inmutables y validados |
| P04, P06, T01-R04, T02-R07, T03-R02 | Separación plataforma / negocio y denegación | BrandScope, ScopeId y RequirePublicPlatform; nunca confiere permisos a un usuario |
| P25, T03-R01 | Favicon, SEO, copyright, color y activos de marca | BrandSnapshot y BrandPresentation, sin recursos activos de terceros |
| P24, T02-R06, T03-R04 | Dominio oficial probado; Host no concede acceso | WebsiteUrl solo acepta sintaxis HTTPS de origen; no verifica propiedad ni autoriza routing |
| P17, P22, T02-R04, T03-R03 | Independencia respecto a SOLQARYN y emisor legal comprobado | Sin dependencias externas, secretos, emisor legal inventado ni datos compartidos |
| P32, P33, T02-R08 | No vender ni prometer funcionalidades inexistentes | Sin endpoints, pagos, contratación ni producción |

## Contratos tipados diseñados

1. **BrandScope** distingue Platform con Guid.Empty y Tenant con GUID no vacío. Los IDs no acreditan membresía, rol, sucursal, propiedad del recurso ni host verificado.
2. **BrandSnapshot** es una revisión inmutable (Revision mayor o igual a 1), con estados Draft o Published. Draft no tiene fecha de publicación; Published exige fecha UTC. Solo RequirePublicPlatform permite una identidad **publicada de plataforma**; prohíbe borradores y marcas tenant.
3. **BrandPreviewReference** lleva únicamente Scope, ScopeId y DraftRevision. **No es un token, permiso ni entrega de contenido**. La vista previa es una consulta restringida a un Draft, no un estado persistente adicional. C01-04 y los capítulos de auth deberán validar actor, tenant y permisos antes de exponerla.
4. **BrandPublicationProposal** contiene borrador válido, revisión pública esperada, operación identificable y razón segura. **No publica ni crea derecho de escritura**. La idempotencia, control real de concurrencia, auditoría, rollback y autorización quedan pendientes de un servicio backend seguro.
5. **BrandPresentation** contiene FaviconUrl local, TypographyToken (system/humanist/editorial), IconSetToken (outline/solid), SeoTitle, SeoDescription y CopyrightNotice opcional. C01-05 deberá mapear exclusivamente tokens a un registro de componentes confiables.
6. **BrandValidation** admite colores RGB hexadecimal de seis dígitos, texto sin caracteres de control/markup/bidi inseguro, activos PNG/WebP exclusivamente locales y WebsiteUrl de origen HTTPS sin userinfo, query, fragmento ni ruta. Sintaxis HTTPS **no significa** titularidad o DNS verificados.

El constructor básico de BrandSnapshot se mantiene compatible con el prototipo en PR #10, pero **no se fusiona PR #10 ahora**. Presentation es opcional durante bootstrap; la publicación definitiva y requisitos legales se restringirán al desarrollarse los adaptadores autorizados.

## Alcance y límites

- **Backend:** proyecto Domain puro net10.0, cero referencias externas; contratos y pruebas de consola sin paquetes NuGet.
- **Frontend:** cero cambios. La interfaz pertenece a C01-05.
- **Database:** cero cambios. MySQL 8.4, migraciones y parámetros de Hostinger pertenecen a C01-03.
- **HTTP/Auth:** no se agregan rutas, logins, permisos administrativos o servicios de publicación. El modelo **no sustituye** el aislamiento multitenant real.
- **Legal/producción:** Hostinger y crecisol.com siguen NO VERIFICADOS; no se accede a VPS, DNS, correos ni cobros.
- **Pruebas aún pendientes:** dominio real, autenticación y BOLA/IDOR, concurrencia persistida, publicación/rollback, UI, rendimiento y seguridad de extremo a extremo.
- **Rollback:** revertir únicamente el PR específico de C01-02. No existe migración real ni infraestructura afectada.
- **Certificación:** C01 continúa con **0/37 capítulos certificados** hasta C01-08.

## Puerta de auditoría y entrega

- **Auditoría A:** matriz y pruebas positivas/negativas reales de contratos.
- **Auditoría B:** denegación de exposición pública tenant/draft, validaciones, no secrets/endpoints y negativos.
- **Auditoría C:** dominio puro net10, pruebas reproducibles, docs, separación de fases y reversibilidad.
- **Salida:** tres auditorías PASS + build y tests reales PASS + diff revisado + acta técnica → merge individual a dev (delegación del propietario).
