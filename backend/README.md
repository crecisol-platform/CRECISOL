# Backend CRECISOL

Área reservada para ASP.NET Core 10 / net10.0, Clean Architecture y módulos verticales ligeros. Domain independiente, Application por casos de uso, Infrastructure con adaptadores, API delgada, Workers con outbox cuando se implementen.

Tenancy, auth, privacidad, historial y superadministración se validan en servidor. No existe API ejecutable todavía.

## C01-02 · Contratos de identidad (código real)

El Domain (net10.0) está implementado sin dependencias NuGet de terceros. Contiene BrandSnapshot, validación de marca y estilos, estados Draft/Published, preview descriptivo y propuesta de publicación SIN ejecutor. Se puede reproducir la prueba con SDK .NET 10:

~~~sh
dotnet build backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj -c Release
dotnet run --no-build -c Release --project backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj
~~~

En GitHub Actions #42521639367 compiló con cero advertencias/errores y ejecutó 51 aserciones. **Aún no existe API ejecutable integrada en dev, MySQL conectado, login ni endpoints administrativos**; las futuras integraciones requieren pruebas propias. [Contrato y límites](../docs/c01/C01_02_CONTRATOS.md).
