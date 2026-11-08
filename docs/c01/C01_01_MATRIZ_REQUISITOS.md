# C01-01 · Matriz de requisitos y dependencias de la fuente canónica

**Proyecto:** CRECISOL · **Fecha de control:** 2026-11-08 · **Estado:** inventario extraído y clasificado; cierre de fase sujeto a validación y conciliación de códigos.

**Única autoridad:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, sección «01. CRECISOL: identidad, visión, alcance y principios», desde su introducción hasta §1.7. La matriz es **derivada, no segundo documento maestro**; las síntesis no sustituyen el texto fuente.

**Control de cobertura original:** 34 párrafos fuente etiquetados P01–P34 (**27 párrafos de contenido y 7 encabezados estructurales**), tres tablas originales T01/T02/T03, con **7 + 8 + 6 = 21 filas de requisitos** y tres encabezados (24 filas incluyéndolos). Los rótulos de sección cuentan dentro de 34 pero no generan un requisito de ejecución independiente. La fuente contiene también **dos tablas anexas** de contrato de implementación, distintas de las tres tablas originales; se analizan en [dependencias y puertas](C01_01_DEPENDENCIAS_Y_GATE.md).

## A. Los 34 párrafos originales

Cada fila conserva una referencia estable al orden físico dentro del maestro; «dependencia» señala quién debe concretar la aceptación, no que la funcionalidad ya esté desarrollada.

| Ref. | Sección | Requisito o significado fiel de la fuente | Dependencias | Criterio para su verificación futura |
| --- | --- | --- | --- | --- |
| P01 | Introducción | SaaS universal para pymes: comercio, servicios, membresías y configuración por negocio; flujos simples y accesibles. | C01-02/05; C02,09,10,13,15 | Inventario de capacidades y contratos por rubro, sin imponer sector. |
| P02 | Introducción | Una sola base modular; no instalaciones separadas ni ERP corporativo ilimitado. | C01-02/03; C02,03,30 | Arquitectura modular única y exclusiones funcionales explícitas. |
| P03 | 1.1 | Encabezado estructural: invariantes de diseño. | C01-01 | Agrupa P04–P10; sin prueba independiente. |
| P04 | 1.1 | Aislamiento total: cambios de URL, parámetros, headers, caché, eventos y archivos no filtran datos tenant. | C01-02/03/04/07; C04,06,07,21,32 | Negativas cross-tenant para cada superficie afectada; P0. |
| P05 | 1.1 | Cobros reales solo con confirmación confiable e idempotencia; jamás CVV ni número completo. | C08,12,31; C01-07 | Sin medios de pago reales en C01; pruebas y controles al habilitar cobros. |
| P06 | 1.1 | URL e ID aleatorio nunca equivalen a autorización. | C01-04/07; C04,05,06 | Autorización backend y negativas IDOR; P0. |
| P07 | 1.1 | Personalización solo como datos validados; prohibido JavaScript o SQL enviado por clientes. | C01-02/04/05/07; C09,34 | Contratos tipados, sanitización, preview y publicación segura. |
| P08 | 1.1 | SuperAdmin controla la plataforma; acceso excepcional a datos consumidores con auditoría. | C05,06,07,08 | MFA, motivo, límite temporal y registros; no acceso automático en C01. |
| P09 | 1.1 | Cambios de horario, contrato, inventario o membresía preservan historia. | C11,12,13,15,17,26 | Pruebas de versionado/eventos sin sobrescribir registros anteriores. |
| P10 | 1.1 | Respaldo, restauración verificada, contingencia y despliegue reversible. | C07,24,25,29 | Evidencia de recuperación/rollback antes de producción; fuera de la ejecución VPS actual. |
| P11 | 1.2 | Encabezado estructural: límites deliberados. | C01-01 | Agrupa P12; sin prueba independiente. |
| P12 | 1.2 | Excluir hospital, historia clínica, nómina avanzada, contabilidad corporativa, fiscalidad integral, banca, agregación de fondos y manufactura compleja. | C01-01; C08,16,23,29 | Mensajes y planes no afirman capacidades reguladas no implementadas. |
| P13 | 1.3 | Encabezado estructural: identidad oficial y posicionamiento. | C01-01 | Agrupa P14–P15 y T02; sin prueba independiente. |
| P14 | 1.3 | Nombre CRECISOL, lema y descriptor oficiales; soluciones seguras/adaptables sin promesas de resultados. | C01-02/05/06/07 | Contrato único de identidad y pruebas de texto en experiencias aprobadas. |
| P15 | 1.3 | CRECI = crecimiento; SOL = claridad, energía y vínculo institucional; relato no registral ni garantía. | C01-02/05; C23 | Uso editorial fiel, sin declaración de derechos jurídicos inferidos. |
| P16 | 1.4 | Encabezado estructural: relación institucional con SOLQARYN. | C01-01 | Agrupa P17–P22; sin prueba independiente. |
| P17 | 1.4 | Respaldo de marca SOLQARYN sin compartir credenciales, repo, datos, entornos, secretos o servicios. | C01-01/07; C23,25 | Prueba documental de independencia; ninguna escritura o lectura privada entre plataformas. |
| P18 | 1.4 | Repositorio, CI y permisos de CRECISOL independientes; documentación separada por acceso. | C01-01/07; C25 | Verificar repo/branch/CI y scopes de permisos GitHub. |
| P19 | 1.4 | Frontend/API/workers/MySQL propios previstos en Hostinger VPS; sin conexiones transversales. | C01-03; C24,35 | Solo parámetros y contratos locales; VPS existente NO VERIFICADO, sin despliegue. |
| P20 | 1.4 | Cloudflare R2: buckets, rutas, llaves y políticas independientes; archivos privados autorizados y temporales. | C21,32; C01-07 | Diseño futuro y controles negativos por tenant; no habilitar R2 ahora. |
| P21 | 1.4 | SuperAdmin CRECISOL tiene poderes solo dentro de CRECISOL y autorizaciones trazables. | C05,06,08 | Pertenencia/acción y auditoría; no federar permisos con SOLQARYN. |
| P22 | 1.4 | Contratos, facturas y privacidad deben identificar la entidad jurídica y contacto reales acreditados. | C01-01/02; C23,29 | Pendiente evidencia legal; no inventar emisor, fiscalidad o responsables. |
| P23 | 1.5 | Encabezado estructural: identidad digital y anticlonación. | C01-01 | Agrupa P24–P29; sin prueba independiente. |
| P24 | 1.5 | crecisol.com solo oficial con titularidad comprobada; dominios de empresas verificados y no autorizadores. | C01-03/04/07; C04,20,24 | Sin afirmar DNS/TLS o propiedad; pruebas de host y control de dominio futuro. |
| P25 | 1.5 | Marca coherente en favicon/SEO/OG, emails, notificaciones, recibos, QR, app, ayuda; marca tenant prevalece en su sitio. | C01-02/05/06/07; C19,20,21 | Matriz de emisores/experiencias; cero fuga de marca y datos. |
| P26 | 1.5 | Correos institucionales reales y verificados, SPF/DKIM/DMARC, URLs seguras, anti-phishing. | C19,20,24,29 | Sin correos no verificados; controles DNS y remitente antes de habilitación. |
| P27 | 1.5 | Enlaces auth, pago y QR separados por propósito, público, vigencia, emisor y contexto; no secretos. | C05,12,19,31 | Negativas de reutilización/filtración de tokens y enlaces. |
| P28 | 1.5 | Slugs del sistema reservados; IDN/punycode, homoglifos, colisiones, redirects y Host validados. | C01-07; C04,20 | Pruebas negativas contra host falso, redirección abierta y slug reservado. |
| P29 | 1.5 | Antes de lanzamiento verificar marca, dominio, registrador, renovaciones, recuperación, redes y políticas. | C23,24,29 | Evidencia externa verificable y responsable; nunca presuponer titularidad. |
| P30 | 1.6 | Encabezado estructural: mensajes comerciales de referencia. | C01-01 | Agrupa P31–P33; sin prueba independiente. |
| P31 | 1.6 | Encabezado y subtítulo comerciales deben conservar nombre, lema y descriptor oficial. | C01-02/05/06 | Contrato de marca y SEO/landing actualizados desde fuente común. |
| P32 | 1.6 | Propuesta de valor de ventas, reservas, membresías y clientes, solo para capacidades efectivamente publicadas. | C01-02/05; C08,12,13,15,17 | No presentar promesas comerciales no implementadas como funcionalidades activas. |
| P33 | 1.6 | CTAs sugeridos: Conoce/Explora/Solicita demo/Planes; prohibida compra de módulos inactivos o cobros fuera del PSP. | C01-05/07; C08,31 | Botones honestos y negativos para cobros/pantallas deshabilitadas. |
| P34 | 1.7 | Encabezado estructural: pruebas de aceptación de marca e identidad. | C01-01/07 | Agrupa T03-R01–R06; sin prueba independiente. |

## B. Las 21 filas originales de las tres tablas

Se conservan tres identificadores por tabla y fila: `T01-Rnn` (capacidades), `T02-Rnn` (identidad aprobada) y `T03-Rnn` (aceptación). Las frases son **síntesis fieles** de su comportamiento y control; para texto completo consultar la misma fila en el Word.

| Ref. | Tabla | Comportamiento/elemento de la fuente | Control/criterio de aceptación | Fases y capítulos dependientes |
| --- | --- | --- | --- | --- |
| T01-R01 | Capacidades | Público objetivo: comercios, profesionales, una o varias sucursales y crecimiento a mediana empresa. | Catálogo universal con límites comerciales configurables, sin dependencia sectorial. | C01-02; C02,30 |
| T01-R02 | Capacidades | Producto y servicio mixtos: productos, servicios y membresías en un negocio. | Venta, reserva y suscripción con contratos distintos y cliente/historial compartidos. | C10,12,13,15,17 |
| T01-R03 | Capacidades | Diseño sin código: propietario configura tarjetas, páginas, secciones, tipografías, banners y dashboard. | Esquemas tipados seguros, vista previa, publicación versionada y rollback. | C01-02/04/05; C09 |
| T01-R04 | Capacidades | Seguridad por defecto: auth y autorización servidor; mínimo privilegio y exposición. | Puertas P0 de aislamiento, identidad, pagos, privacidad y respaldo. | C01-04/07; C04–07 |
| T01-R05 | Capacidades | Rendimiento percibido: páginas ligeras, acciones ágiles, administración compacta y responsive. | Presupuestos de tamaño, mediciones y pruebas en móvil real. | C01-05/07; C27 |
| T01-R06 | Capacidades | Rapidez controlada: microtareas pequeñas, revisadas e independientes. | Un objetivo verificable por PR, sin saltar seguridad. | C01-01/08; C25 |
| T01-R07 | Capacidades | No-limitación artificial: extensiones, atributos, plantillas y rubros administrables. | Cuotas técnicas honestas, límites de planes y reglas legales; no prometer infinito. | C01-02; C08,30 |
| T02-R01 | Identidad | Nombre público: CRECISOL, mayúsculas en logotipo; Crecisol en prosa. | No utilizar nombres provisionales en marca oficial. | C01-02/05/06/07 |
| T02-R02 | Identidad | Lema: Tu negocio, a tu manera. | Promesa de marca sin garantía contractual de resultados. | C01-02/05/07 |
| T02-R03 | Identidad | Descriptor: Comercio · Servicios · Membresías · Crecimiento. | Familias de valor, sin exclusividad por sector. | C01-02/05/07 |
| T02-R04 | Identidad | Relación de marca: CRECISOL, una plataforma de SOLQARYN. | Solo respaldo/origen de marca; no dependencias de código, usuarios, BD o infraestructura. | C01-01/08; C23 |
| T02-R05 | Identidad | Público objetivo: pequeñas y medianas empresas, según operación de cada tenant. | Activación parametrizada de comercio, servicios y membresías. | C01-02; C02,30 |
| T02-R06 | Identidad | Dominio comercial elegido: crecisol.com. | No afirmar compra, titularidad ni disponibilidad sin registrar/consulta jurídica. | C01-01/07; C24 |
| T02-R07 | Identidad | Identidad propia de cada negocio tenant. | Sitios, correos, documentos, QR y pagos no confunden emisor plataforma/negocio. | C01-02/03/04/07; C04,19,20,23 |
| T02-R08 | Identidad | Naturaleza: SaaS multitenant comercial universal. | No describir como ERP ilimitado, banco, agregador de fondos ni sistema médico. | C01-01/05 |
| T03-R01 | Aceptación | Marca global: landing, onboarding, SuperAdmin, emails, SEO y documentos. | Nombre/lema/descriptor coherentes y sin marcas ajenas no autorizadas. | C01-05/06/07; dependencias C05,19,20 |
| T03-R02 | Aceptación | Marca tenant: sitios, portal, pagos, reseñas y QR. | Identidad/políticas/cobrador propio; sin fuga de branding ni datos entre tenants. | C01-02/03/04/07; C04–06,12,18 |
| T03-R03 | Aceptación | Relación matriz en footer, contratos y página institucional. | Expresar respaldo SOLQARYN y emisor jurídico real en documentos contractuales. | C01-02/05/07; C23 |
| T03-R04 | Aceptación | Dominio y seguridad: raíz/subdominios/dominios propios verificados. | Host canónico, TLS y resolución determinista; sin privilegios por host falso/redirect/slug. | C01-03/04/07; C04,20,24 |
| T03-R05 | Aceptación | Mensajería y enlaces: acceso, recuperación, cobro y notificación. | Emisor y propósito; sin fugas de tokens a logs, referers, capturas o enlaces. | C01-07; C05,19,31 |
| T03-R06 | Aceptación | Prelanzamiento: marca, dominio, datos fiscales y medios oficiales. | Archivar evidencia real, responsable y revisión; nunca derechos o compras ficticios. | C01-01/08; C23,24,29 |

## C. Política de estados para no inflar el avance

**Inventariado** significa localizado, sintetizado y enlazado a un criterio observable; **no significa implementado ni probado**. Los encabezados P03, P11, P13, P16, P23, P30 y P34 son estructurales.

- La API pública de marca y su compilación, el PNG extraído y las negativas SQL actuales constituyen **evidencia técnica preliminar** de algunas filas. La revisión funcional/visual integral y la validación cross-tenant siguen abiertas.
- Pruebas de cobros, membresías, nómina, Cloudflare R2, DNS, mail, ERP y cumplimiento legal se **difieren a los capítulos específicos**; sus invariantes P0 no se omiten, solo se prohíbe afirmar cobertura prematura.
- Hostinger VPS y `crecisol.com` son **destinos previstos por el maestro y datos reportados por el propietario**, sin verificación de propiedad, DNS, TLS ni accesos. No hay permiso para conectar ni desplegar ahora.
- Se conserva una única instancia de cada obligación fuente; la presencia en ambos párrafo y tabla no permite considerarla duplicadamente completada.

**Siguiente documento:** [C01-01 · mapa de dependencias, conflicto de fases y puertas](C01_01_DEPENDENCIAS_Y_GATE.md).
