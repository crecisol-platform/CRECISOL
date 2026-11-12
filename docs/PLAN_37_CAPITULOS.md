# Orden de implementación obligatorio de CRECISOL

**Ubicación operativa verificada (11-11-2026):** `crecisol-platform/CRECISOL` (ID `1450414592`, `dev`). [Evidencia del traslado](MIGRACION_ORGANIZACION_GITHUB_2026_11_11.md). CI posterior al PR #79 sigue `queued` (`43097016405`): **C03 NO CERTIFICADO; C04 BLOQUEADO** hasta el gate vigente; los hechos y referencias históricos debajo no se reescriben.

**NOTA VIGENTE C03-10 (11-11-2026):** C03-09 está certificada en su alcance según docs/c03/C03_09_CERTIFICACION_AUTONOMA.md. C03-10 CERRADA TÉCNICAMENTE (acta y 16/16 CI PRE/POSTMERGE verificada), C03 NO CERTIFICADO, C04 BLOQUEADO. El propietario autorizó validar C03-10 mediante GitHub Actions con CRECISOL público. Véase docs/DECISION_OPERATIVA_2026-11-11_RETORNO_CI_C03.md. Las líneas históricas de abajo no se reescriben hasta certificación y acta verificable.

**AVISO OPERATIVO VIGENTE DESDE 11-11-2026 (posterior a los registros históricos):** [Decisión del propietario: autonomía técnica sin dependencia de GitHub Actions](DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md). GitHub solo aloja y versiona código; **CI PREMERGE/POSTMERGE deja de ser requisito prospectivo**, sin alterar sus resultados históricos. El asistente ejecuta tres auditorías A/B/C rigurosas y pruebas reales aplicables; las pruebas no ejecutables por falta de herramientas se declaran como tales. **C03-09 CERTIFICADA EN SU ALCANCE (9/10)** mediante [acta autónoma](c03/C03_09_CERTIFICACION_AUTONOMA.md) y pruebas reales previas de Codex. C03-10 queda habilitada para auditar el capítulo completo; **C03 aún no está certificado y C04 permanece bloqueado**. Esta nota prevalece solo en el proceso operativo, **no modifica** el contenido funcional ni huellas del Documento Maestro Único.

Fuente de nombres: Documento Maestro Único. **ESTADO VIGENTE: 2/37 capítulos certificados (C01 y C02); C03-01 a C03-08 CERTIFICADAS (8/10), C03-05 CERTIFICADA (5/10), C03-06 CERTIFICADA (6/10) y C03-07 CERTIFICADA (7/10) tras PR técnico #63 (8/8 CI POSTMERGE SUCCESS, SHA a256885afa5ee2f639bd4ed6f7d8595b709e6409) y PR documental #64 (5/5 CI POSTMERGE SUCCESS, SHA b1e019afabfb44f05076024b8d34cde74a340acc); C03-08 CERTIFICADA (8/10) por PR #70 (6/6 CI POSTMERGE SUCCESS, SHA 221afb6cc515fc79a3b6ca6e010af11f2e20b0a2) y PR #71 (5/5 CI POSTMERGE SUCCESS, SHA b333e58378b6a5ea0438f1ee6b11de85d9ce04df); C03-09 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO.** Antecedente histórico: la aprobación del propietario «Adelante por favor con ello» del 9-11-2026 fue específica para C03-04 tras el PR técnico #55. C03-04 quedó certificada después mediante PR #56; C03-05 mediante PR #58. [Acta C03-04](c03/C03_04_ACTA_MICROFASE.md), [acta C03-03](c03/C03_03_ACTA_MICROFASE.md), [acta C03-02](c03/C03_02_ACTA_MICROFASE.md), [acta C03-01](c03/C03_01_ACTA_MICROFASE.md). Word y PDF intactos; sin BOLA/IDOR/MFA privados, consola administrativa ni producción.

**ESTADO OPERATIVO POSTERIOR — C03 CAPÍTULO (11-11-2026):** [Registro prospectivo de certificaciones](ESTADO_VIGENTE_CAPITULOS.md) y [acta integral](c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md). El propietario dio aceptación expresa condicionada; tras aprobar PREMERGE, fusionar a dev y aprobar POSTMERGE de ese acta, el estado pasa a **C03 CERTIFICADO (10/10), 3/37 capítulos (C01, C02, C03)**. **C04-01 HABILITADA SOLO PARA PREPARACIÓN, C04 NO CERTIFICADO, C05 BLOQUEADO**. El registro posterior prevalece; las filas C03/C04 que siguen permanecen literalmente como inventario histórico verificable por los gates C01/C02/C03 de su fecha, **no son el estado vigente para abrir código nuevo**.

**Antecedente histórico preservado para las auditorías anteriores (NO estado vigente):** 1/37 capítulos certificados; al terminar C02-08, C02 **NO CERTIFICADO**, C03 bloqueado, 8/9 técnicas completadas. La última acta C02-09 cerró 9/9 y la aceptación humana posterior «APROBADO» sustituyó esa situación.

C01 | CRECISOL: identidad, visión, alcance y principios | **CERTIFICADO** (C01-01 a C01-08, 8/8; A/B/C integrales PASS; aprobación expresa 2026-11-09; alcance únicamente C01)
C02 | Personas, experiencias y arquitectura de producto | **CERTIFICADO C02, alcance público/sintético delimitado y aceptado expresamente por el propietario el 9-11-2026 («APROBADO»)**. 9/9 microfases, auditorías integrales A/B/C PASS, 58/58 Chromium C02 + 25/25 C01, Domain/Application, MySQL8.4/API/Angular públicos desechables; PR técnico [#46](https://github.com/solqaryn-platform/CRECISOL/pull/46) SHA `cb450690a8b7a9d0660c39baa52d483abf164168`, acta [#47](https://github.com/solqaryn-platform/CRECISOL/pull/47) SHA `d8ba77dd65f87325ca8dbf2f38455943af2f8d8e`, 12/12 CI POSTMERGE SUCCESS incluido [#42678910536](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42678910536). [Acta de certificación](c02/C02_09_AUDITORIA_Y_ACTA.md). Formalización definitiva condicionada a CI POSTMERGE del PR de aceptación. Sin auth privada, BOLA/IDOR/MFA, panel integral, WCAG manual ni cobros reales
C03 | Arquitectura técnica limpia y dependencias | **C03-01 A C03-08 CERTIFICADAS (8/10)**. C03-05 CERTIFICADA (5/10) vía PR #58, 11/11 CI POSTMERGE SUCCESS en `12fb19b0c35038c297939e17b27770c4fd63f77e` ([acta](c03/C03_05_CERTIFICACION_POSTMERGE.md)); C03-06 CERTIFICADA (6/10) vía PR #61 y 5/5 POSTMERGE SUCCESS sobre `a1923d117febc5fd4e5a544b558c0f85ad517b44` ([prueba C03_06_CERTIFICACION_POSTMERGE.md](c03/C03_06_CERTIFICACION_POSTMERGE.md)). **C03-07 CERTIFICADA (7/10)** tras PR #63 (8/8 POSTMERGE SUCCESS, 8/8 CI POSTMERGE SUCCESS sobre `a256885afa5ee2f639bd4ed6f7d8595b709e6409`) y PR #64 (5/5 POSTMERGE SUCCESS, 5/5 CI POSTMERGE SUCCESS sobre `b1e019afabfb44f05076024b8d34cde74a340acc`); [constancia C03_07_CERTIFICACION_POSTMERGE.md](c03/C03_07_CERTIFICACION_POSTMERGE.md). **C03-08 CERTIFICADA (8/10)** por PR #70 (6/6 POSTMERGE SUCCESS, SHA `221afb6cc515fc79a3b6ca6e010af11f2e20b0a2`) y PR #71 (5/5 POSTMERGE SUCCESS, SHA `b333e58378b6a5ea0438f1ee6b11de85d9ce04df`); [constancia C03_08_CERTIFICACION_POSTMERGE.md](c03/C03_08_CERTIFICACION_POSTMERGE.md). **C03-09 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02)**. Infraestructura PLATFORM sintética/inactiva, sin autorización tenant privada, workers productivos, pagos ni despliegues
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

[Propuesta de 365 microfases para 37 capítulos](PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md), con 8 microfases operativas de C01 ya definidas y 357 microfases **provisionales** de C02–C37 en archivos separados. La certificación de C01 habilita iniciar C02 con nueva matriz y gates; las propuestas de C03–C37 NO habilitan desarrollo anticipado, NO modifican el Documento Maestro Único y NO constituyen implementación o certificación. Cada capítulo posterior debe contrastarse íntegramente con el Word vigente y obtener su puerta de entrada cuando corresponda. [Protocolo de continuidad no activado](PROTOCOLO_CONTINUIDAD_CRECISOL.md).\n\n## Integración comercial propuesta sin cambiar el orden de los 37 capítulos

El propietario aprobó el 10-11-2026 un modelo comercial de CRECISOL con **modalidad de dirección + plan SaaS + duración contractual + periodicidad de pago**, publicación de ofertas administrada, anticipos, renovación/cancelación, manejo de morosidad, altas/bajas y publicidad bajo condiciones. Se registra como `COM-001..COM-014` y `COM-T01..COM-T18`, con mapa capítulo por capítulo en [análisis de impacto](ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md). La edición comercial Word/PDF está incorporada y sus huellas verificadas en la rama del PR #73; para convertirse en autoridad vigente de dev aún requiere CI, auditorías A/B/C y aceptación de integración.

**No se agrega un capítulo 38 ni se activan C04-C37.** Los planes futuros afectados se precisan conservando su número de microfases; C01, C02 y C03-01..08 permanecen certificados en su alcance original. C03-09 sigue pendiente; esta intervención no es su ejecución.
