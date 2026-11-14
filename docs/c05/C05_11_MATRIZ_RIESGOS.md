# CRECISOL C05-11 — matriz transversal de riesgos P0/P1

**Fecha:** 2026-11-14. **Repositorio:** `crecisol-platform/CRECISOL`; **base dev:** `52efec64f43245771af59459f8db3b38685c1703`. **ESTADO INICIAL: PENDIENTE DE PRUEBAS INTEGRALES, NO CERTIFICADA, C06 BLOQUEADO.** No transformar un hallazgo hipotético en PASS sin salida de CI del mismo SHA. Los riesgos productivos no desaparecen por una certificación acotada a datos sintéticos.

| ID | Tipo y gravedad | Fallo que bloquea el cierre sintético | Ensayo real exigido | Estado inicial |
| --- | --- | --- | --- | --- |
| R05-01 | P0 realm/membresía | Mismo correo crea grants tenant A/B o confunde cuenta PLATFORM | `.NET10` AccountIdentity y C04 TenantIdentity; A/B negativas | PENDIENTE |
| R05-02 | P0 invitación/registro | Token ajeno/replay o registro enumeran correo, conceden membresía | C05-02 signed/neutral/bound claim MySQL8.4, QA A/B | PENDIENTE |
| R05-03 | P0 privacidad registro | Datos de buzón/contacto en logs o expiración resurrecta invitación | C05-02 mailbox/retention/outbox sintéticas, QA | PENDIENTE |
| R05-04 | P0 secreto | Password PHC/pepper equivocado, contraseña o key en repositorio/log | C05-03 Argon2id, dummy, negativas .NET10 y grep auditado | PENDIENTE |
| R05-05 | P0 login contextual | Host o claims forzados seleccionan otra identidad o emiten permiso | C05-04 .NET10, host C04-04, casos tenant ambiguo | PENDIENTE |
| R05-06 | P0 sesión | Refresh rotado/revocado revive por carrera/replay de token | C05-05 HTTP/MySQL8.4, revocación, 16-way CAS | PENDIENTE |
| R05-07 | P0 recuperación | Enlace de un uso se reusa, expira mal o concede sesión | C05-06 HTTP/MySQL8.4, reloj DB/tenant/replay | PENDIENTE |
| R05-08 | P0 correo | Se modifica alias sin doble prueba/reautenticación | C05-07 HTTP/MySQL8.4, cambio concurrente | PENDIENTE |
| R05-09 | P0 MFA | El restablecimiento omite step-up, replay OTP o WebAuthn ajeno | C05-08 crypto ES256/TOTP y HTTP/MySQL8.4 | PENDIENTE |
| R05-10 | P0 PII/foto | IDOR de perfil, ciphertext/media de tenant A accesible desde B | C05-09 HTTP/MySQL8.4, AES-GCM y CAS de 16 | PENDIENTE |
| R05-11 | P0 fuerza bruta | Cambio email/IP, spoof XFF o 16 solicitudes rompen CAS | C05-10 HTTP/MySQL8.4, 6x16 5/11/0 en SHA actual | PENDIENTE |
| R05-12 | P0 bitácora | IP/email/token/contraseña/payload en acceso; DELETE/UPDATE de traza | C05-10 HMAC y append-only, SQL least-privilege | PENDIENTE |
| R05-13 | P0 integración | API productiva activa endpoint privado sin gates reales | Auditoría explícita `Crecisol.Api/Program.cs` branding-only | PENDIENTE |
| R05-14 | P1 framework/compat | .NET10, Angular22, MySQL8.4 o Chromium rompen retrocompatibilidad | Compilar tests + C03-10 real suite y browsers | PENDIENTE |
| R05-15 | P1 suministro/CI | Workflow usa token de escritura, secretos productivos o contenedores sin pin | Revisión de workflows, permisos, digest MySQL, cleanup | PENDIENTE |
| R05-16 | P0 origen canónico | Word/PDF/plan editados o acta usa SHA/run histórico falso | SHA256 Word/PDF, DOCX XML, diff exacto A/B/C mismo SHA | PENDIENTE |
| R05-17 | P1 auditoría cruzada | Microfase C05 anterior tiene prueba esencial no repetida o fallida | QA A/B 01..10 + contratos + scripts MySQL + C03/C04 | PENDIENTE |
| R05-18 | P0 progreso indebido | C06 se habilita con identidades reales/no comprobadas | Verificar NO-GO productivo, C06 BLOQUEADO hasta cierre y alcance | PENDIENTE |
| R05-19 | P0 comercial fuera de alcance | COM-T08/09/10 o SuperAdmin se presentan como autorizados/ejecutados | Contraste Word/plan, conservar NO EJECUTADAS | PENDIENTE |
| R05-20 | P1 alta carga/secret key | Caso 6x16 se extrapola a DDoS global, WAF, KMS o Redis real | Registrar límite sintético y ausencia de productización | PENDIENTE |

## Disposiciones

**A:** R05-01, R05-13..20. **B:** R05-01..13, R05-15..20. **C:** R05-14..20 más arquitectura/regresiones. Ejecutar pruebas reales en CI con SHA/run, no resolver celdas con texto solamente. El dictamen podrá detallar qué controles fueron realmente ejecutados; esta matriz es una lista de obligaciones, **no** una declaración anticipada de PASS.

**NO-GO de identidad operativa:** los módulos C05-01..10 existen como contratos, adaptadores de ensayo y bases de datos efímeras; **no hay validación de usuario/tenancy/JWT/MFA/recuperación/observabilidad de una API privada productiva unificada**. Esas dependencias son bloqueantes para cualquier uso de clientes, no constituyen funcionalidad ya implementada. Todo hallazgo P0/P1 confirmado **dentro del núcleo sintético** bloquea el cierre de C05-11.

**Trazabilidad del fallo histórico C05-10:** [#43643788104](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788104) registró 5/4/7; [remediación #146](https://github.com/crecisol-platform/CRECISOL/pull/146#issuecomment-6884929367) confirmó 6x16 5/11/0 y [ratificación #147](https://github.com/crecisol-platform/CRECISOL/pull/147#issuecomment-6885019825) formalizó el núcleo sintético. Ni R05-11 ni R05-20 se resuelven en C05-11 por citar una ejecución anterior: **se deben repetir**.

**Seguridad de scope:** no cambiar `main`, `solqaryn/Solqaryn`, Documento Maestro, archivos reales, producción, VPS, Cloudflare, email externo ni cobros. **Rollback:** revertir exclusivamente cambios de documentación/QA/workflows de C05-11 en `dev` tras revisión; nunca revertir otras microfases por inercia.
