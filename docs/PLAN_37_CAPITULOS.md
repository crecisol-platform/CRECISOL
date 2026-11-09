# Orden de implementación obligatorio de CRECISOL

Fuente de nombres: Documento Maestro Único. Estado general: **1/37 capítulos certificados (C01, 9 noviembre 2026)**. Certificación con aceptación expresa del propietario, tres auditorías integrales y CI postmerge del SHA `b85ba613bfb8616697226a3252e89919aa38ced2`; acta en `docs/c01/C01_08_AUDITORIA_Y_ACTA.md`. C02–C37 NO están implementados íntegramente ni certificados; C02 conserva entregas técnicas parciales que no equivalen a un capítulo completo. Cada capítulo requiere implementación específica, pruebas ejecutadas, triple auditoría A/B/C, acta y aceptación antes de abrir desarrollo del siguiente. Los títulos no son evidencia de software.

C01 | CRECISOL: identidad, visión, alcance y principios | **CERTIFICADO** (C01-01 a C01-08, 8/8; A/B/C integrales PASS; aprobación expresa 2026-11-09; alcance únicamente C01)
C02 | Personas, experiencias y arquitectura de producto | **EN DESARROLLO** (C02-01..C02-07 cerradas técnicamente, **7/9 condicionado a integrar acta y verificar CI POSTMERGE**; C02-07 diseño móvil/accesibilidad basal en cuatro rutas públicas y harness aislado, PR #42 SHA `912c709ad51082093a82c615fa29106904639a43`, 42/42 Chromium, A/B/C y 18 mutaciones negativas, 10 CI POSTMERGE SUCCESS incluido [#42659404907](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42659404907); [acta C02-07](c02/C02_07_ACTA_MICROFASE.md); C02-08 pendiente/siguiente, C02-09 pendiente; capítulo C02 **NO CERTIFICADO**, C03 bloqueado; WCAG manual y backend privado NO implementados)
C03 | Arquitectura técnica limpia y dependencias | PENDIENTE
C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE
C05 | Identidad, autenticación y recuperación segura | PENDIENTE
C06 | Autorización fina, protección de datos y privacidad | PENDIENTE
C07 | Ciberseguridad integral y respuesta a incidentes | PENDIENTE
C08 | Gobierno central, planes, contratos y facturación SaaS | PENDIENTE
C09 | Constructor visual universal y design system | PENDIENTE
C10 | Catálogo universal y taxonomías administrables | PENDIENTE
C11 | Inventario, compras y operaciones minoristas | PENDIENTE
C12 | Pedidos, cotizaciones, caja y cobros del negocio | PENDIENTE
C13 | Reservas, horarios, personal y recursos | PENDIENTE
C14 | Restaurantes, mesas QR y autoservicio | PENDIENTE
C15 | Membresías y acceso físico universal; perfil gimnasio | PENDIENTE
C16 | Extensiones por sector comercial | PENDIENTE
C17 | Historial del cliente y CRM en dos planos | PENDIENTE
C18 | Fidelización, promociones, reseñas y atención | PENDIENTE
C19 | Marketing, Social Hub y mensajería multicanal | PENDIENTE
C20 | Sitios públicos, mapas y contenido editorial | PENDIENTE
C21 | Cloudflare R2: imágenes, videos, documentos y archivos | PENDIENTE
C22 | Finanzas ligeras, analítica y dashboards | PENDIENTE
C23 | Legislación, políticas y consentimiento | PENDIENTE
C24 | VPS Hostinger y Docker: front, API y MySQL privados | PENDIENTE
C25 | GitHub, CI/CD y disciplina de microtareas | PENDIENTE
C26 | API, eventos y modelos de datos MySQL | PENDIENTE
C27 | Aceptación funcional y seguridad: pruebas | PENDIENTE
C28 | Matriz completa de 30 mejoras | PENDIENTE
C29 | Puertas de lanzamiento y decisiones pendientes | PENDIENTE
C30 | Capacidades universales, permisos y rubros | PENDIENTE
C31 | Pagos por enlace, cobros y suscripciones | PENDIENTE
C32 | Cloudflare R2 y gestión segura de todos los archivos | PENDIENTE
C33 | Diccionario relacional MySQL normalizado | PENDIENTE
C34 | Formularios completos y tablas de gestión | PENDIENTE
C35 | Topología definitiva de plataformas y despliegues | PENDIENTE
C36 | Matriz adicional de pruebas transversales | PENDIENTE
C37 | Referencias normativas y técnicas | PENDIENTE

Reglas transversales: multitenancy shared/dedicated, login contextual sin selector, seguridad browser/API/DB, superadministración auditada, perfiles y multimedia, parametrización y seguimiento universal por expediente; ninguna de estas capacidades se considera ya programada.

## Planificación futura no ejecutable

[Propuesta de 365 microfases para 37 capítulos](PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md), con 8 microfases operativas de C01 ya definidas y 357 microfases **provisionales** de C02–C37 en archivos separados. La certificación de C01 habilita iniciar C02 con nueva matriz y gates; las propuestas de C03–C37 NO habilitan desarrollo anticipado, NO modifican el Documento Maestro Único y NO constituyen implementación o certificación. Cada capítulo posterior debe contrastarse íntegramente con el Word vigente y obtener su puerta de entrada cuando corresponda. [Protocolo de continuidad no activado](PROTOCOLO_CONTINUIDAD_CRECISOL.md).
