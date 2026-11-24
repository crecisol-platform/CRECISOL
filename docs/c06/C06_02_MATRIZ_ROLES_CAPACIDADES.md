# CRECISOL — C06-02: matriz funcional de roles, capacidades y alcances

**Estado de esta ampliación:** candidata de corrección C06-02; **pendiente de pruebas, auditorías A/B/C, integración y acta propia de adenda**. No se amplía la certificación original por el solo hecho de registrar este documento.

**Autoridad:** Documento Maestro Único C06 (huella aprobada bajo `docs/MASTER_REFERENCE.md`), inventario C06-01 `docs/c06/C06_01_CATALOGO_ACCIONES.json`, arquitectura C04/C05 y gobernanza CRECISOL. **Fuente estructurada y única de esta matriz:** [C06_02_MATRIZ_ROLES_CAPACIDADES.json](C06_02_MATRIZ_ROLES_CAPACIDADES.json). Este Markdown explica el contrato; las pruebas consumen directamente el JSON y rechazan divergencias.

## 1. Qué queda definido

El catálogo clasifica **108 acciones exactas** (80 operaciones HTTP existentes y 28 acciones normativas futuras) junto a **12 puertos internos no HTTP**. Las 108 acciones se clasifican una por una; el estado `role_candidate` expresa intención del modelo, **jamás una concesión en producción**. Cada acción incluye ámbito `tenant/platform/public`, módulo, frontera mínima existente, perfiles candidatos, verificaciones exigibles y tres bloqueos: `grantable=false`, `policy_activated=false`, `inherited_across_tenants=false`.

Se distinguen las operaciones propias de la cuenta (perfil, contraseña, MFA, passkeys y sesiones), los flujos con prueba preautenticada, la única proyección pública de marca, la ruta exclusiva del arnés, las operaciones empresariales, la privacidad del titular y las excepciones de plataforma. **Las operaciones personales/públicas no se convierten en permisos delegables** por aparecer en el catálogo.

## 2. Funciones por perfil — propuestas no operativas

| Perfil canónico | Ámbito | Funciones candidatas | Prohibiciones explícitas |
| --- | --- | --- | --- |
| `platform-operator` | Plataforma verificada | Resumen de abusos, metadatos de soporte; autogestión de cuenta y sesiones permanece por C05 | Nada de lectura privada tenant, acceso empresarial, recuperación privilegiada o superadmin implícitos |
| `platform-security-operator` | Plataforma verificada | Casos de plataforma, revisión, compartición protegida, políticas; MFA/step-up, motivo, expiración y bitácora | Sin privilegio automático a tenant; no hereda break-glass |
| `platform-incident-responder` | Excepción de plataforma | Reserva semántica para procedimientos temporales de C06-06 | **Cero** capacidades candidatas activas; break-glass siempre bajo puerta específica |
| `tenant-owner` | Empresa + sucursales verificadas | Gobierno de membresías, delegación condicionada al techo, consulta y cierre de expedientes, exportación privada solo sujeta a política | Sin ingresos, márgenes, caja o reembolsos implícitos; sin plataforma |
| `tenant-admin` | Empresa + sucursales verificadas | Subconjunto administrativo verificable, invitaciones y aprobaciones de membresía; casos permitidos | No supera techo del propietario; sin herencia entre empresas ni privilegio financiero automático |
| `tenant-branch-manager` | **Sucursal explícita** | Lectura, notas, diagnóstico, solicitud de cotización, entrega, cierre de caso en sucursal asignada | No administra membresías empresariales ni sucursal ajena; no puede concederse finanzas/exportación |
| `tenant-staff` | **Sucursal explícita** | Lectura operativa, notas, cotizaciones, entrega y canales en sucursal asignada | No cierre como autoridad general, no aprobaciones ajenas ni política de roles |
| `tenant-viewer` | **Sucursal explícita** | Lectura contextual de casos y suscripciones autorizadas | Sin edición, cierre, exportación o función administrativa heredada |
| `tenant-finance-specialist` | Empresa + sucursal + autorización sensible independiente | Caja, ingresos, margen, reportes y reembolsos **solo como propuestas** | Requiere segregación, step-up, límites y auditoría; nunca deriva de ser owner/admin |
| `tenant-custom` | **Sucursal explícita** | Perfil extensible futuro con selección individual de capacidades bajo el techo del otorgante | **Cero capacidades predeterminadas**, wildcards y herencia prohibidos |

El consumidor/titular de datos **no** se modela como un empleado ni como un rol tenant: su aprobación de cotizaciones y sus derechos de privacidad corresponden a verificación de titular y del recurso, nunca a una jerarquía empresarial. Tampoco se implementa un SuperAdmin omnipotente.

## 3. Funciones y restricciones que no pueden confundirse

- **Plataforma vs empresa:** toda función candidata pertenece a un solo reino; un operador global no hereda información interna de organizaciones. El soporte excepcional se reserva exclusivamente a C06-06 con MFA, motivo, plazo, aprobaciones y bitácora.
- **Empresa vs sucursal:** el tenant se verifica desde el servidor. Un encargado o colaborador requiere sucursal explícitamente aprobada; **no existe asignación automática a todas las sucursales ni acceso a otra empresa**.
- **Propietario/administrador:** la delegación tiene techo verificable y no incluye permisos financieros, reembolsos, privacidad del titular, exportación sensible ni herramientas de plataforma por defecto.
- **Roles personalizados:** futuros permisos específicos serán acciones nominales individuales, sin patrones `*`, solo bajo política de delegación aprobada. El rol `tenant-custom` nace siempre vacío.
- **Operaciones propias y públicas:** no son grants de negocio. La ruta `private.case.read.harness_only` no constituye un endpoint concedible.
- **Operaciones futuras:** las 28 acciones siguen `http_registered=false`, `grantable=false`. La tabla SQL C06 `019_c06_prepared_roles.sql` tampoco puede activar grants.
- **Asignación verificable:** la infraestructura C06-02 ya ofrece propuestas ligadas a `tenant_id`, membresía, rol y `branch_id`; la lectura con actor/acción/ventana/revocación es de **solo inspección**, `AuthorizesPrivateResource=false`. C06-03 resolverá identidad/membresía real; C06-04 aplicará la política ABAC sobre recurso. Esta matriz no anticipa esas puertas.

## 4. Pruebas obligatorias para cerrar la adenda

El auditor independiente `qa/c06_02_role_matrix_audit.py` verificará:

- **A:** paridad completa de 80 + 28 acciones y 12 puertos; diez perfiles, alcance/atributos coherentes con C06-01 y Maestro, sin una sola acción inventada, duplicada u omitida.
- **B:** mutaciones ofensivas sobre herencia entre organizaciones, separación PLATFORM/TENANT, sucursales, funciones financieras, aprobaciones del titular, break-glass, grant por UI, exportación y permisos predeterminados.
- **C:** comprobación de la restricción SQL de realm (`platform-*` y SuperAdmin excluidos de `c06_tenant_role`), FK compuestas, estado `proposal_only`, autoridad estricta en backend y compilaciones/regresiones existentes.

Se suman pruebas .NET por encargado de sucursal y MySQL real de propuestas, no herencia y rollback. La **nota de calidad ≥9.5/10** solo podrá asignarse al alcance técnico si todos los gates pasan en el SHA definitivo. No hay interfaz gráfica nueva que pueda recibir evaluación visual.

## 5. Integración, reversión y riesgo

Cambios previstos: este JSON/Markdown, auditoría específica, endurecimiento acotado de migración 019, pruebas .NET/MySQL y su workflow. **Sin** alterar las API C05, módulos Angular, sesiones, MFA, `main`, datos privados, producción, VPS, cobros ni SOLQARYN. El rollback del cambio documental es revertir el PR; si se hubiera aplicado SQL únicamente en base desechable, conservar copia y usar la reversión 019 tras comprobación de no pérdida de datos C05.

**Dictamen de esta revisión:** el entregable funcional de C06-02 se considerará completo únicamente después de verificar la matriz, las pruebas negativas, las tres auditorías PREMERGE/POSTMERGE y su acta complementaria. C06 global sigue sin certificarse.
