# CRECISOL — C03-02 · Contratos Domain puros

**ESTADO: INICIADA EN RAMA TÉCNICA; NO CERTIFICADA.** Solo C03-01 está certificada (1/10). **C03 NO CERTIFICADO; C04 BLOQUEADO.** No existe aprobación humana anticipada para C03-02.

## Autoridad, entrada y trazabilidad

Repositorio exclusivo `solqaryn-platform/CRECISOL`, rama `feature/c03-02-domain-contracts`, base `dev` SHA `8e4f25073a3f11150e4323ecb66380647f0cdc0b`. El PR #50 formalizó C03-01 y sus **7/7 workflows POSTMERGE SUCCESS** en ese SHA; [acta previa](C03_01_ACTA_MICROFASE.md).

La autoridad es el Documento Maestro Único Word SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`. Se conserva su frontera C03/C04 y la matriz real de C03-01: **T01-R02 Domain (entidades, invariantes y eventos puros, sin HTTP/ORM/SDK)**, **P03** árbol recomendado y **P04** dependencias y límites. No adelantar eventos/outbox de C03-07. La diferencia **DIF-C03-01** sigue vigente: las puertas normativas `MAE-C03-01..08` del Word no equivalen a las diez microfases técnicas `C03-01..10`.

## Alcance físico preciso

- `backend/src/Crecisol.Domain/Common/DomainRevision.cs`: valor de versión positivo, inmutable, secuencia estrictamente creciente, control de overflow; no representa control transaccional.
- `backend/src/Crecisol.Domain/Branding/BrandScopeKey.cs`: identidad de agregado PLATAFORMA versus TENANT como valor inmutable. **No concede derechos de acceso.**
- `BrandSnapshot`, `BrandPreviewReference` y `BrandPublicationProposal` reutilizan ambas reglas sin cambiar sus firmas públicas, evitando reglas duplicadas e impactos funcionales C01.
- `backend/tests/Crecisol.Domain.ContractsTests/`: ejecutable net10.0 independiente de NuGet externo con escenarios positivos y negativos de revisión, ámbitos, vista previa y propuesta.
- `qa/c03_02_domain_audits.py` y `.github/workflows/c03-02-domain.yml`: auditorías A/B/C, mutaciones adversariales sobre referencias/SDK/HTTP/SQL y verificación de compilación/pruebas reales en CI.

**Prohibido ampliar el alcance:** no tocar `frontend/`, `database/`, `Crecisol.Application`, `Crecisol.Infrastructure`, `Crecisol.Api`, rutas privadas, migraciones, autenticación, conexiones de cliente, workers, producción ni SOLQARYN. Los módulos futuros Identity, Tenants, Catalog, Orders, Payments, Scheduling, Memberships, Media, Analytics y Cases siguen solamente PLANIFICADOS.

## Contratos de seguridad y límites

1. `Crecisol.Domain` conserva `net10.0` sin `ProjectReference`, `PackageReference`, HTTP, ORM, SQL, SDK ni service locator. Se mantiene el grafo acíclico aprobado en C03-01.
2. La revisión propuesta debe ser positiva, estrictamente mayor que la versión actual (que puede valer cero), y no desbordarse al generar sucesor. La transacción y optimistic concurrency real deberán aplicarse en backend cuando correspondan.
3. La clave `Platform` exige `Guid.Empty`; `Tenant` exige GUID distinto de cero. Comparar claves **NO** verifica la identidad del actor, pertenencia, scope autorizado, propiedad de recurso ni `TenantContext`. No existe aquí seguridad BOLA/IDOR/MFA ejecutable de módulos privados.
4. Los contratos Domain no ejecutan código/configuración de cliente y no introducen datos, servicios ni efectos secundarios externos.

## Pruebas exigidas antes del cierre

| Auditoría | Evidencia | Negativas |
| --- | --- | --- |
| A — trazabilidad | SHA real Word, T01-R02/P03/P04, matriz original C03 y alcance del diff | No ampliar fase ni certificar capítulo |
| B — seguridad/arquitectura | .csproj puro, grafo físico y mutaciones de paquetes ORM, dependencia Infrastructure, HTTP/SQL y localizador | Deben ser rechazadas |
| C — compilación/regresiones | Compilar .NET10 Release API y ejecutable nuevo, reejecutar contratos de Domain C01 y Application, pruebas nuevas positivas/negativas | Revisión inválida/obsoleta/overflow, ámbito falsificado y vista previa inválida |

La CI **PREMERGE** y la CI **POSTMERGE** deben comprobarse sobre el SHA exacto. No se afirma **PASS**, cierre técnico ni **CERTIFICADA** por escribir esta especificación. Para certificación de C03-02 aún se necesitan las tres auditorías reales, acta y autorización expresa posterior.

## Riesgos y rollback

Riesgo: cambiar accidentalmente validación de marca C01; mitigación: regresiones C01 Domain/Application. Riesgo: tratar `BrandScopeKey` como autorización; mitigación: prohibirlo explícitamente y excluir cualquier mecanismo de permisos. Sin benchmark productivo, pruebas reales de tenants privados, VPS o pagos. **Rollback:** revert del PR técnico en `dev`, sin migración, pérdida de datos ni despliegue.
