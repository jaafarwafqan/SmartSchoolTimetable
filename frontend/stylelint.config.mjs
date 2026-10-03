// Design-system enforcement for CSS (DESIGN_SYSTEM.md section 12.1).
// tokens.css is the only file allowed to define hex colours and font families.
export default {
  plugins: ["stylelint-use-logical"],
  rules: {
    // No hex colours outside tokens.css.
    "color-no-hex": true,
    // Logical properties only: margin/padding/inset/border/float/text-align left/right are rejected.
    "csstools/use-logical": ["always", { severity: "error" }],
    "property-disallowed-list": [
      ["font-family", "/^font$/", "left", "right", "float", "clear"],
      { message: "Use tokens.css for fonts and logical properties (inset-inline-*, float: inline-start) for layout." },
    ],
    "declaration-property-value-disallowed-list": {
      "text-align": ["left", "right"],
      "float": ["left", "right"],
    },
    // No px font sizes.
    "declaration-property-unit-disallowed-list": {
      "font-size": ["px"],
    },
  },
  overrides: [
    {
      files: ["src/styles/tokens.css"],
      rules: {
        "color-no-hex": null,
        "property-disallowed-list": null,
      },
    },
  ],
};
