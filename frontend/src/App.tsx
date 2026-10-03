import { lazy, Suspense } from "react";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { AppGate } from "./features/auth/AppGate";

// The style guide exists only in development builds; Vite removes this branch (and its chunk) in production.
const DesignGuidePage = import.meta.env.DEV
  ? lazy(() => import("./features/design-guide/DesignGuidePage"))
  : null;

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {DesignGuidePage && (
          <Route path="/design" element={<Suspense fallback={null}><DesignGuidePage /></Suspense>} />
        )}
        <Route path="/*" element={<AppGate />} />
      </Routes>
    </BrowserRouter>
  );
}
