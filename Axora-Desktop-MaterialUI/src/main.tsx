import ReactDOM from "react-dom/client";
import App from "./App";
import "./styles/index.css";

// Note: StrictMode removed intentionally — it causes double-render in dev
// which creates visual flicker in Framer Motion animations and Tauri windows.
ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <App />
);
