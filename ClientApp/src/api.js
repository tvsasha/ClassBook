export function resolveApiBase() {
  const { hostname, port, origin } = window.location;
  const isLocalHost = hostname === "localhost" || hostname === "127.0.0.1";
  const devPorts = new Set(["5173", "5500", "5501", "5502", "5503", "5504", "5505"]);

  if (isLocalHost && devPorts.has(port)) {
    return "https://localhost:7062/api";
  }

  return `${origin}/api`;
}

const apiBase = resolveApiBase();

export async function apiRequest(path, options = {}) {
  const response = await fetch(`${apiBase}${path}`, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    ...options
  });

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message || `Ошибка запроса: ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

async function readErrorMessage(response) {
  const text = await response.text();
  if (!text) {
    return "";
  }

  try {
    const parsed = JSON.parse(text);
    return parsed.message || parsed.title || text;
  } catch {
    return text;
  }
}
