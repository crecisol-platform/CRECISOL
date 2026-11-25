# CRECISOL — C06-04: autorización ABAC por acción, sucursal y propiedad

**Fecha:** 24-11-2026. **Estado:** candidata NO CERTIFICADA. **Exigencia:** calidad técnica, funcional y de seguridad >=9.5/10; cero P0/P1 y evidencia real A/B/C, MySQL 8.4 y E2E antes de integrar y certificar.

## Autoridad y dependencia
Repositorio CRECISOL original ID 1450414592 (ubicación crecisol-platform/CRECISOL), rama base dev SHA af01c2e112c41c76a70effc4c271a1c8a30670b0. Documento Maestro Único Word, SHA-256 aprobado ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c; reglas AGENTS.md, Skill CRECISOL, docs/MASTER_REFERENCE.md, C06-01/02/03 y estado vigente de capítulos.

Entregable: evaluar **actor autenticado, rol de membresía, acción exacta del servidor, tenant, sucursal, propiedad del expediente y asignación individual**. Nadie recibe autoridad por presentar un ID válido desde el navegador.

**Límite real de ejecución:** la única operación activa del producto que lee un recurso privado de tenant en C05 es POST /api/v1/identity/private/read. La ruta GET /api/v1/private/cases/{resourceId} pertenece exclusivamente al arnés no productivo de C05 y NO está registrada en Program.cs. La clave canónica cases.read se usa como acción fija del backend; la matriz C06-02 y sus 28 acciones futuras siguen no concedibles. Sesiones, recuperación, MFA, perfil y membresías conservan sus propios controles C05/C06-03. C06-05/06/07/08/09/10 requieren puertas independientes. NO afirmar que se implementó un motor completo para endpoints todavía inexistentes.

## Arquitectura y decisión
- Domain: backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs. Función pura fail-closed; contexto C06-03, rol real de fila SQL, acción fija cases.read, permiso de servidor RESOURCE_READ, recurso y branch de catálogo, propietario, grant válido y asignación.
- Infrastructure: backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs. Lee recurso y grant de forma parametrizada, con FOR SHARE dentro de la **misma transacción C05**, y utiliza hora real UTC_TIMESTAMP(6); actor/tenant nunca se aceptan de fields, headers o claims. Bloquea duplicidad de grants (LIMIT 2, count=1), conserva el recurso autorizado en la misma fila bloqueada.
- Integración: Authorized.cs reusa contexto ya comprobado en C06-03 y llama al evaluador antes de devolver referencia privada; no crea endpoint nuevo.
- Propietario y grant exacto (scope 1): ALLOW solo recurso propio y actor coincidente.
- OWNER / ADMIN / tenant-owner / tenant-admin con grant explícito de sucursal (scope 2): ALLOW exclusivamente en su sucursal y tenant, nunca por rol solamente ni por una propuesta C06-02.
- Empleado, encargado o lector con grant amplio legacy (scope 2): adicionalmente necesita asignación activa, exacta e individual por tenant+actor+branch+resource+cases.read; sin asignación DENY. No puede leer otros casos de la misma sucursal.
- Roles PLATFORM, SuperAdmin, tenant-custom, roles desconocidos, acciones wildcard, recursos suspendidos, caducados o revocados: DENY genérico sin enumeración.
- Sólo el servidor decide action/permission/role/owner. C06 no concede permisos desde frontend.

## Datos y reversión
- 020_c06_case_abac.sql introduce **una tabla aditiva** de asignaciones con UNIQUE y FK compuestas tenant+branch+case, tenant+cuenta y tenant+aprobador, CHECK de acción cases.read, plazos max 30 días y triggers anti-reparenting/anti-revocación irreversible. NO hay seeds ni API de emisión.
- database/operations/c06_04_local_runtime_role.sql añade **solo SELECT** al rol local C05 sobre la tabla, sin privilegios DML.
- Reversión en base MySQL desechable usando 020_c06_case_abac.down.sql; nunca ejecutarla contra producción/datos reales sin copia y autorización.

## Pruebas exigibles
**A:** Maestro intacto, inventario de C06-01 (80 HTTP+12 internos+28 futuras), catálogo C06-02 (10 roles/108 acciones), ramas/diff y operación protegida exacta.

**B:** múltiples contratos .NET positivos y negativos: mismo tenant/branch pero otro dueño, otro empleado con mismo rol, ID ajeno, action distinta, permiso ausente, grant revocado/duplicado, branch y tenant cruzados, role PLATFORM/SuperAdmin, asignación pendiente/vencida/ajena, revocación. MySQL 8.4 real: FK/UNIQUE/CHECK, tiempos, mutaciones, rollback. E2E opt-in C06-04 en Angular→API→MySQL: acceso verdadero propietario, grant amplio solo DENY, asignación específica ALLOW, caso no asignado DENY, revocación DENY, sin fuga entre organizaciones.

**C:** compilación backend .NET 10 con advertencias como errores, Angular 22 types/build y Chromium, auditorías A/B/C; E2E histórico C05 sin bandera debe seguir funcionando. Sin nuevas pantallas: nota de UX visual no aplicable, pero rendimiento/seguridad/ingeniería requiere >=9.5/10 verificado.

## Certificación y riesgos
No certificar esta microfase por una compilación o propuesta de PR. Exigir CI A/B/C PREMERGE sobre SHA exacto, E2E HTTP real, pruebas negativas, regresiones C05, integración solo en dev, POSTMERGE y acta institucional propia. C06-04 puede certificar únicamente las políticas operativas dentro del alcance declarado. Antes del cierre: 3/11 microfases C06 certificadas, 5/37 capítulos completos; C06 global NO CERTIFICADO; C06-05/C07 no habilitadas. Sin modificar main, producción, Word/PDF, datos reales, VPS, cobros ni SOLQARYN.
