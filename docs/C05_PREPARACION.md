# CRECISOL — Capítulo 05 · Preparación institucional y puerta de entrada

**Fecha:** 12-11-2026 (Honduras). **Repositorio exclusivo:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`), integración `dev`.
**Estado:** C05-01 **SOLO EN PREPARACIÓN DOCUMENTAL**, **NO IMPLEMENTADA Y NO CERTIFICADA**. C05-02..C05-11 bloqueadas. Capítulo C05 NO certificado; C06 bloqueado.

## 1. Prerrequisito C04 real y alcance de habilitación

El capítulo C04 tiene 12/12 microfases ratificadas **exclusivamente en arquitectura, contratos internos y fixtures sintéticos**. La puerta institucional final es el [dictamen permanente del PR #111](https://github.com/crecisol-platform/CRECISOL/pull/111#issuecomment-6843638186), en `dev` SHA `479cce19a27a1c947df615a01284f2de60f1168f`, con 20/20 ejecuciones CI aplicables `completed/success` y auditorías A/B/C. Se habilita **solamente preparar C05-01**, no activar autenticación privada, JWT, sesiones, MFA, DSN de clientes, Redis, medios ni producción.

Este expediente NO extiende la certificación del C04 a una implementación HTTP/DB productiva. El Word/PDF, `main` y SOLQARYN permanecen intactos.

## 2. Fuente única y distinción de numeraciones

- Único Word canónico: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF de lectura SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; ambos validados por `qa/master_source_guard.py`.
- Extracción real desde GitHub Actions [run #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640): capítulo 05 `CAPÍTULO 05 DE 37`, tramo `1807:1900` del recorrido de párrafos XML, 93 líneas no vacías y 9 009 caracteres en la normalización del extractor, SHA-256 del texto `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. La fuente interna declara 16 párrafos sustantivos y 1 tabla original de 13 filas: **no confundir** esos recuentos con las líneas XML aplanadas.
- El contrato de ejecución del propio Word enumera ocho pasos `C05-01 Extraer` a `C05-08 Acta`; el [plan técnico derivado](planificacion-futura/C05_MICROFASES_PROPUESTAS.md) propone **11 microfases funcionales C05-01..C05-11**. Son sistemas de numeración diferentes, no requisitos intercambiables. Esta entrega trata **solo el modelo de cuentas y credenciales** de la primera microfase funcional.
- La extracción de fuente se utiliza para la trazabilidad, no como aprobación del producto. Ninguna edición del Word/PDF está autorizada por esta preparación.

## 3. Objetivo limitado de C05-01

Especificar el modelo de identidad personal, credenciales, relaciones de pertenencia por tenant y separación de privilegios plataforma/empresa/consumidor, con denegación por defecto. Evitar que identificadores, correos, hosts o claims sustituyan pruebas de autenticación y autorización. La persona puede mantener vínculos explícitos con varias empresas; **la misma identidad en dos negocios jamás hereda permisos del otro**.

**No se crea** entidad persistente, API privada, login, registro real, reset, sesión, grant, worker, foto, tabla ni adaptador. El contrato de C04-05 `TrustedActorSessionSnapshot` es un test doble interno: NO constituye autenticación de usuario real ni criterio para confiar en claims del cliente.

## 4. Mapa de responsabilidad y dependencias

| Capa o sistema | Decisión de preparación C05-01 | Estado operativo |
| --- | --- | --- |
| `backend/Domain` | Separar principal, verificación, credencial y membresía; estados opacos, sin permisos implícitos | Solo especificado, **no código nuevo** |
| `backend/Application` | Definir puertos futuros de identidad/verificación, pertenencia y denegación segura; nunca actor/tenant desde un header | Solo especificado |
| `backend/Infrastructure` | Persistencia/hashing de verificador, envío y gestión segura de secretos futuros | **NO IMPLEMENTADO** |
| `backend/Api` | Autenticación real, cookies seguras, mitigación CSRF, MFA, rate limiting y revocación por fases posteriores | **NO IMPLEMENTADO** |
| `database/` | Diseño futuro de sujetos globales, membresías tenant y credenciales sin claves en claro; índices/FK tenant-scoped | **Sin nuevas migraciones ni SQL** |
| `frontend/` | Login contextual sin selector, errores neutrales, pantallas accesibles y recuperación en fases autorizadas | **Sin UI privada activada** |
| C04-01..12 | Catálogo, host validado, snapshots de contexto, branch/action y pruebas sintéticas previas | No son autorización productiva |
| C05-02..11 / C06-C08 | Invitación, hash, login, sesiones, recuperación, MFA, perfil, mitigación de abuso y autorización fina | **No iniciados** |

## 5. Puertas de calidad y prohibiciones

| Gate | Evidencia exigida | Estado al iniciar |
| --- | --- | --- |
| P0 | Repositorio/ramas/acta C04, Word/PDF y capítulo 05 por SHA | Fuente verificada, precondición contrastada |
| P1 | Especificación C05-01 y matriz de amenazas con requisitos de Word/plan | EN EL PR PREPARATORIO |
| P2 | Auditoría A de cobertura, B de límites/mutaciones adversarias documentales y C de arquitectura/procedencia | PENDIENTE de CI final |
| P3 | Código real, pruebas positivas/negativas, A/B/C de implementación y regresiones .NET/MySQL/Angular | **NO EJECUTADAS; NO AUTORIZADAS por esta preparación** |
| P4 | Diff exacto, CI PREMERGE del HEAD, fusión documental a `dev` solo con pruebas green y CI POSTMERGE | PENDIENTE |
| P5 | Acta técnica de implementación C05-01 y habilitación C05-02 | **BLOQUEADA** |

**NO_GO:** credencial en claro, pertenencia otorgada por `tenant_id`/host/claim, vínculo automático entre negocios por correo, bypass de operadores globales, P0/P1 documental, prueba obligatoria fallida/omitida o Word no comprobable. Los casos negativos de autenticación real, persistencia y sesiones **NO EJECUTADOS** no se convierten en PASS con un test de texto.

El inicio de código posterior deberá tratar una sola microfase, producir pruebas de ejecución real y A/B/C completas, proteger backend/tenancy y presentar expediente por SHA. No programar C05-02 ni abrir C06.

## 6. Reversibilidad

Si una puerta falla, mantener el PR en borrador, documentar el hallazgo, corregir y repetir la prueba sobre un SHA nuevo. Reversión solo de los documentos/QA de este PR; no tocar `main`, Word/PDF, secretos, datos reales, VPS, DNS, Cloudflare, cobros ni `solqaryn/Solqaryn`.
