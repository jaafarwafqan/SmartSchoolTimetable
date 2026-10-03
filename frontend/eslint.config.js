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
            if (context.filename.replaceAll("\\", "/").endsWith("/components/ui/button.tsx")) return;
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

export default tseslint.config(
  ...tseslint.configs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: {
      "localized-ui": localizedUiPlugin,
      "react-hooks": reactHooks,
    },
    rules: {
      "localized-ui/no-hardcoded-ui-text": "error",
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
  },
);
