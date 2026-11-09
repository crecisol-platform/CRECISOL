# Orden de implementación obligatorio de CRECISOL

Fuente de nombres: Documento Maestro Único. **ESTADO VIGENTE: 2/37 capítulos certificados (C01 y C02)**, aprobación humana expresa del propietario «APROBADO» el **9 de noviembre de 2026** tras nueve microfases C02 y auditorías A/B/C integrales. [Acta C02-09 y aceptación](c02/C02_09_AUDITORIA_Y_ACTA.md) y [estado C02](C02_PREPARACION.md). Se exige CI del PR de certificación y POSTMERGE sobre su SHA antes de formalizar el cierre; la última acta técnica C02-09 está fusionada en `dev` SHA `d8ba77dd65f87325ca8dbf2f38455943af2f8d8e`, con 12/12 GitHub Actions POSTMERGE SUCCESS. **C03 HABILITABLE PARA PLANIFICACIÓN DESPUÉS DEL CIERRE, AÚN NO INICIADO**. C04–C37 siguen pendientes. La certificación de C02 se limita a contratos/experiencias, UI pública, pruebas sintéticas aisladas, móvil y accesibilidad automatizada; no afirma panel administrativo integral, autorización privada, pagos, WCAG manual ni despliegue. Los 37 capítulos requieren implementación secuencial, pruebas, A/B/C, acta y aceptación expresa.

**Antecedente histórico preservado para las auditorías anteriores (NO estado vigente):** 1/37 capítulos certificados; al terminar C02-08, C02 **NO CERTIFICADO**, C03 bloqueado, 8/9 técnicas completadas. La última acta C02-09 cerró 9/9 y la aceptación humana posterior «APROBADO» sustituyó esa situación.

C01 | CRECISOL: identidad, visión, alcance y principios | **CERTIFICADO** (C01-01 a C01-08, 8/8; A/B/C integrales PASS; aprobación expresa 2026-11-09; alcance únicamente C01)
C02 | Personas, experiencias y arquitectura de producto | **CERTIFICADO C02, alcance público/sintético delimitado y aceptado expresamente por el propietario el 9-11-2026 («APROBADO»)**. 9/9 microfases, auditorías integrales A/B/C PASS, 58/58 Chromium C02 + 25/25 C01, Domain/Application, MySQL8.4/API/Angular públicos desechables; PR técnico [#46](https://github.com/solqaryn-platform/CRECISOL/pull/46) SHA `cb450690a8b7a9d0660c39baa52d483abf164168`, acta [#47](https://github.com/solqaryn-platform/CRECISOL/pull/47) SHA `d8ba77dd65f87325ca8dbf2f38455943af2f8d8e`, 12/12 CI POSTMERGE SUCCESS incluido [#42678910536](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42678910536). [Acta de certificación](c02/C02_09_AUDITORIA_Y_ACTA.md). Formalización definitiva condicionada a CI POSTMERGE del PR de aceptación. Sin auth privada, BOLA/IDOR/MFA, panel integral, WCAG manual ni cobros reales
C03 | Arquitectura técnica limpia y dependencias | **HABILITABLE UNA VEZ CERTIFICADO C02**; NO INICIADO en este PR, requiere plan, rama propia, pruebas y tres auditorías
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
