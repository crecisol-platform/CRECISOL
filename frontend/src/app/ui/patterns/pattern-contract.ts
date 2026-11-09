/** C02-06: presentation-only contracts. Trusted backend owns every permission,
 * tenant, schema version and configured commercial copy in future chapters.
 */
export type DraftRule = 'plain-text' | 'reference';

export interface DraftFieldConfig {
  readonly id: string;
  readonly label: string;
  readonly hint: string;
  readonly rule: DraftRule;
  readonly required: boolean;
  readonly maxLength: number;
}

export interface TableColumn {
  readonly key: string;
  readonly label: string;
}

export type TableRow = Readonly<Record<string, string>>;

export function safeDisplayText(value: unknown, limit = 160): value is string {
  return typeof value === 'string' && value.trim().length > 0 &&
    value.length <= limit && !/[<>\u0000-\u001f\u007f]/u.test(value);
}

export function safeKey(value: unknown): value is string {
  return typeof value === 'string' && /^[a-z][a-z0-9-]{1,39}$/u.test(value);
}

export function validDraftConfig(config: DraftFieldConfig): boolean {
  return safeKey(config.id) && safeDisplayText(config.label, 80) &&
    safeDisplayText(config.hint, 240) && (config.rule === 'plain-text' || config.rule === 'reference') &&
    typeof config.required === 'boolean' &&
    Number.isInteger(config.maxLength) && config.maxLength >= 8 && config.maxLength <= 120;
}

export function validColumns(columns: readonly TableColumn[], caption: string): boolean {
  return safeDisplayText(caption, 100) &&
    Array.isArray(columns) && columns.length >= 1 && columns.length <= 6 &&
    new Set(columns.map(column => column.key)).size === columns.length &&
    columns.every(column => safeKey(column.key) && safeDisplayText(column.label, 60));
}

/** Pure, local UX feedback. NEVER a server authorization or a successful save. */
export function draftIssue(value: string, config: DraftFieldConfig): string | null {
  if (!validDraftConfig(config)) return 'Configuración de campo no disponible.';
  const trimmed = value.normalize('NFC').trim();
  if (!trimmed) return config.required ? 'Completa este campo para revisar el ejemplo.' : null;
  if (trimmed.length > config.maxLength) return 'El valor supera el límite permitido.';
  if (/[<>\u0000-\u001f\u007f]/u.test(trimmed)) return 'El valor contiene caracteres no admitidos.';
  if (config.rule === 'reference' && !/^[\p{L}\p{N}._ -]+$/u.test(trimmed)) {
    return 'Usa únicamente letras, números, espacios, puntos, guiones o guion bajo.';
  }
  return null;
}
