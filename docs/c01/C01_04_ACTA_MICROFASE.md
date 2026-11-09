# C01-04 · Acta técnica de implementación, pruebas y triple auditoría

**Fecha de ejecución:** 8 de noviembre de 2026 (Honduras), 9 de noviembre UTC.  
**Estado:** PRUEBAS Y AUDITORÍAS TÉCNICAS APROBADAS; **NO CERRAR** hasta CI del HEAD final, revisión del diff y merge confirmado a `dev`.  
**Alcance:** microfase C01-04 exclusivamente (caso de uso + API pública de marca PLATFORM publicada). **Capítulo C01 NO CERTIFICADO, 0/37.**  
**Fuente:** Documento Maestro Único, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, matriz C01-01, contratos net10 C01-02 y modelo MySQL C01-03. C01-01/02/03 cerradas antes de empezar.

## Ejecuciones reales observadas (GitHub)

[CI de la microfase #42556858527](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42556858527), SHA `c1effe9b82dcd2218a91139f9569c6dd1aeee0c2`: **SUCCESS** en tres trabajos separados.

| Control | Evidencia real | Resultado |
| --- | --- | --- |
| Build ASP.NET Core 10 | Compilación API, Application, Infrastructure y Domain en Release | **PASS: 0 errores, 0 advertencias** |
| Regresión Domain C01-02 | Ejecución standalone sobre .NET 10 | **PASS: 51/51 aserciones** |
| Casos de uso Application | Publicación plataforma válida, borrador, tenant publicado, XSS, tokens, DTO sin identificador interno | **PASS: 19/19 aserciones** |
| MySQL 8.4 real | MySQL **8.4.13** en Docker sin red, socket Unix, DDL C01-03, datos sintéticos | **PASS** |
| Permisos DB | Usuario efímero con `GRANT SELECT` solo en 2 tablas y actualización denegada | **PASS** |
| Pruebas HTTP reales | 17 + 1 + 1 + 17 + 1 peticiones: acceso platform Published, Host/Forwarded, query tenant, CORS, cookies, verbos, rutas privadas, falta/alteración de publicación y ausencia de DSN | **PASS: 37/37** |
| Auditoría A | Maestro Word + referencias, puertos/DTO, endpoint y matriz de tests | **PASS** |
| Auditoría B | Denegación fail-closed, selección fija PLATFORM/PUBLISHED, datos no filtrados, no secretos, no endpoints privados | **PASS** |
| Auditoría C | Clean Architecture, proveedor MySqlConnector fijado a versión 2.6.2, UTC, índices existentes, rollback | **PASS** |
| Fundación del mismo HEAD | [CI #42556858546](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42556858546) | **PASS** |

Primera ejecución [#42556728159](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42556728159): las pruebas funcionales pasaron, pero el job cerró con **FAIL por permisos al borrar un directorio temporal MySQL**. Se corrigió restaurando propiedad de archivos del socket **solo temporal** antes de eliminar el contenedor; CI posterior completa **SUCCESS**. No se oculta el antecedente fallido.

## Resultado de diseño

- Único `GET /api/v1/public/branding/platform`, con respuestas 200/400/404/405/503, sin otros endpoints.
- Uso de la publicación MySQL de `scope_kind=1`, `Guid.Empty` y estado publicado, sin parámetros tenant ni consulta por host aportado por navegador.
- Application revalida BrandSnapshot; DTO elimina ScopeId, Scope, estados internos, preview y datos privados.
- Infraestructura MySqlConnector net10 por adaptador; DSN solo de servidor, host allowlist explícito. Sin credenciales reales, CORS, cookies, endpoints de edición ni fallback de demo.
- Datos persistentes: **ninguno**. MySQL temporal se elimina al finalizar; no Hostinger/VPS ni configuración DNS.
- Frontend y Domain original inalterados; MySQL DDL C01-03 inalterado.

## Límites y riesgos que NO cubre C01-04

**No se certifica** autenticación, identidad de usuarios, membresías, RBAC/ABAC, aislamiento horizontal de endpoints privados BOLA/IDOR, proxies/domino real, resolución shared/dedicated, edición de marca, integridad inmutable de revisiones, auditoría privada, publicación compare-and-swap, disponibilidad productiva, UI Angular, acceso móvil, legal/SEO público ni seguridad del SaaS completo. Son obligaciones expresas de otras microfases/capítulos y **NO GO** para exponer datos privados en C01-04.

Aunque la consulta plataforma lee MySQL 8.4 real, la integración se limita a prueba efímera localhost con socket sin red; esto no valida Hostinger, seguridad de credenciales productivas ni API pública desplegada.

## Revisión final, autorización y reversión

El propietario ha autorizado expresamente continuar con la siguiente microfase y tiene delegada la aprobación de microfases en `docs/C01_PREPARACION.md` únicamente tras pruebas y triple auditoría. **Cierre permitido** cuando el PR #18 final apruebe CI sobre último SHA, se revise el diff (sin frontend, secrets o producción) y se confirme el merge/post-merge. Debe registrarse el SHA final de integración en un PR documental separado.

**Rollback:** revertir PR #18 mediante PR trazable; el test elimina el contenedor y el socket efímeros. Sin bases reales, datos productivos, servicios, dominios ni infraestructura para revertir.

**Siguiente en secuencia tras cierre:** C01-05 (Angular 22 de identidad, diseño responsive y accesibilidad). El capítulo C01 completo sigue **0/37 certificados** hasta C01-08.
