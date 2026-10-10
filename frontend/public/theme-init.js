// Applies the saved colour theme before the first paint (no flash). It must be an external file: the app's
// Content-Security-Policy (default-src 'self') does not allow inline scripts. The same rule lives in src/lib/theme.ts.
(function () {
  try {
    var saved = window.localStorage.getItem("sst-theme");
    var dark = saved === "dark" || ((saved === null || saved === "system") && window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
    document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
  } catch (error) {
    document.documentElement.setAttribute("data-theme", "light");
  }
})();
