# CRECISOL · Modelo de marca del capítulo 01

Esquema relacional InnoDB para MySQL 8.4 LTS: ámbitos PLATFORM/TENANT, revisiones y puntero de publicación, claves y FK compuestas.

**Prueba real aislada:** CI #42516130198 ejecutó el DDL y fixtures en MySQL 8.4.13 dentro de contenedor desechable sin red. Verificó dos publicaciones sintéticas y cinco negativas rechazadas (scope cero incorrecto, ID de plataforma incorrecto, case, FK entre tenants y revisión cero).

Scripts: `database/migrations/0001_c01_brand_identity.sql` y `database/tests/c01_brand_schema_smoke.sh`.

**NO es una migración productiva:** no se conectó a bases reales. Falta vínculo al catálogo confiable de tenants, servicios de autorización, auditoría inmutable, rollback y pruebas del modo dedicado/compartido. Ningún cliente puede elegir la conexión desde URL.
