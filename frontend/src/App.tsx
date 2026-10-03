import { BrowserRouter } from "react-router-dom";
import { AppGate } from "./features/auth/AppGate";

export default function App() {
  return (
    <BrowserRouter>
      <AppGate />
    </BrowserRouter>
  );
}
