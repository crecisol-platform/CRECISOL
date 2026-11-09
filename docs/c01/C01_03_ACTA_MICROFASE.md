# C01-03 · Acta técnica de revisión, pruebas y triple auditoría

**Fecha:** 2026-11-08 (Honduras; 2026-11-09 UTC).  
**Estado:** **CERRADA TÉCNICAMENTE C01-03 EN DEV** (3/8 microfases cerradas), tras auditorías A/B/C, pruebas MySQL/.NET y merge confirmado. El capítulo C01 NO está certificado (**0/37**).  
**Origen:** Documento Maestro Único CRECISOL SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e, verificado por qa/c01_verify_source.py.  
**Continuidad:** C01-01 y C01-02 cerradas. PR #15 integró DDL provisional antes de pruebas MySQL/A-B-C (merge c476f937ee49661b4e971a9bab2c70b24bfa89d1); PR correctivo #16 limita la remediación exclusivamente a C01-03.

## Evidencia efectivamente observada en GitHub Actions

Ejecución [#42541568404](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42541568404), SHA 5670d73e5589d12f54b16e8c7b0448486b564e14: **SUCCESS** de tres trabajos.

| Puerta | Evidencia | Resultado observado |
| --- | --- | --- |
| Auditoría A | Fuente Word SHA-256; matriz C01-01, IDs explícitos, tres tablas y cobertura de SQL | PASS |
| Auditoría B | Constraints de scope y Published, FK compuestas, negativos y pruebas sin red, VPS ni credenciales | PASS |
| Auditoría C | .NET 10 puro, límites Domain ↔ MySQL, 3 PK compuestas, InnoDB, UTC y reversión sintética | PASS |
| MySQL 8.4 REAL | MySQL **8.4.13**, contenedor efímero sin red, DDL aplicado, 9 aserciones positivas y 22 negativas de integridad | PASS **9/9 y 22/22** |
| Regresión Domain | Compilación .NET 10 Release, 0 warnings, 0 errors y 51 aserciones previas de contrato | PASS **51/51** |
| Foundation | Workflow de fundación del mismo PR y fuente canónica | PASS |

MySQL provino del contenedor aislado del runner, sin puerto expuesto, volúmenes persistentes, DSN o conexión a Hostinger. No se ejecutó ninguna migración productiva. Se registró un pin del digest observado del contenedor para hacer reproducible el último CI; su resultado final debe revisarse antes de integrar.

## Archivos y alcance del cambio

- database/c01_03_brand_schema.sql: scope Platform/Tenant, revisiones Draft/Published y punteros de publicación con FK compuestas; validaciones básicas y límites coincidentes con Domain.
- database/tests/c01_03_mysql_smoke.sh: regresión reproducible MySQL 8.4, errores de integridad verificados, rollback del esquema descartable.
- qa/c01_03_three_audits.py: auditorías independientes A, B y C.
- .github/workflows/c01-03-check.yml: pruebas read-only en PR específico, sin despliegues ni secretos.
- docs/c01/C01_03_MODELO_MYSQL.md y docs/C01_PREPARACION.md: trazabilidad, estado, límites y riesgos.

**Sin modificación** de Domain, API, frontend, Word/PDF maestros, servidores, DNS, pagos ni repositorio SOLQARYN.

## Riesgos pendientes y puerta para C01-04

Las restricciones SQL **no** realizan autorización de actor, pertenencia de tenant, validación de Host, resolución de DB shared/dedicated, auditoría de cambios ni control de acceso horizontal a SELECT; estos puntos requieren middleware, puertos/adaptadores y pruebas negativas end-to-end antes de cualquier endpoint privado. Se debe imponer la **inmutabilidad real** de revisiones frente a UPDATE/DELETE, transacciones compare-and-swap de publicación, tenant verificado y acceso DB con mínimos privilegios. El contrato .NET es válido, pero no hay aún **proveedor MySQL .NET instalado ni compatibilidad end-to-end del adaptador**; su selección y prueba pertenecen a C01-04, sin suponer éxito anticipado.

No hay bloqueos P0/P1 introducidos dentro del **alcance aislado schema-only**, porque no existe interfaz de persistencia ni publicación; esas capacidades siguen bloqueadas para exposición en C01-04+. La diferencia entre seguridad relacional y aislamiento autorizado debe permanecer explícita en documentación, pruebas y revisiones.

## Condiciones de cierre y reversión

La remediación se integra únicamente si **la CI definitiva del HEAD de PR #16 aprueba** MySQL real, Domain net10, Foundation y las auditorías A/B/C; luego comparar diff, comprobar ausencia de secretos, revisar commit y documentar el merge a dev. El propietario autorizó expresamente revisar y certificar esta microfase y había delegado cierres técnicos individuales sujetos a pruebas y tres auditorías. Esto **no equivale** a aprobación de C01-08, C02, un SaaS funcional o despliegue.

**Rollback del código:** revertir el PR #16 (o el PR #15 para retirar DDL inicial) mediante PR trazable. **Rollback SQL de prueba:** DROP de publicaciones, revisiones y ámbitos, en ese orden, verificado en la base desechable. No hay cambios persistentes reales que revertir.

## Cierre acreditado en GitHub

- [PR #16 de remediación](https://github.com/solqaryn-platform/CRECISOL/pull/16): **fusionado a `dev`**, SHA de merge `42c81b30347d43c718813a32df95b0ceb5566158`.
- [CI final del PR #42541904259](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42541904259): **SUCCESS** en auditoría A/B/C, MySQL 8.4.13 y Domain net10. [Fundación del mismo HEAD #42541904262](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42541904262): **SUCCESS**.
- [CI de fundación post-merge `dev` #42542009481](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42542009481): **SUCCESS** para SHA de merge `42c81b30347d43c718813a32df95b0ceb5566158`.
- Cierre documental posterior por PR exclusivo C01-03, sin introducir funcionalidades; la aprobación de microfase responde a la autorización explícita del propietario para revisarla y certificarla y a la delegación documentada en `docs/C01_PREPARACION.md`.

**Resultado:** C01-03 CERRADA TÉCNICAMENTE (modelo MySQL y controles de compatibilidad), 3/8 microfases; C01-04 es la siguiente microfase permitida después de este cierre documental. El capítulo C01 permanece NO CERTIFICADO, 0/37. Ninguna función administrativa o privada ha sido habilitada.
