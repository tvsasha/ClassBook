export function resolveApiBase() {
  const { hostname, port, origin } = window.location;
  const isLocalHost = hostname === "localhost" || hostname === "127.0.0.1";
  const devPorts = new Set(["5173", "5500", "5501", "5502", "5503", "5504", "5505"]);

  if (isLocalHost && devPorts.has(port)) {
    return "https://localhost:7062/api";
  }

  return `${origin}/api`;
}

export const apiBase = resolveApiBase();

export async function apiRequest(path, options = {}) {
  let response;

  try {
    response = await fetch(`${apiBase}${path}`, {
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {})
      },
      ...options
    });
  } catch (error) {
    throw new Error(toRussianNetworkError(error));
  }

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message || getFriendlyHttpError(response.status));
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

function getFriendlyHttpError(status) {
  if (status === 404) {
    return "Действие сейчас недоступно. Обновите страницу и попробуйте снова.";
  }

  if (status === 401 || status === 403) {
    return "Для этого действия не хватает прав. Войдите заново или обратитесь к администратору.";
  }

  return "Не удалось выполнить действие. Попробуйте еще раз.";
}

function toRussianNetworkError(error) {
  const message = String(error?.message || "").toLowerCase();

  if (message.includes("failed to fetch") || error instanceof TypeError) {
    return "Сейчас не получается открыть данные. Попробуйте обновить страницу через пару минут.";
  }

  return "Что-то пошло не так при загрузке данных. Попробуйте повторить действие.";
}

async function readErrorMessage(response) {
  const text = await response.text();
  if (!text) {
    return "";
  }

  try {
    const parsed = JSON.parse(text);
    return parsed.message || parsed.error || parsed.title || text;
  } catch {
    return text;
  }
}
