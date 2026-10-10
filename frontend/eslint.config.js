import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";

const localizedUiPlugin = {
  rules: {
    "no-hardcoded-ui-text": {
      meta: {
        type: "problem",
        schema: [],
        messages: {
          literal: "استخدم قاموس الترجمة للنص الظاهر للمستخدم.",
        },
      },
      create(context) {
        return {
          JSXText(node) {
            if (node.value.trim()) {
              context.report({ node, messageId: "literal" });
            }
          },
          JSXAttribute(node) {
            const name = node.name?.name;
            if (
              ["aria-label", "title", "placeholder", "alt"].includes(name) &&
              node.value?.type === "Literal" &&
              typeof node.value.value === "string"
            ) {
              context.report({ node, messageId: "literal" });
            }
          },
          JSXOpeningElement(node) {
            if (node.name.type !== "JSXIdentifier") return;
            if (node.name.name === "Button") {
              const hasIcon = node.attributes.some(
                (attribute) => attribute.type === "JSXAttribute" &&
                  attribute.name.name === "icon",
              );
              const hasLabel = node.parent.type === "JSXElement" &&
                node.parent.children.some((child) =>
                  (child.type === "JSXText" && child.value.trim().length > 0) ||
                  child.type === "JSXExpressionContainer" ||
                  child.type === "JSXElement",
                );
              if (!hasIcon || !hasLabel) {
                context.report({ node, messageId: "literal" });
              }
              return;
            }
            if (node.name.name !== "button") return;
            // components/ui primitives enforce their accessible names through TypeScript props.
            if (context.filename.replaceAll("\\", "/").includes("/components/ui/")) return;
            const hasAccessibleLabel = node.attributes.some(
              (attribute) => attribute.type === "JSXAttribute" &&
                attribute.name.name === "aria-label",
            );
            if (!hasAccessibleLabel) {
              context.report({ node, messageId: "literal" });
            }
          },
        };
      },
    },
  },
};


// Design-system enforcement for TSX (DESIGN_SYSTEM.md section 12.2).
const arbitraryColorClass = /(?:^|\s|:)(?:bg|text|border|fill|stroke|ring|outline|from|via|to|decoration|shadow|accent|caret|divide|placeholder)-\[(?:#|rgb|hsl|oklch|color:)/;
const hexColor = /#[0-9a-f]{3,8}\b/i;
const colorStyleKey = /color|background|fill|stroke|shadow|border|outline/i;
const rawFormElements = new Set(["button", "input", "select", "textarea", "table"]);
const nativeDateTimeTypes = new Set(["date", "time", "datetime-local", "month", "week"]);
const drawerName = /drawer|sheet/i;
const foreignIconPackage = /^(react-icons|@heroicons\/|@fortawesome\/|@mui\/icons-material|@tabler\/icons|@phosphor-icons\/|react-feather|@radix-ui\/react-icons|@iconify\/|lucide$)|icons?(\/|$)/;

function classNameStrings(node) {
  if (!node.value) return [];
  if (node.value.type === "Literal" && typeof node.value.value === "string") return [node.value.value];
  if (node.value.type === "JSXExpressionContainer") {
    const expression = node.value.expression;
    if (expression.type === "Literal" && typeof expression.value === "string") return [expression.value];
    if (expression.type === "TemplateLiteral") return expression.quasis.map((quasi) => quasi.value.cooked ?? "");
  }
  return [];
}

const designSystemPlugin = {
  rules: {
    "no-arbitrary-colors": {
      meta: {
        type: "problem",
        schema: [],
        messages: {
          arbitraryClass: "Use semantic colour tokens (bg-surface, text-ink ...) instead of arbitrary colour classes.",
          inlineColor: "Inline style colours are not allowed; use a CSS class with tokens.",
        },
      },
      create(context) {
        return {
          JSXAttribute(node) {
            const name = node.name?.name;
            if (name === "className" || name === "class") {
              for (const value of classNameStrings(node)) {
                if (arbitraryColorClass.test(value) || hexColor.test(value)) {
                  context.report({ node, messageId: "arbitraryClass" });
                }
              }
            }
            if (name === "style" && node.value?.type === "JSXExpressionContainer" &&
                node.value.expression.type === "ObjectExpression") {
              for (const property of node.value.expression.properties) {
                if (property.type !== "Property") continue;
                const key = property.key.type === "Identifier" ? property.key.name : String(property.key.value ?? "");
                const literal = property.value.type === "Literal" ? String(property.value.value) : "";
                if (colorStyleKey.test(key) || hexColor.test(literal)) {
                  context.report({ node: property, messageId: "inlineColor" });
                }
              }
            }
          },
        };
      },
    },
    "no-raw-form-elements": {
      meta: {
        type: "problem",
        schema: [],
        messages: { raw: "Feature code must use components/ui primitives instead of a raw <{{name}}>." },
      },
      create(context) {
        const filename = context.filename.replaceAll("\\", "/");
        if (!filename.includes("/src/features/")) return {};
        return {
          JSXOpeningElement(node) {
            if (node.name.type === "JSXIdentifier" && rawFormElements.has(node.name.name)) {
              context.report({ node, messageId: "raw", data: { name: node.name.name } });
            }
          },
        };
      },
    },
    "no-raw-headings": {
      meta: {
        type: "problem",
        schema: [],
        messages: { raw: "Feature code renders section headings with SectionTitle (icon + title), not a raw <{{name}}>." },
      },
      create(context) {
        const filename = context.filename.replaceAll("\\", "/");
        if (!filename.includes("/src/features/")) return {};
        return {
          JSXOpeningElement(node) {
            if (node.name.type === "JSXIdentifier" && (node.name.name === "h2" || node.name.name === "h3")) {
              context.report({ node, messageId: "raw", data: { name: node.name.name } });
            }
          },
        };
      },
    },
    "no-native-date-time": {
      meta: {
        type: "problem",
        schema: [],
        messages: { native: "Use DateField/TimeField (Arabic, 24-hour, school numerals) instead of a native {{kind}} input." },
      },
      create(context) {
        return {
          JSXAttribute(node) {
            if (node.name?.name !== "type") return;
            const value = node.value?.type === "Literal" ? String(node.value.value) : "";
            if (nativeDateTimeTypes.has(value)) context.report({ node, messageId: "native", data: { kind: value } });
          },
        };
      },
    },
    "no-drawers": {
      meta: {
        type: "problem",
        schema: [],
        messages: { drawer: "Side drawers and sheets are not allowed; use the add patterns in DESIGN_SYSTEM.md 14 ({{name}})." },
      },
      create(context) {
        const report = (node, name) => {
          if (drawerName.test(name)) context.report({ node, messageId: "drawer", data: { name } });
        };
        return {
          ImportDeclaration(node) {
            report(node, String(node.source.value));
            for (const specifier of node.specifiers) report(specifier, specifier.local.name);
          },
          JSXOpeningElement(node) {
            if (node.name.type === "JSXIdentifier") report(node, node.name.name);
          },
          JSXAttribute(node) {
            if (node.name?.name === "className") for (const value of classNameStrings(node)) report(node, value);
          },
        };
      },
    },
    "lucide-icons-only": {
      meta: {
        type: "problem",
        schema: [],
        messages: { foreign: "Icons come only from lucide-react ({{source}} is not allowed)." },
      },
      create(context) {
        return {
          ImportDeclaration(node) {
            const source = String(node.source.value);
            if (source !== "lucide-react" && foreignIconPackage.test(source)) {
              context.report({ node, messageId: "foreign", data: { source } });
            }
          },
        };
      },
    },
  },
};

export default tseslint.config(
  ...tseslint.configs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: {
      "localized-ui": localizedUiPlugin,
      "design-system": designSystemPlugin,
      "react-hooks": reactHooks,
    },
    rules: {
      "localized-ui/no-hardcoded-ui-text": "error",
      "design-system/no-arbitrary-colors": "error",
      "design-system/no-raw-form-elements": "error",
      "design-system/no-raw-headings": "error",
      "design-system/lucide-icons-only": "error",
      "design-system/no-native-date-time": "error",
      "design-system/no-drawers": "error",
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
  },
);
