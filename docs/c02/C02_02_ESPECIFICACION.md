# CRECISOL — C02-02 · Mapa de necesidades, tareas y criterios observables

**Estado: DISEÑO DOCUMENTADO / NO IMPLEMENTADO.** C02-02 es la segunda de las 9 microfases técnicas; **1/9** está cerrada hasta que las auditorías, la integración y el acta de C02-02 concluyan. El capítulo 02 continúa **NO CERTIFICADO**, 1/37 capítulos certificados (solo C01), C03 BLOQUEADO. La presente especificación no concede acceso, habilita rutas ni crea servicios privados.

**Fuente exclusiva de requisitos:** [Documento Maestro Único](../MASTER_REFERENCE.md), Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. Capítulo 02: 11 párrafos originales P01..P11, tabla de seis perfiles/contextos T01-R01..R06; prefacio de precedencia 07-11-2026 y anexos G.6/G.7. [Inventario certificado técnicamente C02-01](C02_01_ACTA_MICROFASE.md), cierre integrado `5f0b45e5c8584fbcf5d7d783051ebac3f3dd9c72`, cuatro CI postmerge SUCCESS, incluida [#42617017733](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42617017733).

**Contrato derivado verificable:** [C02_02_MAPA_NECESIDADES.json](C02_02_MAPA_NECESIDADES.json) (datos de diseño, **no runtime ni fuente maestra alternativa**). Contiene **6 experiencias, 18 necesidades/recorridos, 7 pasos omnicanal** y para cada necesidad un criterio `given/when/then`, escenario negativo, fuente de origen y dependencia del capítulo responsable. Toda necesidad se etiqueta `design-only`; las características no se publican por el hecho de documentarlas.

## 1. Objetivo exacto y límites

Convertir el inventario C02-01 en un mapa **observable y comprobable**: quién necesita qué, qué tarea realiza, qué resultado se deberá constatar cuando el módulo exista y qué conducta deberá negarse. **No** es el diseño final de rutas C02-03, navegación privada C02-04, componentes C02-05/06, implementación de autenticación C05/06 ni un documento que asegure que ya existe contratación, pedidos o pagos.

**Precedencia normativa:** en el login habitual **no hay selector de empresa**. La identidad, el dominio verificado, la pertenencia, la acción, la sucursal y el recurso deben resolverse en backend o rechazarse en caso de ambigüedad. Un `tenant_id`, host, slug, claim o guard Angular no autoriza nada. Shared/dedicated usan las mismas reglas; el backend determina ubicación física a partir de un catálogo protegido. La consola SuperAdmin tampoco lee PII privada automáticamente.

**No confundir numeraciones:** `MAE-C02-02 = Diseñar` es una de las ocho puertas de proceso que figuran en el Word; `C02-02` es la segunda de nueve microfases operativas del plan derivado. No se adelantan MAE-C02-03..08 ni C02-03..09.

## 2. Experiencias E01–E06, prioridades y recorridos

Las necesidades siguientes son **propuestas trazadas al Word**, no módulos activos. Su criterio exacto y caso negativo están en el JSON para uso por C02-03..08 y por capítulos funcionales futuros.

| Experiencia y persona | Necesidad observable | Identificador y dependencia | Riesgo/negativo prioritario |
| --- | --- | --- | --- |
| **E01** SaaS pública; visitante | Entender oferta, alcance real y sectores | N01 · P01/T01-R01 · C20/C09 | No insinuar funcionalidades productivas inexistentes ni mostrar PII |
| E01; visitante | Comparar condiciones y entender CTA por teclado | N02 · P11/T01-R01 · C08/C20 | Sin cobros reales ni acciones inaccesibles |
| E01; visitante | Recorrer demo sintética por sector y rol | N03 · T01-R06 · C02-08/C09 | Sin acceder a tenant, credenciales o datos reales |
| **E02** Alta de empresas; futuro propietario | Conocer pasos y requisitos de alta | N04 · P01/T01-R01 · C05/C08 | Solicitud anónima no concede propiedad |
| E02; prospecto | Registrar solicitud y recibir estado no privilegiado | N05 · P06/T01-R01/R03 · C05/C08 | Rol y `tenant_id` del cliente no autorizan alta |
| E02; prospecto | Leer términos y consentimiento explícito | N06 · P11/T01-R01 · C08/C23/C31 | No aceptar por defecto ni realizar cargos reales |
| **E03** Consola central; SuperAdmin | Consultar indicadores agregados y catálogo | N07 · P01/T01-R02 · C06/C08/C22 | Sin PII privada ni historial de consumidores por defecto |
| E03; SuperAdmin | Gestionar parámetros institucionales con control de acciones | N08 · P11/T01-R02 · C06/C08 | No autoasignar permisos ni eludir auditoría |
| E03; soporte privilegiado | Solicitar acceso excepcional documentado | N09 · P06/T01-R02 · C05/C06/C07 | MFA, motivo, caducidad y bitácora obligatorios |
| **E04** Panel negocio; propietario | Editar marca y publicar revisión propia | N10 · P01/P11/T01-R03 · C09/C20/C21 | No ejecutar HTML/JS ni editar marca ajena |
| E04; propietario | Administrar sucursales y personal de su ámbito | N11 · P06/T01-R03 · C04/C05/C06 | Prohibir escalada y edición cross-tenant |
| E04; propietario | Revisar pedidos/finanzas con confirmación sensible | N12 · P08/P09/T01-R03 · C12/C17/C22 | Sin borrar historia ni consultar otro tenant |
| **E05** Puesto empleado; personal asignado | Identificar cola de trabajo permitida | N13 · P08/T01-R04 · C05/C06/C13 | Sin datos de otra sucursal ni finanzas por defecto |
| E05; empleado | Registrar progreso de tarea con historia inmutable | N14 · P07/P08/T01-R04 · C13/C17/C26 | No actualizar expediente ajeno ni sobrescribir eventos |
| E05; empleado | Comprender permisos y revocación de acceso | N15 · P06/P11/T01-R04 · C05/C06 | No reutilizar sesión revocada ni confiar en botón oculto |
| **E06** Sitio/portal consumidor; cliente contextual | Explorar negocio verificado y catálogo público | N16 · P03/P04/P05/T01-R05 · C04/C10/C20 | Rechazar host falso, borradores y contenidos ajenos |
| E06; consumidor | Solicitar reserva/compra bajo contexto autorizado | N17 · P06/P07/T01-R05 · C05/C06/C12/C13 | Sin persistencia/cobro no autorizado ni reintentos duplicados |
| E06; consumidor | Ver comprobantes, consentimiento e historial **propios** | N18 · P08/P09/T01-R05 · C06/C17/C23 | Sin fuga por caché, sesión o historial de otro tenant |

**Centro de demostración:** modo transversal sintético T01-R06, expresado en N03; **no** es E07, persona con privilegios, acceso anónimo a datos privados ni una sexta clase de usuario autenticado. Los seis espacios UX pueden compartir una implementación modular, componentes y API autorizada. Los tipos de cuenta y las experiencias **no** equivalen automáticamente a seis roles de backend.

## 3. Secuencia omnicanal del Word, F01–F07

| Paso | Referencia | Tarea objetivo | Control que impide un atajo inseguro |
| --- | --- | --- | --- |
| F01 | P03 | Consumidor accede por ruta/subdominio/dominio propio | El host nunca autoriza privacidad |
| F02 | P04 | Servidor identifica dominio legítimo y marca publicada | Rechazo de host manipulado/tenant ambiguo |
| F03 | P05 | Consulta información pública del negocio | No publicar borradores ni datos privados |
| F04 | P06 | Solicita operación y se autoriza actor, tenant y acción | Login sin selector y autorización solo backend |
| F05 | P07 | Operación transaccional y evento recuperable | Integridad e idempotencia futuras |
| F06 | P08 | Negocio ve actividad y cliente **solo** lo suyo | Proyecciones por tenant/sucursal/recurso |
| F07 | P09 | Métricas, auditoría y posibles extensiones | PII no visible por conveniencia; pagos solo en su capítulo |

La secuencia no crea rutas ni eventos. Su implementación se delega conforme a `dependencies` y `deferred_to` del contrato JSON. El seguimiento universal G.7 requiere posteriormente historial inmutable y vistas por rol (`UC-036..UC-040`), nunca autorización por navegación.

## 4. Criterios de calidad UX para todos los recorridos

- Cada acción tendrá **nombre accesible** visible o equivalente para lector de pantalla; botones iconográficos con tooltip y alternativa táctil comprensible. Navegación teclado, enfoque claro, modales focalizados y sin trampas.
- Formularios en pasos, errores explicativos, vacíos orientativos, indicadores de carga y confirmaciones **sin exponer datos privados**; el diseño previsto de C02-05/06 especificará estados y recuperación.
- Responsive escritorio/tableta/móvil con las mismas tareas esenciales; **WCAG 2.2 AA** y preferencia de movimiento reducido `prefers-reduced-motion` son criterios futuros, **NO** resultados de una auditoría visual de esta microfase.
- Datos, temas, logos, encabezados, textos, pie, derechos y configuración siempre versionados según ámbito y permisos administrativos; el producto no debe requerir modificación de código para ajustes editoriales autorizados. No habilitar código arbitrario desde la configuración.
- Si falla autenticación, consulta, conectividad o cambia el tenant, el comportamiento seguro es **denegar y limpiar información privada del contexto anterior**, sin fallback a caché ajena.

## 5. Pruebas, auditorías y restricciones de entrega

**A / trazabilidad:** leer Word binario validado por SHA, contar 11 párrafos y 6 filas de persona, validar E01–E06 y F01–F07 en orden, cada `N01..N18` con referencias existentes, dependencia conocida y positivo/negativo observables. Negativas sintéticas al borrar/duplicar IDs y al referenciar otro capítulo sin contrato.

**B / seguridad por diseño:** cada necesidad de ámbito privado exige un caso negativo y marcador de autorización backend; rechazo del tenant picker en login y permisos implícitos. Demo sintética y PII excepcional protegidas. **Estas auditorías son del contrato de diseño**; pruebas reales de BOLA/IDOR/MFA/cross-tenant privado NO EJECUTADAS y obligatorias en los capítulos que creen la superficie.

**C / arquitectura, UX y rendimiento:** formato JSON válido, cobertura exacta 6×3 necesidades y 7 pasos, estados de UX/a11y en especificación, dependencias a capítulos posteriores, vínculos Markdown válidos, sin efectos de código de aplicación. Compilaciones de regresión Angular 22 y .NET 10 existentes se ejecutan en CI; no se considera prueba de la futura UX.

**Archivos:** `docs/c02/C02_02_ESPECIFICACION.md`, `docs/c02/C02_02_MAPA_NECESIDADES.json`, `qa/c02_02_audits.py` y workflow CI de solo lectura. Efecto `frontend/` = ninguno, `backend/` = ninguno, `database/` = ninguno; sin DTO, endpoint, migración, PII ni secretos. **Rollback:** revert del PR de C02-02 sobre `dev`, sin modificar la implementación previa C01/C02-01.

**Gate:** PR, CI real último HEAD con A/B/C y controles negativos, merge SHA esperado, CI postmerge, acta técnica y avance **2/9**, sin certificar C02 ni iniciar C02-03 antes de ese cierre.
