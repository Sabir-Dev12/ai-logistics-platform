import { readFileSync, writeFileSync } from "node:fs";

const root = new URL("../", import.meta.url);
const tokens = JSON.parse(readFileSync(new URL("src/design-system/tokens.json", root), "utf8"));
const variables = [];
for (const [group, entries] of Object.entries(tokens)) {
  if (group === "grid") continue;
  for (const [key, value] of Object.entries(entries)) {
    const name = key.replace(/[A-Z]/g, (letter) => "-" + letter.toLowerCase());
    variables.push(`  --ds-${group}-${name}: ${value};`);
  }
}
const css = "/* Generated from tokens.json. Run npm run tokens; do not edit manually. */\n:root {\n" + variables.join("\n") + "\n}\n";
const target = new URL("src/design-system/tokens.css", root);
if (process.argv.includes("--check")) {
  if (readFileSync(target, "utf8") !== css) {
    throw new Error("tokens.css is out of date. Run npm run tokens.");
  }
} else writeFileSync(target, css);
