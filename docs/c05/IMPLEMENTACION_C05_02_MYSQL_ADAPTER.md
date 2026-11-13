# CRECISOL C05-02 — Persistencia transaccional MySQL 8.4 (integración aislada)

**Estado:** EJECUCIÓN TÉCNICA EN CURSO. **NO CERTIFICADA FUNCIONALMENTE.** Microfase única C05-02. Depende de [PR #116](https://github.com/crecisol-platform/CRECISOL/pull/116#issuecomment-6845872668), auditado solo como núcleo interno, SHA `028c6be94510ccdb11fa4c883aefa5bfc35cf142`. Repositorio autorizado `crecisol-platform/CRECISOL`, ID `1450414592`, base `dev`; nunca SOLQARYN.

**Autoridad canónica:** Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; texto C05 SHA-256 `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. Matriz B01–B18 y T01–T20 de `docs/c05/C05_02_ESPECIFICACION.md`.

## Objeto y límites

Se agrega `MySqlDisposableInvitationLedger` en Infrastructure, que implementa `ITrustedInvitationLedger` desde Application sin introducir SQL en Domain/Application. Consume **MySqlConnector 2.6.2 ya instalado**, parámetros tipados y UUID en orden RFC4122 verificado. Solo permite `c05_02_disposable` en `localhost/127.0.0.1/::1` a partir de DSN **exclusivamente del servidor**; prohíbe `LOCAL INFILE`, no usa pooling y no se registra en DI ni en la API. Esta restricción intencional impide conectarlo por error a una base real. **No es migración, esquema ni adaptador productivo**, aunque ejecute SQL e hilos concurrentes reales dentro de CI.

**Emisión:** lee únicamente el contexto firmado/verificado del caso de uso, no parámetros de tenant del cliente. El adaptador verifica nuevamente tenant ACTIVO, permisos del invitante, perfil limitado, destinatario verificado, términos vigentes y tiempo UTC. Dentro de una transacción con aislamiento READ COMMITTED bloquea la fila de destinatario mediante `FOR UPDATE`; busca pendientes no expirados y deniega duplicados de distinto ID. Inserta solamente hash criptográfico de 32 bytes; la capacidad sin hash jamás se escribe en DB. Una colisión de UNIQUE o FK devuelve denegación, demás fallos son explícitos, no fallback.

**Lectura:** consulta por PK compuesta `tenant_id + invitation_id`, valida que los datos almacenados coincidan, comprueba los tipos y reconstruye el digest protegido directamente (sin confundir hash con token). No enumera direcciones ni libera contraseñas.

**Consumo:** llama al procedimiento `c05_02_claim_once` del fixture con parámetros, no SQL concatenado, y acepta solo `CLAIMED/DENIED`. El procedimiento InnoDB condiciona el `UPDATE` a estado Pending, revisión, token digest, identidad del destinatario, tenant activo, términos y consentimiento vigentes, vigencia del canal y permiso del emisor; 16 conexiones simultáneas deben producir **un solo ganador**. No escribe membresías, roles, contraseñas ni sesiones. El momento de linearización es el UPDATE SQL, no un bloqueo en memoria.

## Evidencia exigida (A/B/C)

- **A normativa y compilación:** SHA Word/PDF/C05 verificados, precedente C05-01 y C05-02, compilación real .NET10 Release warnaserror, matriz de interfaz/adapter y prueba SQL real `--integration`.
- **B ofensiva:** restaurar digest protegido, rechazar 31 bytes o hash nulo, inyección de tenant/DSN remota, 16 clientes MySqlConnector con un ganador, replays, tenant B, revisión y digest ajenos, invitante revocado, destinatario no verificado, suspensión, términos obsoletos, perfil ilegal, duplicado, caducidad. Pruebas negativas no omitidas.
- **C arquitectura/operación:** fuente y diff acotados a C05-02, mismo Git SHA/run, padre PR HEAD/dev base, control de secretos, no rutas privadas nuevas, no despliegue, no configuración de producción; regressiones C03/C04/C05 previas en workflows aplicables y run final `SUCCESS`.
- **Evidencia explícita:** la prueba del script MySQL desechable corre `dotnet run --no-build -- --integration` con credenciales **sintéticas aleatorias de un solo uso** y puerto 127.0.0.1 efímero. Sin `--integration` el nuevo proyecto declara `SKIP`; esto **NO cuenta como ejecución SQL**. La CI de este PR exige la bandera y falla si faltan credenciales o existe fallo.
- **No se han ejecutado (NO EJECUTADAS):** pruebas de registro HTTP funcional real y de no-enumeración temporal, verificación efectiva de correo, entrega de invitación a destinatario real, rutas de sesión/identidad, base multitenant de identidad real compartida/dedicada, alta real de usuario y límites de abuso a través de API. No se pueden marcar PASS porque tales integraciones no existen.

## Bloqueos P0/P1 para CERTIFICAR la microfase completa

1. C05-01 aporta solo modelos internos; no hay autenticación productiva capaz de confirmar actor/invitante y permisos de acción, sucursal y propiedad para una solicitud HTTP real. Esto NO se resuelve confiando en claims del navegador.
2. El canal de correo requiere un comprobante de posesión independiente del token, envío de mensajes con privacidad y un flujo anti-enumeración medible; no está implementado.
3. Identidad real, consentimientos, políticas y auditoría productiva requieren persistencia/procedimientos aprobados para shared y dedicated, reintentos transaccionales y pruebas negativas de **extremo a extremo**, no tablas de prueba `c05_02_*`.
4. Las futuras credenciales, sesiones y MFA pertenecen a C05-03..08 y no deben activarse aquí, ni simular autorización mediante recibos.

**Resultado admisible de este PR:** certificar únicamente **la integración de almacenamiento desechable y sus contratos**, si la CI A/B/C PRE/POSTMERGE pasa sin fallos. **C05-02 sigue NO CERTIFICADA FUNCIONALMENTE y C05-03 permanece BLOQUEADA** hasta superar las puertas que faltan. No tocar `main`, Word/PDF, SOLQARYN ni activar VPS, Cloudflare, email externo, producción o pagos.

**Rollback:** revertir el squash específico tras revisar dependencias; la base de prueba se destruye al cerrar Docker. No se crean tablas en producción, secretos en el repositorio ni cuentas reales.
