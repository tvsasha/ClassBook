import { apiRequest } from "./api.js";

const currentUserKey = "currentUser";

export const roleTargets = {
    "Администратор": "/index.html",
    "Учитель": "/teacher.html",
    "Ученик": "/student-portal.html",
    "Родитель": "/parent-portal.html",
    "Менеджер расписания": "/raspisanie.html",
    "Директор": "/director-dashboard.html"
};

export function readStoredUser() {
    const raw = sessionStorage.getItem(currentUserKey);
    if (!raw || raw === "null" || raw === "undefined") {
        return null;
    }

    try {
        return JSON.parse(raw);
    } catch {
        sessionStorage.removeItem(currentUserKey);
        return null;
    }
}

export function storeUser(user) {
    sessionStorage.setItem(currentUserKey, JSON.stringify(user));
}

export function clearUser() {
    sessionStorage.removeItem(currentUserKey);
}

export function getRole(user) {
    return user?.role || user?.roleName || "";
}

export function getTargetForUser(user) {
    if (!user) {
        return "/app/";
    }

    if (user.mustChangePassword) {
        return "/app/?mode=change-password";
    }

    return roleTargets[getRole(user)] || "/app/";
}

export async function login(loginValue, password) {
    const user = await apiRequest("/auth/login", {
        method: "POST",
        body: JSON.stringify({ login: loginValue, password })
    });
    storeUser(user);
    return user;
}

export async function logout() {
    try {
        await apiRequest("/auth/logout", { method: "POST" });
    } finally {
        clearUser();
    }
}

export async function changePassword(currentPassword, newPassword) {
    const user = await apiRequest("/auth/change-password", {
        method: "POST",
        body: JSON.stringify({ currentPassword, newPassword })
    });
    storeUser(user);
    return user;
}

export async function fetchCurrentUser() {
    const user = await apiRequest("/auth/me");
    storeUser(user);
    return user;
}
