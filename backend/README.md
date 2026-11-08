# CRECISOL · Backend inicial (capítulo 01)

Código desde cero, ASP.NET Core 10, con Domain, Application, Infrastructure y API.
Solo existe lectura pública del registro de marca de CRECISOL.

## Desarrollo local

Requiere SDK .NET 10 instalado y comprobado. Ejecutar desde la raíz:

ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5180 dotnet run --project backend/src/Crecisol.Api/Crecisol.Api.csproj

Probar GET http://127.0.0.1:5180/api/v1/public/branding/platform.
Tests de dominio: dotnet run --project backend/tests/Crecisol.Branding.SmokeTests/Crecisol.Branding.SmokeTests.csproj

El origen JSON es **bootstrap local de solo lectura**; no representa todavía la
persistencia MySQL ni un editor seguro. No hay endpoints de tenant, login, alta
de negocios, SuperAdmin ni modificación de parámetros; nunca se deriva
autorización desde Host, query string o IDs suministrados por el navegador.

**No ejecutado en esta sesión:** falta SDK .NET 10 local. Las pruebas deben
ejecutarse realmente, documentarse y aprobarse antes de certificar el capítulo.
