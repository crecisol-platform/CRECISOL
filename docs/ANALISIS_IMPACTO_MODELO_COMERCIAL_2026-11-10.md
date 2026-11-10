# CRECISOL — Análisis de impacto y puerta de migración documental del modelo comercial

**Estado: ANÁLISIS PREPARATORIO / NO NORMATIVO / NO IMPLEMENTADO / NO CERTIFICADO.**  
**Fecha de solicitud y aprobación de análisis:** 2026-11-10.  
**Repositorio exclusivo:** `solqaryn-platform/CRECISOL`.  
**Base verificada:** `dev` commit `af2977296d6235a3b9019d67cbfa65c28732e804`.  
**Punto de continuidad:** C01 y C02 certificados; C03-01..08 certificados; C03-09 no iniciada; C03 no certificado; C04 bloqueado.

> Este documento es un expediente de análisis de impacto, **no** un Documento Maestro alternativo. No modifica ni sustituye el Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, su PDF, la autoridad de `docs/MASTER_REFERENCE.md` ni los contratos históricos. La aprobación del propietario autoriza preparar una actualización coordinada; **no equivale a validar clausulados legales, precios ni políticas aún no fijados**.

## 1. Inventario documental y dependencias confirmadas

- Fuente canónica editable: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`; blob Git `214bf9c3d9763be23a77b7e0c464359584edde46`; SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e` (ver `docs/MASTER_REFERENCE.md`).
- Derivado de lectura: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf`; blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`; SHA-256 `17ffe48aab3b224cc9b11927c498e4f49c162d96bf639c894b84259f597738dd`.
- Se preservan 37 capítulos, la numeración normativa `MAE-CNN-xx` y las microfases técnicas `CNN-xx`, que **no son equivalentes por posición**.
- El maestro actual contiene, según inventario registrado, 189 tablas y un logotipo; el PDF tiene 229 páginas. Estas cantidades son una **línea base** de comprobación, no requisitos de mantener idéntico conteo tras la nueva edición.
- Validadores de C01/C02/C03 fijan la huella del Word y, en algunos casos, estructura y posiciones: antes de cambiar el binario se debe identificar exhaustivamente esas dependencias y conservar las verificaciones históricas.
- Planes derivados: `docs/PLAN_37_CAPITULOS.md`, `docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md`, `docs/planificacion-futura/CNN_MICROFASES_PROPUESTAS.md`, `docs/DECISIONES_ABIERTAS.md`, `docs/ARQUITECTURA.md`, `docs/SEGURIDAD_MULTITENANT.md`.
- Actas y evidencias ya certificadas de C01, C02 y C03-01..08 se mantienen **inalteradas**; nunca se reetiquetan retrospectivamente como implementación de la ampliación.

## 2. Decisiones del propietario que deben reflejarse en la siguiente edición canónica

| ID de trazabilidad propuesto | Requisito aprobado conceptualmente | Criterio mínimo de aceptación documental | Autoridad de implementación prevista |
| --- | --- | --- | --- |
| COM-001 | Tres modalidades comerciales de dirección web con precio distinto | Diferenciar ruta bajo dominio de CRECISOL, subdominio y dominio personalizado; confirmar las denominaciones exactas del maestro antes de fijarlas | C04, C08, C20 |
| COM-002 | Gestión independiente de modalidad, plan/capacidades y plazo contractual | Catálogo normalizado, combinaciones publicables y reglas parametrizables; no confundir slug con dominio | C04, C08, C26/C33 |
| COM-003 | Ofertas contractuales de 3, 6, 12, 24 y 36 meses, extensibles | Definir ofertas por versión, restricciones, precio, moneda, vigencia y visibilidad pública | C08 |
| COM-004 | Precio y descuentos controlados desde SuperAdmin | Publicar/ocultar ofertas sin cambio de código; preservar condiciones aceptadas en contratos anteriores | C08, C34 |
| COM-005 | Pago mensual anticipado y alternativa de prepago total | Anclar fechas, cortes, zona horaria, prorrateos, impuestos y estados; **no afirmar garantía de pagos futuros** | C08, C31 |
| COM-006 | Contratación con aceptación verificable | Evidencia de versión de condiciones, identidad, fecha y mecanismo de aceptación | C08, C23 |
| COM-007 | Renovación automática bajo elección clara del cliente | Consentimiento verificable, aviso, posibilidad de desactivar renovación y tratamiento de cambios de precio | C08, C23, C31 |
| COM-008 | Autoservicio de cancelación y no renovación | Registro de solicitud, acuse, fecha de solicitud/efecto y conciliación; no impedir solicitar rescisión por deuda | C08, C23, C34 |
| COM-009 | Mora, período de gracia, suspensión y reactivación | Estados separados, notificaciones, idempotencia y reversibilidad; **no borrar datos automáticamente** | C04, C08, C31 |
| COM-010 | Altas/bajas de empresas gestionadas por CRECISOL | Autorización backend, causa, evidencia, doble control para acciones sensibles y auditoría | C04, C05-C08 |
| COM-011 | Publicidad propia de CRECISOL y marcas autorizadas | Ubicación, audiencia autorizada, estado, fechas, contenido seguro, preferencia y obligación contractual para espacios en sitios de clientes | C19, C20, C23 |
| COM-012 | Consola global y portal autoservicio separados | Roles diferenciados con servicios comunes y controles de aislamiento tenant en backend | C05-C09, C34 |
| COM-013 | Trazabilidad financiera y contractual | Facturas/pagos conciliados; versión histórica de tarifas y contratos; webhooks idempotentes; sin falsos pagos manuales | C08, C26, C31, C33 |
| COM-014 | Retención, exportación, reembolso y bajas legales | Políticas publicadas; preservar obligaciones legítimas sin bloqueo arbitrario ni pérdida de historial | C06, C08, C23, C31 |

**Advertencia:** las denominaciones de las tres modalidades de COM-001 son una **hipótesis de normalización técnica** derivada de la discusión; requieren comparación con el texto canónico. No se crean tipos distintos ni precios definitivos por inferencia.

## 3. Reglas detalladas a someter a la edición del Word

1. **Catálogo:** la modalidad de dirección es un componente comercial; el plan define capacidades y cuotas; el plazo fija compromiso, periodicidad y reglas de renovación. No fusionar los tres conceptos en una única tabla o bandera.
2. **Calendario:** contrato iniciado el 10-11-2026 por tres meses con cuotas mensuales anticipadas cubre períodos `[10 nov, 10 dic)`, `[10 dic, 10 ene)`, `[10 ene, 10 feb)`; la política de zona horaria, fin de mes, días inhábiles, fallos PSP y reintentos permanece por definir.
3. **Renovación:** el plazo inicial y el plazo de renovación son parámetros separados; la renovación no crea compromisos prolongados opacos. No se cobran periodos futuros sin una base contractual válida y autorización de medio de pago.
4. **Terminación:** distinguir desactivar renovación, solicitud de rescisión, terminación efectiva, suspensión técnica y baja de empresa. Las penalidades y devoluciones requieren revisión legal expresa; **no implantar cobro automático de todas las cuotas restantes**.
5. **Morosidad:** no activar antes de confirmar pago inicial; no eliminar empresas ni datos por impago; distinguir pago pendiente de pago rechazado; conciliación, ventana de gracia y suspensión reversible y auditada.
6. **Publicidad:** campañas de plataforma y marcas asociadas no conceden acceso a datos de tenants; insertar anuncios en sitios clientes solo si el acuerdo comercial y la configuración lo permiten. SOLQARYN es únicamente referencia/marca independiente; nada de conexiones técnicas compartidas ni escrituras en su repositorio.
7. **Seguridad:** aislamiento `tenant_id` + sucursal + recurso del lado servidor, autorización por acción, controles anti-BOLA/IDOR, deduplicación de eventos, logs sin secretos y acceso SuperAdmin excepcional auditable.
8. **Parametrización:** precios, cuotas, períodos, política de gracia, recursos de marca, textos publicitarios y posiciones de anuncios se gestionarán mediante configuración versionada y validada, **sin código arbitrario ni valores comerciales en duro**.
9. **Separación de productos:** contratos que CRECISOL vende a empresas (C08) no son membresías que una empresa vende a consumidores finales (C15); tampoco son cobros operativos del negocio (C12).

## 4. Mapa de impacto sobre microfases propuestas

**La relación es de revisión obligatoria, no de ejecución anticipada.** Los números técnicos son los de los archivos `docs/planificacion-futura/` y pueden ajustarse luego del contraste integral con el Word.

| Capítulo | Impacto principal y revisión |
| --- | --- |
| C04 | Catálogo de tenants, hosts/slug y dominio verificado, aprovisionamiento, suspensión reversible, propiedad de dominio y enrutamiento seguro |
| C05-C07 | Identidad, permisos, separación de accesos, privacidad, controles antifraude, auditoría y seguridad transversal |
| C08 | **Responsable central del catálogo SaaS**: planes, ofertas, versiones, contratos, facturación, mora, renovación, cancelaciones y consola |
| C09 | Componentes parametrizables de catálogo y formularios, sin confundir editor de sitios con SuperAdmin comercial |
| C12/C15 | Asegurar que cobros y membresías de negocios clientes no se mezclan con ingresos SaaS de CRECISOL |
| C19-C20 | Campañas propias, marcas asociadas, ubicaciones de anuncio, publicación e integración consentida en sitios |
| C23 | Textos jurídicos, consentimiento, cancelación, notificaciones, excepciones, obligaciones pendientes y retención |
| C26/C33 | API/DTO, eventos, modelos relacionales y contratos consistentes para la futura implementación |
| C27/C34/C36 | Pruebas positivas/negativas y recorridos SuperAdmin/propietario; cambios de precio, impago, cancelación, carreras, BOLA |
| C31 | Preparación de medios de pago, recurrencia y conciliación **sin activar procesadores reales** |

Se deben inspeccionar **todos los planes de C01-C37 para impacto cruzado**; modificar únicamente microfases realmente relacionadas, y nunca reabrir actas históricas certificadas. **Ningún capítulo futuro se habilita por esta revisión.**

## 5. Puerta documental para editar el DOCX y regenerar el PDF (pasos y estado de migración)

La conexión GitHub activa confirma ruta, blob y SHA, **pero no ha proporcionado contenido binario del DOCX**: los métodos de lectura de texto no sustituyen la entrega íntegra del fichero. Por ello queda prohibido regenerar o sobreescribir el Word/PDF a partir de extractos, de un PDF o de texto supuesto.

Antes de actualizar el canónico, exigir todo lo siguiente:

1. Recuperar el **archivo binario original** y confirmar bytes, SHA-256 y blob de referencia frente a `docs/MASTER_REFERENCE.md`.
2. Leer el contenido íntegro (incluidos 37 capítulos, 189 tablas originales, anexos, imágenes y relaciones), y crear mapa de secciones preciso sin perder estilos, numeración ni contenido.
3. Enumerar todas las aserciones históricas de hashes, contenido, posiciones, cantidades y estructura en `qa/`, workflows, documentos y scripts. Diseñar validadores versionados que conserven la línea base histórica sin invalidar pruebas pasadas.
4. Editar quirúrgicamente una **copia de trabajo** del DOCX canónico; registrar diff semántico de inserciones y decisiones legales abiertas. No reemplazar el documento por uno nuevo simplificado.
5. Renderizar cada página del DOCX y verificar visualmente layout de tablas, portada, TOC, numeraciones, imágenes y enlaces. Exportar el PDF **desde ese mismo DOCX** y verificar páginas y huellas.
6. Sincronizar `MASTER_REFERENCE.md`, las referencias de capítulos, la matriz, los planes derivados y los validadores, preservando explícitamente el SHA de la edición original como baseline histórico.
7. Ejecutar verificaciones locales reproducibles y tres auditorías: **A** trazabilidad y conservación; **B** seguridad/aislamiento/privacidad; **C** arquitectura, regresión documental y calidad visual. Adjuntar evidencia al SHA exacto.
8. Revisar diff, PR y CI antes de fusionar a `dev`; sin force push ni escritura a `main`, sin alterar SOLQARYN. Si faltan archivos o pruebas, declarar NO_GO y dejar el PR en borrador.

## 6. Decisiones comerciales/jurídicas pendientes (no inventar valores)

- Identidad exacta de las tres modalidades aprobadas en el Word y titular del dominio personalizado.
- Cuotas, monedas, impuestos, descuentos, cargos de configuración, upgrades/downgrades y prorrateo.
- Renovación tras 3/6/12/24/36 meses, preavisos y prueba de consentimiento.
- Plazo de gracia y procedimiento de morosidad, suspensión, restitución y recuperación de datos.
- Existencia y legalidad de cargos de terminación, devoluciones, finiquitos y contratos de adhesión; revisión específica bajo ley hondureña aplicable.
- Condiciones y consentimiento sobre espacios publicitarios en sitios clientes, marcas autorizadas y exclusiones por sector.
- PSP y regulaciones de pagos en fase futura, con pruebas sintéticas hasta autorización independiente.

Estas cuestiones se relacionan con `ADR-005`, `ADR-006` y nuevas decisiones comerciales por registrar en `docs/DECISIONES_ABIERTAS.md` **al integrar la nueva edición del maestro**.

## 7. Alcance del PR preparatorio y condiciones de continuidad

- **Alcance original de este PR preparatorio:** inicialmente únicamente el análisis de impacto; ver la actualización posterior del alcance en la sección 8. No sustituye fuentes existentes ni altera estados históricos.
- **Sin cambios:** `frontend/`, `backend/`, `database/`, Word, PDF, `qa/`, workflows, actas certificadas, ramas `main`/`dev` y cualquier recurso de SOLQARYN.
- **Estado esperado:** PR `draft` hacia `dev`, sin aprobación normativa ni merge; el estado del producto continúa **C03-09 pendiente**.
- **Rollback:** cerrar el PR y eliminar su rama de trabajo si fuese necesario; no hay migraciones ni datos afectados.
- **Pruebas (alcance original):** lectura y comparación GitHub de la base, revisión estática del texto y diff inicial del PR. Las verificaciones adicionales constan en la sección 8. **No afirmar auditorías A/B/C de software, compilaciones, CI ni certificación sin evidencias reales**.

## 8. Actualización tras recibir los originales del propietario — 10-11-2026

El propietario adjuntó a la conversación el DOCX y PDF originales. Se **comprobó localmente** que los SHA-256 coinciden exactamente con `docs/MASTER_REFERENCE.md`: DOCX original `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, PDF original `17ffe48aab3b224cc9b11927c498e4f49c162d96bf639c894b84259f597738dd`. El bloqueo de acceso al binario **se resolvió para la edición local**, pero **el conector GitHub no permite subir binarios DOCX/PDF**; la integración de la nueva edición a esta rama está pendiente.

**Resultados locales reproducibles de la edición preparada (no existente aún en esta rama):**

| Archivo preparado localmente | SHA-256 | Tamaño | Verificación |
| --- | --- | ---: | --- |
| `CRECISOL_DOCUMENTO_MAESTRO_UNICO_EDICION_COMERCIAL_2026-11-10.docx` | `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` | 1,353,343 bytes | 37 capítulos, 1,687 párrafos originales sin cambio, 189 tablas anteriores y logo idénticos, 5 tablas nuevas |
| `CRECISOL_DOCUMENTO_MAESTRO_UNICO_EDICION_COMERCIAL_2026-11-10.pdf` | `eabf7641c93158d9a7519e4c2b5785015ef6908cbff9edf51844bdd58a3e7cf6` | 4,632,379 bytes | 237 páginas, 229 anteriores con cuerpo de texto idéntico en comparación PDF, 8 páginas añadidas |

La adenda comercial local incluye `COM-001..COM-014`, `COM-T01..COM-T18`, 37 referencias de capítulo y obligaciones transversales, con precios, días de gracia y penalidades pendientes de definición/validación legal. Renderizado mediante `render_docx.py` (237 páginas), inspección de miniaturas del anexo, ZIP CRC y ubicación de bloques PDF (sin contenido fuera del lienzo); **no se ejecutaron pruebas de software ni revisión legal**.

**Actualización de esta rama:** notas de planificación condicionada añadidas exclusivamente a 24 propuestas de microfases futuras (C04, C05, C06, C07, C08, C09, C12, C15, C17, C18, C19, C20, C21, C23, C25, C26, C27, C29, C30, C31, C33, C34, C36 y C37). También se añadieron referencias preparatorias en `docs/DECISIONES_ABIERTAS.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md`, `docs/ARQUITECTURA.md` y `docs/SEGURIDAD_MULTITENANT.md`. No se han abierto fases de código ni alterado la historia de C01, C02 o C03.

**NO_GO de integración vigente:** no fusionar PR #73 sin incorporar el DOCX/PDF nuevos como la misma edición, actualizar `docs/MASTER_REFERENCE.md` con sus blobs Git reales, migrar validadores históricos de SHA/contenido/estructura, ejecutar CI y tres auditorías A/B/C exigibles. Los controles A/B/C estáticos locales son revisiones preliminares, no certificaciones ni aprobación jurídica. `dev` y `main` deben permanecer intactos hasta cumplir dichas puertas.
