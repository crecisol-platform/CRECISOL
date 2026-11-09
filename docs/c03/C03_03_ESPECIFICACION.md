# CRECISOL — C03-03 · Puertos y casos de uso Application

**ESTADO ACTUAL: C03-03 CERRADA TÉCNICAMENTE, ACEPTADA EXPRESAMENTE PARA CERTIFICACIÓN (3/10); FORMALIZACIÓN DOCUMENTAL CONDICIONADA A CI PREMERGE/POSTMERGE, todavía NO CERTIFICADA hasta cierre final.** C03-01 y C03-02 están **CERTIFICADAS (2/10)** como puerta anterior. **C03 NO CERTIFICADO, C04 BLOQUEADO, C03-04 NO INICIADA.** El propietario autorizó el cierre específico tras el PR técnico #53 (9 noviembre 2026); [acta](C03_03_ACTA_MICROFASE.md). La autorización inicial «Continúa por favor» del PR técnico no era autorización anticipada de certificación.

## 1. Puerta de entrada y fuente autoritativa

- Repositorio exclusivo: `solqaryn-platform/CRECISOL`, rama `feature/c03-03-application-ports`, originada de `dev` SHA **`edb254f57f6005a84ee97588e1ec7dd659ded3d3`**.
- El [PR documental de C03-02 #52](https://github.com/solqaryn-platform/CRECISOL/pull/52) se fusionó en ese SHA; **8/8 workflows POSTMERGE `push/completed/success`** sobre el SHA exacto, incluida [C03-02 A/B/C #42709633644](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42709633644), [regresión C03-01 #42709634047](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42709634047) y [C02 integral #42709633458](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42709633458).
- Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 **`e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**; PDF solo lectura blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`. No alterar archivos maestros.
- [Matriz C03-01](C03_01_ESPECIFICACION.md): **T01-R03 Application — casos de uso pequeños, validación/autorización y puertos, sin MySQL directo**; P03 árbol propuesto, P04 límites de módulos. **DIF-C03-01**: las puertas normativas `MAE-C03-01..08` son distintas de las diez microfases técnicas `C03-01..10`.
- El modelo real actual solo dispone de la API de lectura pública de marca. No existe un backend privado con autenticación/membresía/tenant verificado: **no se simula autorización** ni se activa una ruta administrativa.

## 2. Entregable acotado, dependencia y operación

Se agrega **un único caso de uso interno read-only**, sin adaptador de persistencia ni registro de DI:

1. `backend/src/Crecisol.Application/Branding/AssessPlatformBrandPublicationProposal.cs`: puerto `IPublishedPlatformBrandRevisionReader` con operación `ReadCurrentPublishedRevisionAsync(CancellationToken)`; uso de `BrandPublicationProposal.Validate()`, rechazo anticipado de ámbitos tenant, comprobación de revisión actual estricta y cancelación antes/después del puerto. Retorno `PlatformBrandPublicationPreflight` solo con dos revisiones numéricas. **No publica, no autoriza, no escribe, no devuelve permisos, no hace CAS transaccional.**
2. `backend/tests/Crecisol.Application.ContractsTests/`: ejecutable .NET 10 sin paquetes externos que prueba éxito, ausencia de publicación, ámbito inválido, versión obsoleta, revisión negativa, entradas contaminadas, puerto no disponible, cancelación previa/post lectura y ausencia de campos sensibles.
3. `qa/c03_03_application_audits.py` y `.github/workflows/c03-03-application.yml`: auditorías reales A/B/C, seis/siete mutaciones adversariales de dependencia y API, compilación Release, regresiones C01 y C03.
4. **Sin cambios** en `frontend/`, `database/`, `Crecisol.Domain`, `Crecisol.Infrastructure`, `Crecisol.Api`, rutas, tablas, migraciones, credenciales, publicación efectiva, adaptadores ni workers. Ninguna capacidad se habilita para usuarios.

## 3. Reglas de seguridad y arquitectónicas

- Application debe seguir dependiendo **solamente** de Domain (un `ProjectReference`, `net10.0`), sin `MySqlConnector`, ORM, `DbContext`, `HttpContext`, `IServiceProvider`, conexión SQL ni SDK de infraestructura. Infrastructure sustituible en fases posteriores sin invertir dependencias.
- El puerto no acepta tenant, host, actor, permisos, DSN ni parámetros del navegador. **Esto no reemplaza** `TenantContext`, pertenencia, autorización por acción/sucursal/recurso, BOLA/IDOR/MFA ni el router protected shared/dedicated. No se permite inyectar este caso de uso en API mientras no exista backend privado autorizado.
- Se debe **validar el borrador de Domain antes de leer el puerto**; un borrador tenant, malformado o inválido se rechaza antes de consultar datos. No reintentar ni hacer fallback ante errores/cancelación.
- `null` desde el puerto expresa inexistencia de revisión publicada (base 0); una revisión negativa o distinta de `ExpectedCurrentPublishedRevision` falla. El preflight solo evalúa coherencia, **no reserva la revisión ni garantiza concurrencia**.

## 4. Aceptación y auditorías obligatorias

| Auditoría | Prueba real obligatoria | Negativas |
| --- | --- | --- |
| A — función/trazabilidad | SHA del Word, siete unidades, nueve filas, fila original T01-R03 Application; puerta C03-02 8/8 | No iniciar C03-04 ni certificar el capítulo |
| B — seguridad/límites | .csproj Application solo Domain, fuentes sin HTTP/ORM/SQL/locator, grafo físico acíclico y API sin registro del nuevo caso de uso | Rechazar dependencias de MySQL/Infrastructure y exposición privada |
| C — arquitectura/regresión | Compilación .NET10 Release API + tests nuevos; ejecución real C03-03, C01 Application, C03-02 Domain y C01 Domain | Entradas tenant, revisión inválida, cancelación, filtración de metadatos, fallos de puerto |

El [PR técnico #53](https://github.com/solqaryn-platform/CRECISOL/pull/53), HEAD `4944348018bb3a1434e09eb7fdc802e269290b1b`, fue fusionado exclusivamente a `dev` SHA `c3bb9e4e314297c502b644ba1306a16364e7af2e` con **3/3 CI POSTMERGE SUCCESS**, A/B/C, 7 mutaciones negativas y **37/37 C03-03 + 19/19 C01 Application + 35/35 C03 Domain + 51/51 C01 Domain**. Los workflows históricos `skipped` en PREMERGE por filtro de rama no se contaron como PASS. La [autorización posterior y el acta](C03_03_ACTA_MICROFASE.md) permiten formalizar el cierre, sujeto a CI PREMERGE/POSTMERGE propia del acta. Las regresiones públicas Angular22/MySQL8.4 no acreditan auth privada ni funcionamiento administrativo.

## 5. Impacto, riesgos y Rollback

- **Riesgo P0** si este preflight se interpreta como autorización o publicación: se prohíbe expresamente por código, tests, falta de adaptador/DI/API y documentación. La evaluación es solo un **contrato interno de lectura** y no protege writes reales.
- **Riesgo de acoplamiento o regresión C01:** nuevas pruebas exigen que Application siga sin infraestructura y mantienen C01/C03-02; sin alterar la lectura pública.
- **Sin datos ni migraciones**; revert del PR técnico mediante un PR a `dev` si fuese necesario. Sin cambios a `main`, SOLQARYN ni producción.
- **Siguiente estado permitido:** existe aceptación específica del propietario posterior al PR #53; tras la CI de acta PREMERGE/POSTMERGE sobre SHA exacto se podrá registrar C03-03 CERTIFICADA (3/10). No se certifica por CI técnica aislada. **C03 NO CERTIFICADO; C04 BLOQUEADO; C03-04 NO INICIADA.**
