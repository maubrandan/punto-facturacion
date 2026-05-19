#!/usr/bin/env node
/**
 * sync-dto-to-typescript — afterFileEdit hook
 * Genera o actualiza interfaces TypeScript a partir de clases *Dto.cs o *Entity.cs
 * en POS.Domain / POS.Application, y entidades públicas en POS.Domain/Entities/*.cs.
 *
 * Salida: pos-frontend/src/app/core/models/ (ajustar MODELS_REL_PATH si el cliente usa otra ruta)
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** Relativo a la raíz del repo (donde está .cursor/) */
const MODELS_REL_PATH = "pos-frontend/src/app/core/models";

const REPO_ROOT = path.resolve(__dirname, "..", "..");

function isDomainEntitiesFolderFile(filePath) {
  const norm = filePath.replace(/\\/g, "/");
  if (!/\/POS\.Domain\/Entities\/[^/]+\.cs$/i.test(norm)) return false;
  const base = norm.split("/").pop() || "";
  return base !== "Class1.cs";
}

function shouldProcessFile(filePath) {
  if (!filePath || typeof filePath !== "string") return false;
  const norm = filePath.replace(/\\/g, "/");
  if (!norm.endsWith(".cs")) return false;
  if (!/(?:POS\.Domain|POS\.Application)\//i.test(norm)) return false;
  const base = norm.split("/").pop() || "";
  if (base.endsWith("Dto.cs") || base.endsWith("Entity.cs")) return true;
  return isDomainEntitiesFolderFile(filePath);
}

function toCamelCase(name) {
  if (!name) return name;
  // SKU -> sku (identificadores todo mayúsculas)
  if (/^[A-Z][A-Z0-9]*$/.test(name)) return name.toLowerCase();
  return name.charAt(0).toLowerCase() + name.slice(1);
}

function stripNullableWrapper(t) {
  const s = t.trim();
  const m = /^Nullable<(.+)>$/i.exec(s);
  return m ? m[1].trim() : s;
}

function mapCSharpTypeToTs(typeStr, nullableOuter) {
  let t = stripNullableWrapper(typeStr);
  let nullable = nullableOuter;

  if (t.endsWith("?")) {
    nullable = true;
    t = t.slice(0, -1).trim();
  }
  t = stripNullableWrapper(t);

  const collectionRe =
    /^(?:IList|List|ICollection|IEnumerable|IReadOnlyList|IReadOnlyCollection|HashSet|ObservableCollection)<(.+)>$/i;
  const cm = collectionRe.exec(t);
  if (cm) {
    const inner = mapCSharpTypeToTs(cm[1], false);
    const base = `Array<${inner}>`;
    return nullable ? `${base} | null` : base;
  }

  if (t.endsWith("[]")) {
    const inner = mapCSharpTypeToTs(t.slice(0, -2), false);
    const base = `Array<${inner}>`;
    return nullable ? `${base} | null` : base;
  }

  const dictRe = /^Dictionary<([^,]+),\s*(.+)>$/i.exec(t);
  if (dictRe) {
    const valTs = mapCSharpTypeToTs(dictRe[2].trim(), false);
    const base = `Record<string, ${valTs}>`;
    return nullable ? `${base} | null` : base;
  }

  const simple = t.split(/[.<]/)[0].trim();
  const lower = simple.toLowerCase();

  const numberTypes = new Set([
    "int",
    "long",
    "short",
    "byte",
    "uint",
    "ulong",
    "float",
    "double",
    "decimal",
    "nint",
    "nuint",
  ]);
  const stringTypes = new Set([
    "string",
    "guid",
    "datetime",
    "datetimeoffset",
    "timespan",
    "dateonly",
    "timeonly",
  ]);

  let mapped;
  if (numberTypes.has(lower)) mapped = "number";
  else if (lower === "bool" || lower === "boolean") mapped = "boolean";
  else if (stringTypes.has(lower)) mapped = "string";
  else mapped = t;

  return nullable ? `${mapped} | null` : mapped;
}

/**
 * Primera clase/record pública con nombre que termina en Dto o Entity,
 * o cualquier clase pública en POS.Domain/Entities/*.cs (entidades de dominio).
 */
function extractTargetClassName(source, filePath) {
  const classRe =
    /\b(?:public|internal)\s+(?:abstract\s+|sealed\s+|partial\s+)*class\s+(\w+(?:Dto|Entity))\b/;
  const recordRe =
    /\b(?:public|internal)\s+(?:abstract\s+|sealed\s+|partial\s+)*record\s+(\w+(?:Dto|Entity))\b/;
  let m = classRe.exec(source);
  if (m) return m[1];
  m = recordRe.exec(source);
  if (m) return m[1];
  if (filePath && isDomainEntitiesFolderFile(filePath)) {
    const domainEntityRe =
      /\bpublic\s+(?:abstract\s+|sealed\s+|partial\s+)*class\s+(\w+)\b/;
    m = domainEntityRe.exec(source);
    if (m) return m[1];
  }
  return null;
}

const PROP_LINE =
  /^\s*public\s+(?!static\b)(?:required\s+)?(?:virtual\s+)?(?:override\s+)?(?:new\s+)?([\w.<>,?\[\]\s]+?)\s+(\w+)\s*\{\s*get\b/gm;

function extractProperties(source) {
  const props = [];
  let m;
  while ((m = PROP_LINE.exec(source)) !== null) {
    const typeRaw = m[1].replace(/\s+/g, " ").trim();
    const name = m[2];
    if (/^(class|record|if|else|return)\b/i.test(typeRaw)) continue;
    props.push({ typeRaw, name });
  }
  return props;
}

/** Propiedades declaradas en bases conocidas (no aparecen en el .cs de la entidad). */
function extractInheritedPropsForDomainEntity(source, filePath) {
  if (!filePath || !isDomainEntitiesFolderFile(filePath)) return [];
  const m = /\bclass\s+\w+\s*:\s*([^\n{]+)/.exec(source);
  if (!m) return [];
  const bases = m[1];
  if (!/\bTenantOwnedEntity\b/.test(bases)) return [];
  return [{ typeRaw: "string", name: "TenantId" }];
}

function mergePropsWithoutDuplicateName(inherited, own) {
  const seen = new Set(inherited.map((p) => p.name));
  const out = [...inherited];
  for (const p of own) {
    if (seen.has(p.name)) continue;
    seen.add(p.name);
    out.push(p);
  }
  return out;
}

function classNameToKebabFileBase(className) {
  const s = className.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
  return s;
}

function buildInterfaceSource(className, props, sourceRelPath) {
  const lines = [
    `// Auto-generated by sync-dto-to-typescript from ${sourceRelPath}`,
    `// Regenerate on change; manual edits may be overwritten.`,
    "",
    `export interface ${className} {`,
  ];
  for (const p of props) {
    const tsType = mapCSharpTypeToTs(p.typeRaw, false);
    const key = toCamelCase(p.name);
    const tail =
      className === "Product" && p.name === "FinalPrice"
        ? " // La interfaz lo recibe como número ya calculado"
        : "";
    lines.push(`  ${key}: ${tsType};${tail}`);
  }
  lines.push("}");
  lines.push("");
  return lines.join("\n");
}

function main() {
  let data;
  try {
    const raw = fs.readFileSync(0, "utf8");
    if (!raw.trim()) process.exit(0);
    data = JSON.parse(raw);
  } catch {
    process.exit(0);
  }

  const filePath = data.file_path;
  if (!shouldProcessFile(filePath)) process.exit(0);

  let source;
  try {
    source = fs.readFileSync(filePath, "utf8");
  } catch {
    process.exit(0);
  }

  const className = extractTargetClassName(source, filePath);
  if (!className) process.exit(0);

  const inherited = extractInheritedPropsForDomainEntity(source, filePath);
  const props = mergePropsWithoutDuplicateName(inherited, extractProperties(source));
  const relFromRepo = path.relative(REPO_ROOT, filePath).replace(/\\/g, "/");

  const outDir = path.join(REPO_ROOT, MODELS_REL_PATH);
  fs.mkdirSync(outDir, { recursive: true });

  const fileBase = classNameToKebabFileBase(className);
  const outPath = path.join(outDir, `${fileBase}.model.ts`);
  const content = buildInterfaceSource(className, props, relFromRepo);

  try {
    fs.writeFileSync(outPath, content, "utf8");
  } catch {
    process.exit(1);
  }

  process.exit(0);
}

main();
