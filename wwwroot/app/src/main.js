import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import {
    changePassword,
    clearUser,
    fetchCurrentUser,
    getRole,
    getTargetForUser,
    login,
    logout,
    readStoredUser,
    roleTargets
} from "./auth.js";

const h = React.createElement;

function App() {
    const [user, setUser] = useState(readStoredUser);
    const [loading, setLoading] = useState(true);
    const mode = new URLSearchParams(window.location.search).get("mode");

    useEffect(() => {
        let ignore = false;

        fetchCurrentUser()
            .then((currentUser) => {
                if (!ignore) {
                    setUser(currentUser);
                }
            })
            .catch(() => {
                clearUser();
                if (!ignore) {
                    setUser(null);
                }
            })
            .finally(() => {
                if (!ignore) {
                    setLoading(false);
                }
            });

        return () => {
            ignore = true;
        };
    }, []);

    if (loading) {
        return h("main", { className: "loading" }, "Проверяем сессию...");
    }

    if (user?.mustChangePassword || mode === "change-password") {
        return h(ChangePasswordPage, { user, onChanged: setUser });
    }

    if (user) {
        return h(DashboardPage, { user, onLogout: async () => {
            await logout();
            setUser(null);
        } });
    }

    return h(LoginPage, { onLogin: setUser });
}

function LoginPage({ onLogin }) {
    const [loginValue, setLoginValue] = useState("");
    const [password, setPassword] = useState("");
    const [consent, setConsent] = useState(true);
    const [message, setMessage] = useState("");
    const [submitting, setSubmitting] = useState(false);

    async function handleSubmit(event) {
        event.preventDefault();
        setMessage("");

        if (!loginValue.trim() || !password.trim()) {
            setMessage("Заполните логин и пароль");
            return;
        }

        if (!consent) {
            setMessage("Подтвердите согласие на обработку персональных данных");
            return;
        }

        setSubmitting(true);
        try {
            const currentUser = await login(loginValue.trim(), password.trim());
            onLogin(currentUser);
        } catch (error) {
            setMessage(error.message || "Не удалось выполнить вход");
        } finally {
            setSubmitting(false);
        }
    }

    return h("main", { className: "app-shell" },
        h("section", { className: "hero" },
            h(BrandPanel),
            h("section", { className: "auth-card" },
                h("div", { className: "auth-header" },
                    h("h1", null, "Вход в ClassBook"),
                    h("p", null, "Новый React-вход уже использует cookie-сессию backend и умеет перенаправлять в старые разделы, пока мы переносим их по одному.")
                ),
                h("form", { onSubmit: handleSubmit },
                    h(Field, {
                        label: "Логин",
                        value: loginValue,
                        onChange: setLoginValue,
                        autoComplete: "username"
                    }),
                    h(Field, {
                        label: "Пароль",
                        type: "password",
                        value: password,
                        onChange: setPassword,
                        autoComplete: "current-password"
                    }),
                    h("label", { className: "consent" },
                        h("input", {
                            type: "checkbox",
                            checked: consent,
                            onChange: (event) => setConsent(event.target.checked)
                        }),
                        h("span", null, "Я соглашаюсь на обработку персональных данных в рамках работы системы ClassBook.")
                    ),
                    h("button", { className: "primary-button", disabled: submitting },
                        submitting ? "Входим..." : "Войти"
                    ),
                    h("p", { className: "message" }, message)
                )
            )
        )
    );
}

function ChangePasswordPage({ user, onChanged }) {
    const [currentPassword, setCurrentPassword] = useState("");
    const [newPassword, setNewPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [message, setMessage] = useState("");
    const [submitting, setSubmitting] = useState(false);

    async function handleSubmit(event) {
        event.preventDefault();
        setMessage("");

        if (!currentPassword || !newPassword || !confirmPassword) {
            setMessage("Заполните все поля");
            return;
        }

        if (newPassword.length < 8) {
            setMessage("Новый пароль должен содержать не менее 8 символов");
            return;
        }

        if (newPassword !== confirmPassword) {
            setMessage("Новый пароль и подтверждение не совпадают");
            return;
        }

        setSubmitting(true);
        try {
            const updatedUser = await changePassword(currentPassword, newPassword);
            onChanged(updatedUser);
        } catch (error) {
            setMessage(error.message || "Не удалось сменить пароль");
        } finally {
            setSubmitting(false);
        }
    }

    return h("main", { className: "app-shell" },
        h("section", { className: "hero" },
            h(BrandPanel),
            h("section", { className: "auth-card" },
                h("div", { className: "auth-header" },
                    h("h1", null, "Смена временного пароля"),
                    h("p", null, user ? `Пользователь: ${user.fullName || user.login}` : "Перед продолжением задайте постоянный пароль.")
                ),
                h("p", { className: "hint" }, "После смены пароля React-оболочка откроет ваш рабочий раздел. Старые кабинеты пока остаются на прежних страницах."),
                h("form", { onSubmit: handleSubmit },
                    h(Field, {
                        label: "Текущий пароль",
                        type: "password",
                        value: currentPassword,
                        onChange: setCurrentPassword,
                        autoComplete: "current-password"
                    }),
                    h(Field, {
                        label: "Новый пароль",
                        type: "password",
                        value: newPassword,
                        onChange: setNewPassword,
                        autoComplete: "new-password"
                    }),
                    h(Field, {
                        label: "Повторите новый пароль",
                        type: "password",
                        value: confirmPassword,
                        onChange: setConfirmPassword,
                        autoComplete: "new-password"
                    }),
                    h("button", { className: "primary-button", disabled: submitting },
                        submitting ? "Сохраняем..." : "Сохранить пароль"
                    ),
                    h("p", { className: "message" }, message)
                )
            )
        )
    );
}

function DashboardPage({ user, onLogout }) {
    const role = getRole(user);
    const primaryTarget = getTargetForUser(user);

    return h("main", { className: "app-shell dashboard-wrap" },
        h("section", { className: "dashboard-card" },
            h("div", { className: "topline" },
                h("div", null,
                    h("h1", null, "React-оболочка ClassBook"),
                    h("p", null, `${user.fullName || user.login} · ${role}`)
                ),
                h("button", { className: "ghost-button", style: { width: "auto" }, onClick: onLogout }, "Выйти")
            ),
            h("p", { className: "hint" }, "Это первый слой миграции. Он уже управляет входом и сессией, а рабочие кабинеты пока открывает в legacy-интерфейсе."),
            h("div", { className: "legacy-grid" },
                h(LegacyLink, {
                    href: primaryTarget,
                    title: "Открыть мой раздел",
                    text: "Перейти в кабинет, соответствующий текущей роли."
                }),
                h(LegacyLink, {
                    href: "/director-dashboard.html",
                    title: "Отчёты директора",
                    text: "Доступно директору и администратору."
                }),
                h(LegacyLink, {
                    href: "/index.html",
                    title: "Администрирование",
                    text: "Пользователи, классы, предметы и ученики."
                })
            )
        )
    );
}

function BrandPanel() {
    return h("section", { className: "brand-card" },
        h("div", { className: "eyebrow" }, "ClassBook · React migration"),
        h("h2", { className: "brand-title" }, "Электронный журнал без резкого переезда."),
        h("p", { className: "brand-copy" }, "Мы переносим фронт постепенно: сначала вход, сессия и общая оболочка, затем админка, расписание, журнал учителя и порталы учеников/родителей."),
        h("div", { className: "role-grid" },
            h(RoleTile, { title: "Администратор", text: "Пользователи, доступы, классы и структура школы." }),
            h(RoleTile, { title: "Учитель", text: "Журнал, оценки, посещаемость и уроки." }),
            h(RoleTile, { title: "Родитель и ученик", text: "Безопасный доступ только к своим учебным данным." }),
            h(RoleTile, { title: "Директор", text: "Отчёты, аналитика и аудит действий." })
        )
    );
}

function RoleTile({ title, text }) {
    return h("div", { className: "role-tile" },
        h("strong", null, title),
        h("span", null, text)
    );
}

function Field({ label, value, onChange, type = "text", autoComplete }) {
    return h("div", { className: "field" },
        h("label", null, label),
        h("input", {
            type,
            value,
            autoComplete,
            onChange: (event) => onChange(event.target.value)
        })
    );
}

function LegacyLink({ href, title, text }) {
    return h("a", { className: "legacy-link", href },
        h("strong", null, title),
        h("span", null, text)
    );
}

createRoot(document.getElementById("root")).render(h(App));
