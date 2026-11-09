# C05 · Identidad, autenticación y recuperación segura — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Esta guía deriva del índice `docs/PLAN_37_CAPITULOS.md` y de las políticas transversales de CRECISOL. Antes de activar C05, contrastar cada punto con el texto íntegro del capítulo en el Word canónico, su SHA registrado en `docs/MASTER_REFERENCE.md` y sus anexos; ajustar el número o alcance por PR trazable. Este archivo **no modifica el Documento Maestro Único**.

**Puerta de entrada:** C04 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C05 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** P0: identidad real y recuperación de un uso; login contextual sin selector.

## Plan secuencial de 11 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C05-01 | Modelo de cuentas y credenciales | Identidades separadas de pertenencias tenant | No cuentas compartidas entre negocios |
| C05-02 | Registro e invitaciones seguras | Flujo tipado, expiración y pruebas de abuso | No enumeración de correos |
| C05-03 | Hash y política de credenciales | Hash resistente, secretos fuera de repositorio | No almacenamiento de claves en claro |
| C05-04 | Login contextual | Vinculación de pertenencia sin selector de empresa | No confundir sesión de tenant A y B |
| C05-05 | Sesiones y revocación | Rotación y logout verificables en API | Token revocado no vuelve a funcionar |
| C05-06 | Recuperación de contraseña | Tokens de un solo uso con expiración y revocación | No reutilizar enlaces |
| C05-07 | Verificación de correo y cambios sensibles | Reautenticación y confirmaciones auditable | No cambio de correo sin comprobación |
| C05-08 | MFA y passkeys por riesgo | Inscripción/recuperación y step-up testeados | No bypass por restablecer factor |
| C05-09 | Perfiles y fotos seguras | Datos personales y medios aislados | No lectura cruzada de foto/PII |
| C05-10 | Rate limit y trazas de acceso | Pruebas contra brute-force y filtrado de logs | No registrar tokens o contraseñas |
| C05-11 | Auditorías integrales y acta C05 | A/B/C de autentificación, abuso y aceptación | No habilitar C06 sin identidades confiables |

## Contrato obligatorio para C05-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c05/C05_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c05-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 11 microfases **no** certifica C05. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C06. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.
