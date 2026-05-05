import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";
import { apiRequest } from "./api.js";
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
  const route = useAppRoute();
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
      <AuthenticatedShell
        route={route}
        user={user}
        onLogout={async () => {
          await logout();
          setUser(null);
          window.location.hash = "";
        }}
      />
    );
  }

  return <LoginPage onLogin={setUser} />;
}

function useAppRoute() {
  const [locationKey, setLocationKey] = useState(() => `${window.location.pathname}${window.location.hash}`);

  useEffect(() => {
    const handleChange = () => setLocationKey(`${window.location.pathname}${window.location.hash}`);
    window.addEventListener("hashchange", handleChange);
    window.addEventListener("popstate", handleChange);

    return () => {
      window.removeEventListener("hashchange", handleChange);
      window.removeEventListener("popstate", handleChange);
    };
  }, []);

  return useMemo(() => resolveRoute(locationKey), [locationKey]);
}

function resolveRoute(locationKey) {
  const hashRoute = window.location.hash.replace(/^#\/?/, "").split("?")[0].toLowerCase();
  if (hashRoute) {
    return hashRoute;
  }

  const path = window.location.pathname.toLowerCase();
  if (path.endsWith("/index.html")) {
    return "admin";
  }

  if (path.endsWith("/director-dashboard.html")) {
    return "director";
  }

  if (path.endsWith("/teacher.html")) {
    return "teacher";
  }

  if (path.endsWith("/student-portal.html")) {
    return "student";
  }

  if (path.endsWith("/parent-portal.html")) {
    return "parent";
  }

  if (path.endsWith("/raspisanie.html")) {
    return "schedule";
  }

  return "home";
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
      window.location.hash = getTargetForUser(currentUser);
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
              React-приложение теперь управляет входом, ролями и основными
              страницами без переходов на отдельные HTML-файлы.
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
      window.location.hash = getTargetForUser(updatedUser);
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
            После смены пароля система откроет React-раздел, соответствующий
            вашей роли.
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

function AuthenticatedShell({ route, user, onLogout }) {
  const role = getRole(user);

  return (
    <main className="app-shell app-layout">
      <aside className="side-panel">
        <div>
          <div className="eyebrow">ClassBook React</div>
          <h1>Электронный журнал</h1>
          <p>{user.fullName || user.login}</p>
          <p className="role-badge">{role}</p>
        </div>
        <nav className="app-nav">
          <NavLink route="home" current={route}>Главная</NavLink>
          <NavLink route="admin" current={route}>Администрирование</NavLink>
          <NavLink route="director" current={route}>Отчеты директора</NavLink>
          <NavLink route="teacher" current={route}>Учитель</NavLink>
          <NavLink route="student" current={route}>Ученик</NavLink>
          <NavLink route="parent" current={route}>Родитель</NavLink>
          <NavLink route="schedule" current={route}>Расписание</NavLink>
        </nav>
        <button className="ghost-button" onClick={onLogout}>
          Выйти
        </button>
      </aside>
      <section className="content-panel">
        <RouteContent route={route} role={role} user={user} />
      </section>
    </main>
  );
}

function NavLink({ route, current, children }) {
  return (
    <a className={route === current ? "active" : ""} href={`#/${route}`}>
      {children}
    </a>
  );
}

function RouteContent({ route, role, user }) {
  switch (route) {
    case "admin":
      return <AdminPage role={role} />;
    case "director":
      return <DirectorPage role={role} />;
    case "teacher":
      return <RoleWorkspace title="Кабинет учителя" role={role} apiList={["/teacher/subjects", "/teacher/classes", "/teacher/lessons"]} />;
    case "student":
      return <RoleWorkspace title="Кабинет ученика" role={role} apiList={["/student/me/schedule", "/student/me/grades", "/student/me/attendance"]} />;
    case "parent":
      return <RoleWorkspace title="Кабинет родителя" role={role} apiList={["/parent/students"]} />;
    case "schedule":
      return <RoleWorkspace title="Расписание" role={role} apiList={["/schedule/week", "/schedule/editor/metadata"]} />;
    default:
      return <DashboardPage user={user} />;
  }
}

function DashboardPage({ user }) {
  const role = getRole(user);
  const primaryTarget = getTargetForUser(user);

  return (
    <section className="page-stack">
      <PageHeader
        title="Главная панель"
        subtitle={`${user.fullName || user.login} · ${role}`}
        text="Это уже React-приложение. Ссылки ниже не ведут на отдельные HTML-файлы, поэтому сервер больше не должен отвечать Cannot GET."
      />
      <div className="module-grid">
        <ModuleCard
          href={primaryTarget}
          title="Открыть мой раздел"
          text="Основной рабочий экран для текущей роли."
        />
        <ModuleCard
          href="#/admin"
          title="Администрирование"
          text="Пользователи, роли, ученики и выдача доступов."
        />
        <ModuleCard
          href="#/director"
          title="Отчеты директора"
          text="Сводка по классам, посещаемость, ежедневный контроль и аудит."
        />
      </div>
    </section>
  );
}

function AdminPage({ role }) {
  const allowed = role === "Администратор";
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [students, setStudents] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState({
    login: "",
    fullName: "",
    password: "",
    roleId: ""
  });

  async function loadAdminData() {
    setLoading(true);
    setMessage("");
    try {
      const [usersData, rolesData, studentsData] = await Promise.all([
        apiRequest("/users"),
        apiRequest("/roles"),
        apiRequest("/admin/students")
      ]);
      setUsers(usersData ?? []);
      setRoles(rolesData ?? []);
      setStudents(studentsData ?? []);
    } catch (error) {
      setMessage(error.message || "Не удалось загрузить данные администрирования");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (allowed) {
      loadAdminData();
    }
  }, [allowed]);

  async function createUser(event) {
    event.preventDefault();
    setMessage("");

    if (!form.login || !form.fullName || !form.password || !form.roleId) {
      setMessage("Заполните логин, ФИО, пароль и роль");
      return;
    }

    try {
      await apiRequest("/users", {
        method: "POST",
        body: JSON.stringify({
          login: form.login.trim(),
          fullName: form.fullName.trim(),
          password: form.password,
          roleId: Number(form.roleId)
        })
      });
      setForm({ login: "", fullName: "", password: "", roleId: "" });
      setMessage("Пользователь создан. Не забудьте передать временный пароль лично адресату.");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать пользователя");
    }
  }

  if (!allowed) {
    return <AccessWarning title="Администрирование доступно только администратору" />;
  }

  return (
    <section className="page-stack">
      <PageHeader
        title="Администрирование"
        subtitle="Пользователи, роли и ученики"
        text="Минимальная React-админка уже работает напрямую с backend API. Старый файл «index.html» больше не нужен для перехода из приложения."
      />
      <form className="inline-form" onSubmit={createUser}>
        <Field label="Логин" value={form.login} onChange={(value) => setForm({ ...form, login: value })} />
        <Field label="ФИО" value={form.fullName} onChange={(value) => setForm({ ...form, fullName: value })} />
        <Field label="Временный пароль" value={form.password} onChange={(value) => setForm({ ...form, password: value })} />
        <label className="field">
          <span>Роль</span>
          <select value={form.roleId} onChange={(event) => setForm({ ...form, roleId: event.target.value })}>
            <option value="">Выберите роль</option>
            {roles.map((item) => (
              <option key={item.id} value={item.id}>{item.name}</option>
            ))}
          </select>
        </label>
        <button className="primary-button">Создать пользователя</button>
      </form>
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Пользователей" value={users.length} />
        <MetricCard label="Активных" value={users.filter((item) => item.isActive).length} />
        <MetricCard label="Со временным паролем" value={users.filter((item) => item.mustChangePassword).length} />
        <MetricCard label="Учеников" value={students.length} />
      </div>
      <DataTable
        title="Пользователи"
        columns={["ФИО", "Логин", "Роль", "Статус", "Пароль"]}
        rows={users.slice(0, 12).map((item) => [
          item.fullName,
          item.login,
          item.roleName,
          item.isActive ? "Активен" : "Отключен",
          item.mustChangePassword ? "Нужно сменить" : "Постоянный"
        ])}
      />
      <DataTable
        title="Ученики"
        columns={["ФИО", "Класс", "Аккаунт"]}
        rows={students.slice(0, 12).map((item) => [
          `${item.lastName} ${item.firstName}`,
          item.className || "Без класса",
          item.hasAccount ? "Есть" : "Не выдан"
        ])}
      />
    </section>
  );
}

function DirectorPage({ role }) {
  const allowed = role === "Директор" || role === "Администратор";
  const [summary, setSummary] = useState(null);
  const [attendance, setAttendance] = useState(null);
  const [daily, setDaily] = useState(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  async function loadReports() {
    setLoading(true);
    setMessage("");
    try {
      const [classSummary, attendanceStats, dailyReport] = await Promise.all([
        apiRequest("/director/report/class-summary"),
        apiRequest("/director/report/attendance"),
        apiRequest("/director/report/daily")
      ]);
      setSummary(classSummary);
      setAttendance(attendanceStats);
      setDaily(dailyReport);
    } catch (error) {
      setMessage(error.message || "Не удалось загрузить отчеты директора");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (allowed) {
      loadReports();
    }
  }, [allowed]);

  if (!allowed) {
    return <AccessWarning title="Отчеты доступны директору и администратору" />;
  }

  const classRows = summary?.classSummary ?? [];
  const attendanceRows = attendance?.statistics ?? [];
  const dailyRows = daily?.report ?? [];

  return (
    <section className="page-stack">
      <PageHeader
        title="Отчеты директора"
        subtitle="Аналитика учебного процесса"
        text="Эта панель уже открывается внутри React и получает данные из директорских API без перехода на «director-dashboard.html»."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Уроков за день" value={daily?.totalLessons ?? 0} />
        <MetricCard label="Оценки заполнены" value={daily?.lessonsWithCompleteGrades ?? 0} />
        <MetricCard label="Посещаемость заполнена" value={daily?.lessonsWithCompleteAttendance ?? 0} />
        <MetricCard label="Классов в сводке" value={classRows.length} />
      </div>
      <DataTable
        title="Сводка по классам"
        columns={["Класс", "Учеников", "Средние пропуски", "Средняя оценка"]}
        rows={classRows.map((item) => [
          item.className,
          item.studentCount,
          formatNumber(item.averageAbsences),
          formatNumber(item.averageGrade)
        ])}
      />
      <DataTable
        title="Посещаемость"
        columns={["Класс", "Всего", "Присутствуют", "Отсутствуют", "% присутствия"]}
        rows={attendanceRows.map((item) => [
          item.className,
          item.totalStudents,
          item.present,
          item.absent,
          `${formatNumber(item.presentPercentage)}%`
        ])}
      />
      <DataTable
        title="Ежедневный контроль"
        columns={["Урок", "Учитель", "Класс", "Оценки", "Посещаемость"]}
        rows={dailyRows.slice(0, 12).map((item) => [
          item.name,
          item.teacher,
          item.class,
          `${formatNumber(item.gradesPercentage)}%`,
          `${formatNumber(item.attendancePercentage)}%`
        ])}
      />
    </section>
  );
}

function RoleWorkspace({ title, role, apiList }) {
  const [result, setResult] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  async function loadPreview() {
    setLoading(true);
    setMessage("");
    try {
      const responses = await Promise.allSettled(apiList.map((path) => apiRequest(path)));
      setResult(responses.map((item, index) => ({
        path: apiList[index],
        status: item.status,
        value: item.status === "fulfilled" ? item.value : item.reason?.message
      })));
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="page-stack">
      <PageHeader
        title={title}
        subtitle={`Текущая роль: ${role}`}
        text="Эта страница уже находится внутри React-приложения. Детальный перенос интерфейса можно делать по блокам, но переходов на отдельные HTML больше нет."
      />
      <button className="primary-button compact" onClick={loadPreview}>
        Проверить API раздела
      </button>
      <StatusLine loading={loading} message={message} />
      <div className="api-list">
        {apiList.map((path) => (
          <code key={path}>{path}</code>
        ))}
      </div>
      {result.length > 0 && (
        <DataTable
          title="Результат проверки"
          columns={["API", "Статус", "Ответ"]}
          rows={result.map((item) => [
            item.path,
            item.status === "fulfilled" ? "OK" : "Ошибка",
            item.status === "fulfilled" ? summarizeValue(item.value) : item.value
          ])}
        />
      )}
    </section>
  );
}

function BrandPanel() {
  return (
    <section className="brand-card">
      <div className="eyebrow">ClassBook · React shell</div>
      <h2 className="brand-title">Один вход. Одна оболочка. Без HTML-переездов.</h2>
      <p className="brand-copy">
        Админка, директорская аналитика и рабочие разделы постепенно живут в
        React, а backend остается единым источником данных и прав доступа.
      </p>
      <div className="role-grid">
        <RoleTile title="Администратор" text="Пользователи, доступы, классы и структура школы." />
        <RoleTile title="Учитель" text="Журнал, оценки, посещаемость и уроки." />
        <RoleTile title="Родитель и ученик" text="Безопасный доступ только к своим учебным данным." />
        <RoleTile title="Директор" text="Отчеты, аналитика и аудит действий." />
      </div>
    </section>
  );
}

function PageHeader({ title, subtitle, text }) {
  return (
    <header className="page-header">
      <span>{subtitle}</span>
      <h2>{title}</h2>
      <p>{text}</p>
    </header>
  );
}

function ModuleCard({ href, title, text }) {
  return (
    <a className="module-card" href={href}>
      <strong>{title}</strong>
      <span>{text}</span>
    </a>
  );
}

function MetricCard({ label, value }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function DataTable({ title, columns, rows }) {
  return (
    <section className="table-card">
      <div className="table-title">{title}</div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column}>{column}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length}>Данных пока нет</td>
              </tr>
            ) : rows.map((row, index) => (
              <tr key={`${title}-${index}`}>
                {row.map((cell, cellIndex) => (
                  <td key={`${title}-${index}-${cellIndex}`}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function AccessWarning({ title }) {
  return (
    <section className="page-stack">
      <PageHeader
        title={title}
        subtitle="Доступ ограничен"
        text="Backend не отдаст данные без нужной роли. React дополнительно скрывает страницу, чтобы пользователь сразу понимал причину."
      />
    </section>
  );
}

function StatusLine({ loading, message }) {
  if (loading) {
    return <p className="status-line">Загружаем данные...</p>;
  }

  if (message) {
    return <p className="status-line">{message}</p>;
  }

  return null;
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

function formatNumber(value) {
  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric) ? numeric.toFixed(1) : "0.0";
}

function summarizeValue(value) {
  if (Array.isArray(value)) {
    return `${value.length} записей`;
  }

  if (value && typeof value === "object") {
    return Object.keys(value).slice(0, 4).join(", ") || "Объект";
  }

  return String(value ?? "Пустой ответ");
}

createRoot(document.getElementById("root")).render(<App />);
