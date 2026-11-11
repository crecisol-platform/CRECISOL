# CRECISOL — C03-10 | Hallazgo de regresión heredada C01-07

**Fecha:** 11-11-2026. **Estado:** corrección candidata, pendiente de CI de la propia rama. Este registro no certifica C03, no elimina ni reinterpreta fallos y no abre C04.

## Evidencia observada y diagnóstico

- [GitHub Actions C01-07 POSTMERGE #43090408790](https://github.com/solqaryn-platform/CRECISOL/actions/runs/43090408790), ejecutado en `dev` SHA `19be25d31ff04e3b9676ccc486cbdbd2e2b92563`: `failure` en el paso `Re-run C01-05 and C01-06 Chromium regression on unchanged public UI`.
- Prueba afectada: `frontend/tests/c01-06.official-integration.spec.ts`, caso `compact-320`. **19/20 tests PASS**, uno FAIL: `naturalWidth` esperaba 558 pero recibió 0.
- El elemento `img` ya cumplía `toBeVisible()` y tenía `src` aprobado. Esto no garantiza la carga completa de los píxeles; `naturalWidth === 0` puede reflejar una carga aún pendiente. Los builds y comprobaciones previas de SHA del logo pasaron. No se concluye que el producto esté libre de otras causas sin repetir la prueba.

## Remediación mínima

1. Reemplazar las dos lecturas inmediatas de `naturalWidth`/`naturalHeight` del emblema por `expect(locator).toHaveJSProperty(...)` con reintento automático. Añadir espera equivalente para el ancho del logo completo antes de consultar sus dimensiones, manteniendo los tamaños exactos (emblema 558x537, imagen completa 1298x859) y las demás verificaciones de accesibilidad y seguridad.
2. Incluir el archivo de prueba C01-06 en los filtros de `.github/workflows/c01-07-regression.yml` y habilitar el job de C01-07 para la rama `feature/c03-10-*`, sin modificar `contents: read` ni habilitar despliegues.
3. No modificar lógica del frontend ni activos, Documento Maestro Word/PDF, endpoints, bases de datos, secretos, `main` o SOLQARYN.

## Comprobaciones obligatorias antes del dictamen

CI PREMERGE del PR de remediación: C01-07 completa y C03-10 integral, con tests reales. Tras fusionar a `dev`, cotejar resultados POSTMERGE del SHA exacto. El fallo original permanece registrado; una corrida nueva no lo convierte retroactivamente en SUCCESS.

**Límite:** C03-10 permanece con acta técnica registrada, pero la certificación final de la microfase queda condicionada a esta regresión. C03 capítulo = NO CERTIFICADO; C04 = BLOQUEADO.
