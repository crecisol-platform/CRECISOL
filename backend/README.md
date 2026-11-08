# CRECISOL · Backend del capítulo 01

API pública de marca con ASP.NET Core 10, Domain / Application / Infrastructure. Solo lectura, sin autorización de tenant ni edición.

**CI verificada:** SDK .NET 10.0.403, compilación Release, 11 grupos de pruebas de dominio y contrato HTTP local, aprobados en https://github.com/solqaryn-platform/CRECISOL/actions/runs/42516130198.

## Ejecución local (requiere SDK .NET 10)

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5180 dotnet run --project backend/src/Crecisol.Api/Crecisol.Api.csproj
dotnet run --project backend/tests/Crecisol.Branding.SmokeTests/Crecisol.Branding.SmokeTests.csproj
```

Endpoint público: `GET /api/v1/public/branding/platform`.

La fuente JSON está versionada para bootstrap, **no** es un CMS ni persistencia MySQL real. No hay endpoints de tenant, login, SuperAdmin ni escritura. No habilitar esas superficies sin autenticación y autorización backend; la consulta pública no otorga permisos.
