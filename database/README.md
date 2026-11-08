# CRECISOL · Diseño relacional de identidad (C01)

MySQL 8.4 LTS, InnoDB y separación explícita de ámbitos de marca.
El archivo migrations/0001_c01_brand_identity.sql define:
- Brand scope, con identidad PLATFORM y, como previsión, TENANT.
- Revisiones de marca versionadas y un puntero separado de publicación.
- Claves compuestas y relaciones que impiden enlazar revisiones entre ámbitos.

**NO EJECUTADO / NO APROBADO PARA PRODUCCIÓN.**
La vinculación de scope TENANT al catálogo autorizado y la autenticación,
triggers o permisos de inmutabilidad, auditoría y publicación se incorporarán
con sus capítulos y pruebas reales de MySQL. La existencia del DDL no permite
a un usuario elegir conexiones ni leer marcas ajenas por URL.
