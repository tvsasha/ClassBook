import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";
import {
  changePassword,
  clearUser,
  fetchCurrentUser,
  getRole,
  getTargetForUser,
  login,
  logout,
  readStoredUser
} from "./auth.js";

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
    return <main className="loading">Проверяем сессию...</main>;
  }

  if (user?.mustChangePassword || mode === "change-password") {
    return <ChangePasswordPage user={user} onChanged={setUser} />;
  }

  if (user) {
    return (
      <DashboardPage
        user={user}
        onLogout={async () => {
          await logout();
          setUser(null);
        }}
      />
    );
  }

  return <LoginPage onLogin={setUser} />;
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

  return (
    <main className="app-shell">
      <section className="hero">
        <BrandPanel />
        <section className="auth-card">
          <div className="auth-header">
            <h1>Вход в ClassBook</h1>
            <p>
              Новый React-вход уже использует cookie-сессию backend и умеет
              перенаправлять в старые разделы, пока мы переносим их по одному.
            </p>
          </div>
          <form onSubmit={handleSubmit}>
            <Field
              label="Логин"
              value={loginValue}
              onChange={setLoginValue}
              autoComplete="username"
            />
            <Field
              label="Пароль"
              type="password"
              value={password}
              onChange={setPassword}
              autoComplete="current-password"
            />
            <label className="consent">
              <input
                type="checkbox"
                checked={consent}
                onChange={(event) => setConsent(event.target.checked)}
              />
              <span>
                Я соглашаюсь на обработку персональных данных в рамках работы
                системы ClassBook.
              </span>
            </label>
            <button className="primary-button" disabled={submitting}>
              {submitting ? "Входим..." : "Войти"}
            </button>
            <p className="message">{message}</p>
          </form>
        </section>
      </section>
    </main>
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

  return (
    <main className="app-shell">
      <section className="hero">
        <BrandPanel />
        <section className="auth-card">
          <div className="auth-header">
            <h1>Смена временного пароля</h1>
            <p>
              {user
                ? `Пользователь: ${user.fullName || user.login}`
                : "Перед продолжением задайте постоянный пароль."}
            </p>
          </div>
          <p className="hint">
            После смены пароля React-оболочка откроет ваш рабочий раздел. Старые
            кабинеты пока остаются на прежних страницах.
          </p>
          <form onSubmit={handleSubmit}>
            <Field
              label="Текущий пароль"
              type="password"
              value={currentPassword}
              onChange={setCurrentPassword}
              autoComplete="current-password"
            />
            <Field
              label="Новый пароль"
              type="password"
              value={newPassword}
              onChange={setNewPassword}
              autoComplete="new-password"
            />
            <Field
              label="Повторите новый пароль"
              type="password"
              value={confirmPassword}
              onChange={setConfirmPassword}
              autoComplete="new-password"
            />
            <button className="primary-button" disabled={submitting}>
              {submitting ? "Сохраняем..." : "Сохранить пароль"}
            </button>
            <p className="message">{message}</p>
          </form>
        </section>
      </section>
    </main>
  );
}

function DashboardPage({ user, onLogout }) {
  const role = getRole(user);
  const primaryTarget = getTargetForUser(user);

  return (
    <main className="app-shell dashboard-wrap">
      <section className="dashboard-card">
        <div className="topline">
          <div>
            <h1>React-оболочка ClassBook</h1>
            <p>
              {user.fullName || user.login} · {role}
            </p>
          </div>
          <button className="ghost-button compact" onClick={onLogout}>
            Выйти
          </button>
        </div>
        <p className="hint">
          Это первый слой миграции. Он уже управляет входом и сессией, а рабочие
          кабинеты пока открывает в legacy-интерфейсе.
        </p>
        <div className="legacy-grid">
          <LegacyLink
            href={primaryTarget}
            title="Открыть мой раздел"
            text="Перейти в кабинет, соответствующий текущей роли."
          />
          <LegacyLink
            href="/director-dashboard.html"
            title="Отчёты директора"
            text="Доступно директору и администратору."
          />
          <LegacyLink
            href="/index.html"
            title="Администрирование"
            text="Пользователи, классы, предметы и ученики."
          />
        </div>
      </section>
    </main>
  );
}

function BrandPanel() {
  return (
    <section className="brand-card">
      <div className="eyebrow">ClassBook · Vite React migration</div>
      <h2 className="brand-title">Электронный журнал без резкого переезда.</h2>
      <p className="brand-copy">
        Мы переносим фронт постепенно: сначала вход, сессия и общая оболочка,
        затем админка, расписание, журнал учителя и порталы учеников/родителей.
      </p>
      <div className="role-grid">
        <RoleTile title="Администратор" text="Пользователи, доступы, классы и структура школы." />
        <RoleTile title="Учитель" text="Журнал, оценки, посещаемость и уроки." />
        <RoleTile title="Родитель и ученик" text="Безопасный доступ только к своим учебным данным." />
        <RoleTile title="Директор" text="Отчёты, аналитика и аудит действий." />
      </div>
    </section>
  );
}

function RoleTile({ title, text }) {
  return (
    <div className="role-tile">
      <strong>{title}</strong>
      <span>{text}</span>
    </div>
  );
}

function Field({ label, value, onChange, type = "text", autoComplete }) {
  return (
    <div className="field">
      <label>{label}</label>
      <input
        type={type}
        value={value}
        autoComplete={autoComplete}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}

function LegacyLink({ href, title, text }) {
  return (
    <a className="legacy-link" href={href}>
      <strong>{title}</strong>
      <span>{text}</span>
    </a>
  );
}

createRoot(document.getElementById("root")).render(<App />);
