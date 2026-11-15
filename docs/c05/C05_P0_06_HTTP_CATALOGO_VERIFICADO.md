# CRECISOL C05-P0-06 — Catálogo único de Host verificado para HTTP privado

**Alcance:** código, pruebas y documentación de la rama del PR #151, sin deployment.

La ruta privada de ensayo deja de depender de `FixedTestHostCatalog`. Utiliza `CanonicalTenantHost.Parse` y `ResolveVerifiedTenantHost`, el mismo resolvedor de dominio verificado que ya usan registro y login contextual. Este resolvedor exige vinculación única y evidencia UTC válida; las entradas inexistentes, ambiguas incluso para un mismo tenant, caducadas o suspendidas niegan acceso. Después, `ReadMfaAuthorizedPrivateResource` valida sesión MFA, tenant, miembro, sucursal, recurso, propietario y acción en MySQL 8.4.

**Pruebas esperadas:** Host A contra bearer B, host inválido con punto final, dos entradas duplicadas, entradas fuera del catálogo, headers falsificados y revocación de factor/permisos/sesión. Ejecutar .NET 10, MySQL 8.4, auditorías A/B/C y regresiones C03/C04 en el SHA de integración antes de declarar cierre técnico.

**NO-GO operativo:** esta unificación no habilita la API privada real en `backend/src/Crecisol.Api/Program.cs`, no integra aprobador humano autenticado con MFA ni frontend Angular autenticado; quedan P0/P1 funcionales y de migración productiva. Por tanto no se certifica C05 completo, no se fusiona #151, no se habilita C06, no se hacen despliegues ni se modifica SOLQARYN. La evidencia de ejecución y SHA deben constar en #150 tras su verificación.

## Siguiente subcorte — contrato de ruta reutilizable

Se añade `backend/src/Crecisol.Api/Modules/Identity/PrivateResourceReadModule.cs`: define la ruta real planificada `GET /api/v1/private/cases/{resourceId}` y exige, en el arranque del host, disponer de `PrivateResourceHttpRead` desde DI. El mismo módulo se monta y se prueba mediante Kestrel loopback y MySQL 8.4 descartable, sin rutas duplicadas ad hoc en el harness.

**Esencial:** el módulo existe en el código del producto, pero `Program.cs` NO lo registra ni obtiene credenciales de ensayo. Esto NO habilita todavía la API operacional. Se certificará solo la composición de ensayo si se completan A/B/C y pruebas MySQL en el mismo SHA.
