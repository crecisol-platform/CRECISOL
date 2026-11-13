# Backend CRECISOL

Área reservada para ASP.NET Core 10 / net10.0, Clean Architecture y módulos verticales ligeros. Domain independiente, Application por casos de uso, Infrastructure con adaptadores, API delgada, Workers con outbox cuando se implementen.

Tenancy, auth, privacidad, historial y superadministración se validan en servidor. Existe una API ASP.NET Core 10 mínima con una sola lectura pública GET `/api/v1/public/branding/platform`. C03 está cerrado y certificado en 10/10 microfases dentro de su alcance técnico; C03-07 implementa solamente un relay de prueba PLATFORM desactivado y sin efectos comerciales. Los módulos privados, login, autorización multitenant, workers productivos y endpoints de negocio no están implementados y pertenecen a C04 y capítulos posteriores. C04 está en desarrollo, sin API privada ni despliegue productivo.

## C01-02 · Contratos de identidad (código real)

El Domain (net10.0) está implementado sin dependencias NuGet de terceros. Contiene BrandSnapshot, validación de marca y estilos, estados Draft/Published, preview descriptivo y propuesta de publicación SIN ejecutor. Se puede reproducir la prueba con SDK .NET 10:

~~~sh
dotnet build backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj -c Release
dotnet run --no-build -c Release --project backend/tests/Crecisol.Branding.ContractsTests/Crecisol.Branding.ContractsTests.csproj
~~~

En GitHub Actions #42521639367 compiló con cero advertencias/errores y ejecutó 51 aserciones. **Aún no existe API ejecutable integrada en dev, MySQL conectado, login ni endpoints administrativos**; las futuras integraciones requieren pruebas propias. [Contrato y límites](../docs/c01/C01_02_CONTRATOS.md).
