# CRECISOL C06-01 — Especificación y matriz canónica de permisos

**Microfase:** C06-01, Inventario de acciones y permisos. **Tipo:** catálogo de contrato, no motor de autorización. **Fuente:** Documento Maestro Único C06, plan de 37 capítulos, `docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md` y código `dev` SHA `659477eef45780d5b36ffac95162075dd0aded24`.

## Única matriz versionada y delimitación

**Fuente de verdad en este alcance:** [`backend/contracts/authorization/c06_01_actions.json`](../../backend/contracts/authorization/c06_01_actions.json). No se duplica ni deriva de textos de botones. Cada acción observada declara `action_id`, módulo, método HTTP, ruta, código operativo, realm, límite mínimo de acceso, campos contractuales, modalidad de exposición y ubicación de fuente.

| Superficie auditada | Filas | Significado |
| --- | ---: | --- |
| Identidad y cuenta tenant (POST) | 44 | Rutas operativas condicionadas a habilitación local, con control de sesión, credencial o prueba según operación |
| Operadores plataforma (POST) | 31 | Realm de operadores separado; no equivale a SuperAdmin de todo el SaaS |
| Branding público (GET) | 1 | Proyección pública estricta, sin concesión de permisos |
| Registro/confirmación de buzón (POST) | 3 | Rutas de alta pública contextuales y condicionales, con validación/recibo neutral |
| Consulta privada de casos (GET) solo arnés | 1 | Implementación disponible para pruebas desechables; **NO mapeada en `Program.cs`** |
| Contratos internos de C04 (no HTTP) | 12 | Interfaces de casos de uso y contexto multitenant; **NO son endpoints ni grants** |

**Total:** 80 entradas HTTP declaradas (79 registrables en producto bajo las condiciones señaladas, 1 exclusivamente de arnés) más 12 contratos internos fuera de HTTP. **No son 80 permisos concedidos:** las rutas públicas o de preautenticación no son privilegios y el catálogo no se conecta todavía a un autorizador.

## Criterios de ámbito (target de C06, no concesiones actuales)

| Categoría `access_boundary` | Regla de inventario / futura obligación |
| --- | --- |
| `anonymous_public_projection` | Proyección sin datos privados y host admitido; no exige sesión empresarial |
| `tenant_preauth_or_proof` | Solicitudes de login, MFA, recuperación y registro; no conceden membresía por sí solas |
| `tenant_own_account` / `tenant_own_session` | Sesión y membresía revalidadas en backend; datos únicamente del titular |
| `tenant_approver_grant` | Verificación efectiva de permiso, tenant y caso de aprobación, antes de consultar datos |
| `tenant_branch_resource_grant` | Tenant, sucursal, acción, recurso y propietario verificados por servidor |
| `platform_operator` / `platform_session` | Realm PLATFORM separado del usuario de tenant; no heredar privilegios de negocios |
| `platform_privileged` | Operaciones delicadas justificadas, verificadas, restringidas y auditadas |
| `platform_recovery_or_challenge` | Comprobantes de desafío/recuperación; jamás acceso general por conocer un token |

**Nota de seguridad:** `code_field_present` informa únicamente que el contrato acepta un campo `code`; **no afirma** que la prueba TOTP esté fresca o verificada. Es la implementación transaccional de C05 la que debe hacerlo. Todos los `c06_policy_bound` son `false`, todos los `ui_grant_source` son `false` y `authorization_from` exige `backend_only`. Las capacidades asignables, roles, ramas y reglas ABAC se implementarán recién en C06-02 a C06-04 con pruebas propias.

## Fuentes y límites reales

- `Program.cs` publica GET de branding y compone condicionalmente el registro, la comprobación de correo, la finalización de registro y el módulo de identidad local.
- `OperationalIdentityModule.cs` registra las **75 operaciones** POST desde un diccionario cerrado: 44 de identidad/tenant y 31 de plataforma.
- `PrivateResourceReadModule.cs` tiene ruta GET reutilizable por harness y verificaciones de recurso/tenant; actualmente no se registra desde el producto.
- Las operaciones de `backend/src/Crecisol.Application/Tenancy/` son contratos internos de C04, no URLs registradas. Módulos futuros C07–C37 **no son operaciones existentes** por figurar en un plan.
- Si el backend agrega un método, ruta u operación, la auditoría bloqueará la CI hasta que la matriz y su alcance se revisen. Ningún permiso puede nacer de un nombre de pestaña, Angular guard, claim o parámetro del cliente.

## Pruebas y auditorías C06-01

- **A**: cargar catálogo, recorrer la tabla real de 75 operaciones, comprobar contratos de campos, mapeo HTTP y los otros cinco patrones; extraer y comprobar el capítulo 06 del Word canónico (SHA-256).
- **B**: nueve mutaciones negativas del catálogo (omisión, duplicado, degradación de realm plataforma, pérdida de grant de recurso, UI como fuente, autorización prematura, fuente frontend, contrato omitido, arnés publicado) y aserciones de host, origen y encabezados controlados por backend.
- **C**: asegurar >10 módulos funcionales, separar el arnés, respetar arquitectura y reconstruir .NET 10 y Angular 22 en CI sin cambios de `frontend/src`, `backend/src` ni migraciones.
- **No ejecutadas aquí:** intentos ofensivos BOLA/IDOR de la implementación C06, prueba de roles asignados y escaneo de producción; corresponden a C06-02..C06-10. No atribuirles PASS retrospectivo.

## Reversibilidad / salida

Cambio limitado a contrato JSON, documentación y QA/CI. Revertir el commit o PR revierte todo el inventario sin afectar sesiones, SQL, secretos, endpoints ni permisos actuales. La microfase solo podrá cerrarse al pasar pruebas A/B/C efectivas y registrar HEAD, merge a `dev`, POSTMERGE y acta; C06-02 permanece bloqueada mientras C06-01 esté pendiente.
