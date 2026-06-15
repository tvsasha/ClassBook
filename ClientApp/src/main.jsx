import React, { useEffect, useId, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";
import { apiBase, apiRequest, csrfHeaders } from "./api.js";
import {
  changePassword,
  clearUser,
  fetchCurrentUser,
  getRole,
  getTargetForUser,
  heartbeat,
  login,
  logout,
  readStoredUser,
  sendOfflineBeacon
} from "./auth.js";

async function copyTextToClipboard(text) {
  const clipboard = typeof navigator !== "undefined" ? navigator.clipboard : null;
  const writeText = clipboard && typeof clipboard.writeText === "function"
    ? clipboard.writeText.bind(clipboard)
    : null;

  if (writeText) {
    try {
      await writeText(text);
      return;
    } catch {
      // Clipboard API can be blocked on plain HTTP, so fall back below.
    }
  }

  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.left = "-9999px";
  textarea.style.top = "0";
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();

  const copied = document.execCommand("copy");
  document.body.removeChild(textarea);

  if (!copied) {
    throw new Error("copy_failed");
  }
}

const themeStorageKey = "classbook-theme";
const cookieConsentStorageKey = "classbook-cookie-consent";

function getInitialTheme() {
  const storedTheme = window.localStorage.getItem(themeStorageKey);
  if (storedTheme === "light" || storedTheme === "dark") {
    return storedTheme;
  }

  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function App() {
  const [user, setUser] = useState(readStoredUser);
  const [loading, setLoading] = useState(true);
  const [theme, setTheme] = useState(getInitialTheme);
  const route = useAppRoute();
  const mode = new URLSearchParams(window.location.search).get("mode");

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
    window.localStorage.setItem(themeStorageKey, theme);
  }, [theme]);

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

  useEffect(() => {
    if (!user) {
      return undefined;
    }

    let stopped = false;
    const sendHeartbeat = () => {
      if (stopped || document.visibilityState !== "visible") {
        return;
      }

      heartbeat().catch(() => {});
    };
    const handleVisibility = () => {
      if (document.visibilityState === "visible") {
        sendHeartbeat();
      } else {
        sendOfflineBeacon();
      }
    };
    const handlePageHide = () => sendOfflineBeacon();

    sendHeartbeat();
    const intervalId = window.setInterval(sendHeartbeat, 30000);
    document.addEventListener("visibilitychange", handleVisibility);
    window.addEventListener("pagehide", handlePageHide);

    return () => {
      stopped = true;
      window.clearInterval(intervalId);
      document.removeEventListener("visibilitychange", handleVisibility);
      window.removeEventListener("pagehide", handlePageHide);
    };
  }, [user?.id]);

  let content;
  if (loading) {
    content = <main className="loading">Проверяем сессию...</main>;
  } else if (user?.mustChangePassword || mode === "change-password") {
    content = <ChangePasswordPage user={user} onChanged={setUser} />;
  } else if (user) {
    content = (
      <AuthenticatedShell
        route={route}
        user={user}
        theme={theme}
        onThemeToggle={() => setTheme((current) => current === "dark" ? "light" : "dark")}
        onLogout={async () => {
          await logout();
          setUser(null);
          window.location.hash = "";
        }}
      />
    );
  } else {
    content = <LoginPage onLogin={setUser} />;
  }

  return (
    <>
      {!user && (
        <ThemeToggle
          theme={theme}
          className="floating-theme-toggle"
          onToggle={() => setTheme((current) => current === "dark" ? "light" : "dark")}
        />
      )}
      {content}
      <CookieBanner />
    </>
  );
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
              Войдите в электронный журнал, чтобы открыть расписание, дневник,
              отчеты или рабочий журнал в соответствии с вашей ролью.
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
            После смены временного пароля система откроет рабочий раздел,
            соответствующий вашей роли.
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

function ThemeToggle({ theme, onToggle, className = "" }) {
  const dark = theme === "dark";
  return (
    <button
      className={`theme-toggle ${className}`.trim()}
      type="button"
      onClick={onToggle}
      aria-label={dark ? "Включить светлую тему" : "Включить темную тему"}
      title={dark ? "Светлая тема" : "Темная тема"}
    >
      <span className="theme-toggle-track" aria-hidden="true">
        <svg className="theme-toggle-sun" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.66 6.34l1.41-1.41" />
        </svg>
        <svg className="theme-toggle-moon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79Z" />
        </svg>
        <span className="theme-toggle-thumb" />
      </span>
      <span className="theme-toggle-label">{dark ? "Светлая" : "Темная"}</span>
    </button>
  );
}

function CookieBanner() {
  const [visible, setVisible] = useState(() => !window.localStorage.getItem(cookieConsentStorageKey));

  function saveChoice(choice) {
    window.localStorage.setItem(cookieConsentStorageKey, choice);
    setVisible(false);
  }

  if (!visible) {
    return null;
  }

  return (
    <aside className="cookie-banner" role="dialog" aria-label="Использование cookie" aria-live="polite">
      <div className="cookie-banner-copy">
        <strong>Cookie в ClassBook</strong>
        <p>
          Система использует обязательные cookie для безопасного входа и сохранения сессии.
          Аналитические и рекламные cookie не используются.
        </p>
      </div>
      <div className="cookie-banner-actions">
        <button className="ghost-button compact" type="button" onClick={() => saveChoice("necessary")}>
          Не согласен
        </button>
        <button className="primary-button compact" type="button" onClick={() => saveChoice("accepted")}>
          Согласен
        </button>
      </div>
    </aside>
  );
}

function AuthenticatedShell({ route, user, theme, onThemeToggle, onLogout }) {
  const role = getRole(user);
  const navItems = getNavItemsForRole(role);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const currentNavItem = navItems.find((item) => item.route === route);
  const routeClass = route === "admin" ? "route-admin" : "";

  return (
    <main className={`app-shell app-layout ${sidebarCollapsed ? "sidebar-collapsed" : ""} ${routeClass}`}>
      <aside className="side-panel">
        <div className="side-brand">
          <div className="eyebrow">ClassBook</div>
          <h1>Электронный журнал</h1>
          <p>{user.fullName || user.login}</p>
        </div>
        <nav className="app-nav">
          {navItems.map((item) => (
            <NavLink key={item.route} route={item.route} current={route}>{item.label}</NavLink>
          ))}
        </nav>
        <div className="side-actions">
          <button className="ghost-button compact sidebar-toggle" type="button" onClick={() => setSidebarCollapsed((current) => !current)}>
            {sidebarCollapsed ? "Развернуть" : "Свернуть"}
          </button>
          <button className="ghost-button" onClick={onLogout}>
            Выйти
          </button>
        </div>
      </aside>
      <section className="content-panel">
        <header className="topbar">
          <div>
            <span>Главная / {currentNavItem?.label || "Обзор"}</span>
            <strong>{currentNavItem?.label || "Главная"}</strong>
          </div>
          <div className="topbar-actions">
            <ThemeToggle theme={theme} onToggle={onThemeToggle} />
            <div className="topbar-user">
              <span>{user.fullName || user.login}</span>
              <b>{(user.fullName || user.login || "C").slice(0, 1).toUpperCase()}</b>
            </div>
          </div>
        </header>
        <RouteContent route={route} role={role} user={user} />
      </section>
    </main>
  );
}

function getNavItemsForRole(role) {
  const common = [{ route: "home", label: "Главная" }];
  const byRole = {
    "Администратор": [
      { route: "admin", label: "Администрирование" },
      { route: "director", label: "Отчеты директора" },
      { route: "teacher", label: "Журнал учителя" },
      { route: "student", label: "Данные ученика" },
      { route: "parent", label: "Данные родителя" },
      { route: "schedule", label: "Расписание" }
    ],
    "Директор": [
      { route: "director", label: "Отчеты директора" },
      { route: "teacher", label: "Журналы" },
      { route: "schedule", label: "Расписание" }
    ],
    "Учитель": [
      { route: "teacher", label: "Журнал учителя" },
      { route: "class-teacher", label: "Классное руководство" },
      { route: "schedule", label: "Мое расписание" }
    ],
    "Ученик": [
      { route: "student", label: "Мой дневник" },
      { route: "schedule", label: "Мое расписание" }
    ],
    "Родитель": [
      { route: "parent", label: "Дневник ребенка" }
    ],
    "Менеджер расписания": [
      { route: "schedule", label: "Редактор расписания" }
    ]
  };

  return [...common, ...(byRole[role] ?? [])];
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
      return <TeacherPage role={role} user={user} />;
    case "class-teacher":
      return <ClassTeacherPage role={role} />;
    case "student":
      return <StudentPage role={role} />;
    case "parent":
      return <ParentPage role={role} />;
    case "schedule":
      if (role === "Учитель") {
        return <SchedulePage role={role} user={user} />;
      }
      if (role === "Ученик") {
        return <StudentPage role={role} view="schedule" />;
      }
      return <SchedulePage role={role} user={user} />;
    default:
      return <DashboardPage user={user} />;
  }
}

function DashboardPage({ user }) {
  const role = getRole(user);
  const primaryTarget = getWorkspaceTargetForRole(role);
  const [dashboard, setDashboard] = useState(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let ignore = false;

    async function loadDashboard() {
      setLoading(true);
      setMessage("");
      try {
        const data = await loadRoleDashboard(role, user);
        if (!ignore) {
          setDashboard(data);
        }
      } catch (error) {
        if (!ignore) {
          setMessage(error.message || "Не удалось загрузить главную панель");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadDashboard();

    return () => {
      ignore = true;
    };
  }, [role, user?.id]);

  useEffect(() => {
    if (role !== "Администратор") {
      return undefined;
    }

    let ignore = false;
    async function refreshOnlineUsers() {
      try {
        const section = await loadOnlineUsersSection();
        if (!ignore) {
          setDashboard((current) => updateDashboardSection(current, section));
        }
      } catch {
        // Тихое обновление: если сеть мигнула, не мешаем работе главной панели.
      }
    }

    const intervalId = window.setInterval(refreshOnlineUsers, 15000);
    return () => {
      ignore = true;
      window.clearInterval(intervalId);
    };
  }, [role]);

  return (
    <section className="page-stack">
      <PageHeader
        title="Главная панель"
        subtitle={`${user.fullName || user.login} · ${role}`}
        text={getDashboardIntro(role)}
      />
      <StatusLine loading={loading} message={message} />
      <DashboardOverview dashboard={dashboard} role={role} />
      <div className="module-grid dashboard-actions">
        {primaryTarget !== "#/admin" && primaryTarget !== "#/director" && (
          <ModuleCard
            href={primaryTarget}
            title="Открыть мой раздел"
            text="Основной рабочий экран для текущей роли."
          />
        )}
        {role === "Администратор" && (
          <>
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
          </>
        )}
        {role === "Директор" && (
          <ModuleCard
            href="#/director"
            title="Отчеты директора"
            text="Сводка по классам, посещаемость, ежедневный контроль и аудит."
          />
        )}
      </div>
    </section>
  );
}

function getWorkspaceTargetForRole(role) {
  const targets = {
    "Администратор": "#/admin",
    "Директор": "#/director",
    "Учитель": "#/teacher",
    "Ученик": "#/student",
    "Родитель": "#/parent",
    "Менеджер расписания": "#/schedule"
  };

  return targets[role] || "#/home";
}

async function loadRoleDashboard(role, user) {
  if (role === "Администратор") {
    const [users, students, classes, subjects, classTeachers] = await Promise.all([
      apiRequest("/users"),
      apiRequest("/admin/students"),
      apiRequest("/classes"),
      apiRequest("/subjects"),
      apiRequest("/class-teacher/assignments")
    ]);
    const assignments = classTeachers ?? [];
    return {
      metrics: [
        { label: "Пользователей", value: users?.length ?? 0 },
        { label: "Учеников", value: students?.length ?? 0 },
        { label: "Классов", value: classes?.length ?? 0 },
        { label: "Предметов", value: subjects?.length ?? 0 }
      ],
      sections: [
        {
          title: "Что требует внимания",
          items: [
            `${(users ?? []).filter((item) => item.mustChangePassword).length} учетных записей с временным паролем`,
            `${(students ?? []).filter((item) => !item.hasAccount).length} учеников без выданного доступа`,
            `${(classes ?? []).filter((item) => !assignments.some((assignment) => Number(assignment.classId) === Number(item.classId))).length} классов без классного руководителя`
          ]
        },
        buildOnlineUsersSection(users ?? [])
      ]
    };
  }

  if (role === "Директор") {
    const period = getDirectorPeriod("week");
    const query = `startDate=${period.start}&endDate=${period.end}`;
    const [classSummary, attendance, daily, teachers] = await Promise.all([
      apiRequest(`/director/report/class-summary?${query}`),
      apiRequest(`/director/report/attendance?${query}`),
      apiRequest(`/director/report/daily?date=${period.end}`),
      apiRequest(`/director/report/teachers?${query}`)
    ]);
    const attendanceRows = attendance?.statistics ?? [];
    const averageAttendance = attendanceRows.length
      ? calculateAverage(attendanceRows.map((item) => item.presentPercentage))
      : 0;
    const averageGrade = calculateAverage((classSummary?.classSummary ?? []).map((item) => item.averageGrade).filter((value) => Number(value) > 0));
    return {
      metrics: [
        { label: "Уроков сегодня", value: daily?.totalLessons ?? 0 },
        { label: "Средняя посещаемость", value: `${formatNumber(averageAttendance)}%` },
        { label: "Средняя оценка", value: formatNumber(averageGrade) },
        { label: "Учителей", value: teachers?.teachers?.length ?? 0 }
      ],
      sections: [
        {
          title: "Ежедневный контроль",
          table: {
            columns: ["Урок", "Учитель", "Класс", "Оценки", "Посещаемость"],
            rows: (daily?.report ?? []).slice(0, 8).map((item) => [
              item.name,
              item.teacher,
              item.class,
              `${formatNumber(item.gradesPercentage)}%`,
              `${formatNumber(item.attendancePercentage)}%`
            ])
          }
        },
        {
          title: "Классы: успеваемость и посещаемость",
          table: {
            columns: ["Класс", "Учеников", "Пропусков на ученика", "Средняя"],
            rows: (classSummary?.classSummary ?? []).slice(0, 8).map((item) => [
              item.className,
              item.studentCount,
              formatNumber(item.averageAbsences),
              formatNumber(item.averageGrade)
            ])
          }
        }
      ]
    };
  }

  if (role === "Учитель") {
    const [classes, subjects, lessons, classTeacherDashboard] = await Promise.all([
      apiRequest(`/teacher/classes?teacherId=${user.id}`),
      apiRequest(`/teacher/subjects?teacherId=${user.id}`),
      apiRequest(`/teacher/lessons?teacherId=${user.id}`),
      apiRequest("/class-teacher/me/dashboard").catch(() => null)
    ]);
    const upcomingLessons = (lessons ?? [])
      .filter((lesson) => parseLocalDate(lesson.date) >= getTodayStart())
      .sort(comparePortalLessons);
    return {
      metrics: [
        { label: "Моих уроков", value: lessons?.length ?? 0 },
        { label: "Ближайших", value: upcomingLessons.length },
        { label: "Классов", value: classes?.length ?? 0 },
        { label: "Предметов", value: subjects?.length ?? 0 }
      ],
      sections: [
        {
          title: "Ближайшие уроки",
          table: {
            columns: ["Дата", "Класс", "Предмет", "Тема", "ДЗ"],
            rows: upcomingLessons.slice(0, 8).map((lesson) => [
              formatDate(lesson.date),
              lesson.className,
              lesson.subjectName,
              formatLessonTopic(lesson.topic),
              lesson.homework || "Не задано"
            ])
          }
        },
        {
          title: "Классное руководство",
          items: (classTeacherDashboard?.classes ?? []).slice(0, 4).map((item) =>
            `${item.className}: ${item.studentsCount} учеников, средняя ${formatNumber(item.averageGrade)}, пропусков ${item.absencesCount}`
          )
        }
      ]
    };
  }

  if (role === "Ученик") {
    const [info, schedule, grades, homework, attendance] = await Promise.all([
      apiRequest("/student/me/class"),
      apiRequest("/student/me/schedule"),
      apiRequest("/student/me/grades"),
      apiRequest("/student/me/homework"),
      apiRequest("/student/me/attendance")
    ]);
    const todaySchedule = getTodaySchedule(schedule ?? []);
    const problems = (attendance ?? []).filter((item) => Number(item.status ?? 1) !== 1);
    return {
      metrics: [
        { label: "Уроков сегодня", value: todaySchedule.length },
        { label: "Оценок", value: grades?.length ?? 0 },
        { label: "Средний балл", value: formatNumber(calculateAverage((grades ?? []).map((item) => item.value))) },
        { label: "Пропусков/опозданий", value: problems.length }
      ],
      sections: [
        {
          title: "Сегодня",
          table: {
            columns: ["Урок", "Время", "Предмет", "Тема", "ДЗ"],
            rows: todaySchedule.slice(0, 8).map((lesson) => [
              lesson.lessonNumber || "—",
              formatTimeRange(lesson),
              lesson.subject || lesson.subjectName,
              formatLessonTopic(lesson.topic),
              lesson.homework || "Не задано"
            ])
          }
        },
        {
          title: `Профиль: ${info?.lastName ?? ""} ${info?.firstName ?? ""}`.trim(),
          items: [
            `Класс: ${info?.class?.name || "не указан"}`,
            `Актуальных домашних заданий: ${(homework ?? []).length}`,
            `Последняя оценка: ${(grades ?? [])[0]?.value ?? "пока нет"}`
          ]
        }
      ]
    };
  }

  if (role === "Родитель") {
    const students = await apiRequest("/parent/students");
    const selectedStudent = (students ?? [])[0];
    if (!selectedStudent) {
      return {
        metrics: [
          { label: "Детей", value: 0 },
          { label: "Оценок", value: 0 },
          { label: "ДЗ", value: 0 },
          { label: "Посещаемость", value: "—" }
        ],
        sections: [{ title: "Нет привязанных учеников", items: ["Администратор еще не привязал ребенка к учетной записи родителя."] }]
      };
    }

    const [schedule, grades, homework, attendance] = await Promise.all([
      apiRequest(`/parent/student/${selectedStudent.studentId}/schedule`),
      apiRequest(`/parent/student/${selectedStudent.studentId}/grades`),
      apiRequest(`/parent/student/${selectedStudent.studentId}/homework`),
      apiRequest(`/parent/student/${selectedStudent.studentId}/attendance`)
    ]);
    const problems = (attendance ?? []).filter((item) => Number(item.status ?? 1) !== 1);
    return {
      metrics: [
        { label: "Детей", value: students?.length ?? 0 },
        { label: "Средний балл", value: formatNumber(calculateAverage((grades ?? []).map((item) => item.value))) },
        { label: "Домашних заданий", value: homework?.length ?? 0 },
        { label: "Пропусков/опозданий", value: problems.length }
      ],
      sections: [
        {
          title: `${selectedStudent.firstName} ${selectedStudent.lastName}`,
          items: [
            `Класс: ${selectedStudent.class?.name || "не указан"}`,
            `Уроков в расписании: ${(schedule ?? []).length}`,
            `Последних оценок: ${(grades ?? []).length}`
          ]
        },
        {
          title: "Ближайшие занятия",
          table: {
            columns: ["Дата", "Предмет", "Тема", "ДЗ"],
            rows: (schedule ?? []).slice(0, 8).map((lesson) => [
              formatDate(lesson.date),
              lesson.subject || lesson.subjectName,
              formatLessonTopic(lesson.topic),
              lesson.homework || "Не задано"
            ])
          }
        }
      ]
    };
  }

  if (role === "Менеджер расписания") {
    const weekStart = toIsoDate(getMonday(new Date()));
    const [metadata, week] = await Promise.all([
      apiRequest("/schedule/editor/metadata"),
      apiRequest(`/schedule/editor/week?weekStart=${weekStart}`)
    ]);
    const lessons = week?.lessons ?? [];
    const emptyTopics = lessons.filter((lesson) => !lesson.topic || isPlaceholderTopic(lesson.topic)).length;
    return {
      metrics: [
        { label: "Уроков недели", value: lessons.length },
        { label: "Классов", value: metadata?.classes?.length ?? 0 },
        { label: "Предметов", value: metadata?.subjects?.length ?? 0 },
        { label: "Тем к уточнению", value: emptyTopics }
      ],
      sections: [
        {
          title: "Уроки с незаполненной темой",
          table: {
            columns: ["Дата", "Класс", "Предмет", "Учитель"],
            rows: lessons.filter((lesson) => !lesson.topic || isPlaceholderTopic(lesson.topic)).slice(0, 8).map((lesson) => [
              formatDate(lesson.date),
              lesson.className,
              lesson.subjectName,
              lesson.teacherName
            ])
          }
        }
      ]
    };
  }

  return {
    metrics: [],
    sections: []
  };
}

async function loadOnlineUsersSection() {
  const users = await apiRequest("/users");
  return buildOnlineUsersSection(users ?? []);
}

function buildOnlineUsersSection(users) {
  return {
    title: "Пользователи онлайн",
    table: {
      columns: ["ФИО", "Логин", "Роль", "Онлайн"],
      rows: users.filter((item) => item.isOnline).slice(0, 6).map((item) => [
        item.fullName || "Не указано",
        item.login,
        item.roleName,
        "Онлайн"
      ])
    }
  };
}

function updateDashboardSection(dashboard, nextSection) {
  if (!dashboard) {
    return dashboard;
  }

  const sections = dashboard.sections ?? [];
  return {
    ...dashboard,
    sections: sections.map((section) => (
      section.title === nextSection.title ? nextSection : section
    ))
  };
}

function DashboardOverview({ dashboard, role }) {
  if (!dashboard) {
    return null;
  }

  return (
    <>
      <div className="metric-grid dashboard-metrics">
        {(dashboard.metrics ?? []).map((item) => (
          <MetricCard key={item.label} label={item.label} value={item.value} />
        ))}
      </div>
      <div className="dashboard-section-grid">
        {(dashboard.sections ?? []).map((section) => (
          <DashboardSection key={section.title} section={section} role={role} />
        ))}
      </div>
    </>
  );
}

function DashboardSection({ section }) {
  return (
    <details className="report-section dashboard-section" open>
      <summary>
        <span>
          <strong>{section.title}</strong>
          {section.items && <small>{section.items.length} пункта</small>}
          {section.table && <small>{section.table.rows.length} строк</small>}
        </span>
        <b>Открыть</b>
      </summary>
      <div className="report-section-body">
        {section.items && (
          <div className="insight-list">
            {section.items.length === 0 ? (
              <p className="empty-text padded">Данных пока нет</p>
            ) : section.items.map((item, index) => (
              <article className="insight-item" key={`${section.title}-${index}`}>
                <span>{String(index + 1).padStart(2, "0")}</span>
                <strong>{item}</strong>
              </article>
            ))}
          </div>
        )}
        {section.table && (
          <DataTable
            title={section.title}
            columns={section.table.columns}
            rows={section.table.rows}
            className="nested-table dashboard-table"
          />
        )}
      </div>
    </details>
  );
}

function getDashboardIntro(role) {
  const introByRole = {
    "Администратор": "Оперативная сводка по учетным записям, структуре классов и данным, которые требуют внимания администратора.",
    "Директор": "Ключевые показатели учебного процесса: заполнение журнала, посещаемость, классы и работа учителей.",
    "Учитель": "Рабочая сводка преподавателя: ближайшие уроки, классы, предметы и задачи по журналу.",
    "Ученик": "Короткая учебная картина на день: расписание, оценки, домашние задания и посещаемость.",
    "Родитель": "Главные данные по ребенку: успеваемость, расписание, домашние задания и посещаемость.",
    "Менеджер расписания": "Контроль недельной сетки занятий, назначений и уроков с темами, которые нужно уточнить."
  };

  return introByRole[role] || "Быстрый доступ к основным разделам электронного журнала, доступным вашей учетной записи.";
}

function AdminPage({ role }) {
  const allowed = role === "Администратор";
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [students, setStudents] = useState([]);
  const [classes, setClasses] = useState([]);
  const [classTeacherAssignments, setClassTeacherAssignments] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState({
    login: "",
    fullName: "",
    password: "",
    roleId: ""
  });
  const [editingUser, setEditingUser] = useState(null);
  const [editingStudent, setEditingStudent] = useState(null);
  const [studentAccessTarget, setStudentAccessTarget] = useState(null);
  const [parentAccessTarget, setParentAccessTarget] = useState(null);
  const [parentAccessForm, setParentAccessForm] = useState({ fullName: "", login: "" });
  const [issuedAccess, setIssuedAccess] = useState(null);
  const [studentAccountLink, setStudentAccountLink] = useState({
    studentId: "",
    userId: ""
  });
  const [classTeacherForm, setClassTeacherForm] = useState({
    classId: "",
    teacherId: ""
  });
  const [classForm, setClassForm] = useState({ name: "" });
  const [editingClass, setEditingClass] = useState(null);
  const [classDeleteTarget, setClassDeleteTarget] = useState(null);
  const [classDeleteForm, setClassDeleteForm] = useState({
    studentAction: "keepWithoutClass",
    targetClassId: ""
  });
  const [userFilters, setUserFilters] = useState({
    search: "",
    roleId: "",
    status: "all",
    sort: "fullName"
  });
  const [studentFilters, setStudentFilters] = useState({
    search: "",
    classId: "",
    account: "all",
    sort: "name"
  });
  const [studentImportText, setStudentImportText] = useState("");
  const [studentImportFileName, setStudentImportFileName] = useState("");
  const [docxRosterFile, setDocxRosterFile] = useState(null);
  const [parentRosterFile, setParentRosterFile] = useState(null);
  const [issuedBatch, setIssuedBatch] = useState(null);
  const [adminTab, setAdminTab] = useState("overview");
  const [subjects, setSubjects] = useState([]);
  const [subjectForm, setSubjectForm] = useState({
    name: "",
    teacherId: "",
    classId: ""
  });
  const [editingSubject, setEditingSubject] = useState(null);
  const [subjectClassAssignmentForm, setSubjectClassAssignmentForm] = useState({
    subjectId: "",
    classId: "",
    teacherId: ""
  });
  const [subjectFilters, setSubjectFilters] = useState({
    search: "",
    teacherId: "",
    classId: "",
    sort: "name"
  });

  async function loadAdminData() {
    setLoading(true);
    setMessage("");
    try {
      const [usersData, rolesData, studentsData, classesData, assignmentsData, subjectsData] = await Promise.all([
        apiRequest("/users"),
        apiRequest("/roles"),
        apiRequest("/admin/students"),
        apiRequest("/classes"),
        apiRequest("/class-teacher/assignments"),
        apiRequest("/subjects")
      ]);
      setUsers(usersData ?? []);
      setRoles(rolesData ?? []);
      setStudents(studentsData ?? []);
      setClasses(sortItems(classesData ?? [], "name", { name: (item) => classSortValue(item.name) }));
      setClassTeacherAssignments(sortItems(assignmentsData ?? [], "className", {
        className: (item) => classSortValue(item.className),
        teacherName: (item) => item.teacherName || ""
      }));
      setSubjects(sortItems(subjectsData ?? [], "name", { name: (item) => item.name || "" }));
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

    if (!form.login || !form.fullName || !form.roleId) {
      setMessage("Заполните логин, ФИО и роль");
      return;
    }

    try {
      const temporaryPassword = form.password || generateTemporaryPassword();
      const created = await apiRequest("/users", {
        method: "POST",
        body: JSON.stringify({
          login: form.login.trim(),
          fullName: form.fullName.trim(),
          password: temporaryPassword,
          roleId: Number(form.roleId)
        })
      });
      setForm({ login: "", fullName: "", password: "", roleId: "" });
      setIssuedAccess({
        id: created.id,
        login: created.login,
        fullName: created.fullName,
        temporaryPassword,
        mustChangePassword: created.mustChangePassword,
        message: "Пользователь создан"
      });
      setMessage("Пользователь создан, временный пароль готов к передаче");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать пользователя");
    }
  }

  async function saveUser(event) {
    event.preventDefault();
    if (!editingUser) {
      return;
    }

    try {
      await apiRequest(`/users/${editingUser.id}`, {
        method: "PUT",
        body: JSON.stringify({
          login: editingUser.login.trim(),
          fullName: editingUser.fullName.trim(),
          password: editingUser.password?.trim() || null,
          roleId: Number(editingUser.roleId),
          isActive: Boolean(editingUser.isActive)
        })
      });
      setEditingUser(null);
      setMessage("Пользователь обновлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось обновить пользователя");
    }
  }

  async function saveStudent(event) {
    event.preventDefault();
    if (!editingStudent) {
      return;
    }

    try {
      await apiRequest(`/admin/students/${editingStudent.studentId}`, {
        method: "PUT",
        body: JSON.stringify({
          firstName: editingStudent.firstName.trim(),
          lastName: editingStudent.lastName.trim(),
          birthDate: editingStudent.birthDate,
          classId: Number(editingStudent.classId)
        })
      });
      setEditingStudent(null);
      setMessage("Карточка ученика обновлена");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось обновить ученика");
    }
  }

  async function resetUserPassword(user) {
    try {
      const access = await apiRequest(`/users/${user.id}/reset-password`, { method: "POST" });
      setIssuedAccess(access);
      setMessage("Временный пароль создан");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать временный пароль");
    }
  }

  useEffect(() => {
    if (!message) {
      return undefined;
    }

    const timer = window.setTimeout(() => setMessage(""), 6000);
    return () => window.clearTimeout(timer);
  }, [message]);

  useEffect(() => {
    setMessage("");
  }, [adminTab]);

  async function activateUser(user) {
    try {
      await apiRequest(`/users/${user.id}`, {
        method: "PUT",
        body: JSON.stringify({ isActive: true })
      });
      setMessage("Пользователь активирован");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось активировать пользователя");
    }
  }

  async function deleteUser(user) {
    if (!window.confirm(`Удалить пользователя ${user.fullName || user.login}?`)) {
      return;
    }

    try {
      await apiRequest(`/users/${user.id}`, { method: "DELETE" });
      setMessage("Пользователь удален");
      await loadAdminData();
    } catch (error) {
      const messageText = error.message || "Не удалось удалить пользователя";
      if (messageText.includes("привязаны предметы") || messageText.includes("привязаны уроки")) {
        const confirmed = window.confirm(`${messageText}\n\nУдалить пользователя вместе с его предметами, уроками, оценками и посещаемостью?`);
        if (!confirmed) {
          setMessage(messageText);
          return;
        }

        try {
          await apiRequest(`/users/${user.id}`, {
            method: "DELETE",
            body: JSON.stringify({ deleteLinkedSubjects: true })
          });
          setMessage("Пользователь и связанные учебные данные удалены");
          await loadAdminData();
        } catch (cascadeError) {
          setMessage(cascadeError.message || "Не удалось удалить пользователя со связанными данными");
        }
        return;
      }

      setMessage(messageText);
    }
  }

  async function issueStudentAccess(event) {
    event.preventDefault();
    if (!studentAccessTarget) {
      return;
    }

    try {
      const access = await apiRequest(`/admin/students/${studentAccessTarget.studentId}/issue-account`, {
        method: "POST",
        body: JSON.stringify({ login: "" })
      });
      setStudentAccessTarget(null);
      setIssuedAccess(access);
      setMessage("Доступ ученика подготовлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось подготовить доступ ученика");
    }
  }

  async function issueParentAccess(event) {
    event.preventDefault();
    if (!parentAccessTarget || !parentAccessForm.fullName.trim()) {
      setMessage("Укажите ФИО родителя");
      return;
    }

    try {
      const access = await apiRequest(`/admin/students/${parentAccessTarget.studentId}/issue-parent-account`, {
        method: "POST",
        body: JSON.stringify({
          fullName: parentAccessForm.fullName.trim(),
          login: parentAccessForm.login.trim() || null
        })
      });
      setParentAccessTarget(null);
      setParentAccessForm({ fullName: "", login: "" });
      setIssuedAccess(access);
      setMessage("Доступ родителя подготовлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось подготовить доступ родителя");
    }
  }

  async function copyAccessText(access) {
    const text = `Логин: ${access.login}\nВременный пароль: ${access.temporaryPassword}\nПри первом входе система попросит задать постоянный пароль.`;
    try {
      await copyTextToClipboard(text);
      setMessage("Данные доступа скопированы");
    } catch {
      setMessage("Не удалось скопировать автоматически. Выделите логин и пароль в окне и скопируйте вручную.");
    }
  }

  async function copyBatchAccess(items) {
    const text = items
      .map((item) => `${item.fullName}\nЛогин: ${item.login}\nВременный пароль: ${item.temporaryPassword}\n`)
      .join("\n");
    try {
      await copyTextToClipboard(text);
      setMessage("Список доступов скопирован");
    } catch {
      setMessage("Не удалось скопировать список автоматически. Выделите данные в окне и скопируйте вручную.");
    }
  }

  async function attachStudentAccount(event) {
    event.preventDefault();
    if (!studentAccountLink.studentId || !studentAccountLink.userId) {
      setMessage("Выберите ученика и учетную запись");
      return;
    }

    try {
      await apiRequest(`/admin/students/${studentAccountLink.studentId}/attach-account`, {
        method: "POST",
        body: JSON.stringify({ userId: Number(studentAccountLink.userId) })
      });
      setStudentAccountLink({ studentId: "", userId: "" });
      setMessage("Учетная запись привязана к ученику");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось привязать учетную запись");
    }
  }

  async function importStudents(event) {
    event.preventDefault();
    if (!studentImportText.trim()) {
      setMessage("Добавьте CSV-файл или вставьте строки для импорта");
      return;
    }

    try {
      const result = await apiRequest("/admin/students/import", {
        method: "POST",
        body: JSON.stringify({
          csvText: studentImportText,
          createMissingClasses: true
        })
      });
      setStudentImportText("");
      setStudentImportFileName("");
      setMessage(`Импорт завершен: добавлено ${result.imported}, пропущено ${result.skipped}${result.errors?.length ? `. Ошибки: ${result.errors.slice(0, 3).join("; ")}` : ""}`);
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось импортировать учеников");
    }
  }

  async function readStudentImportFile(file) {
    if (!file) {
      return;
    }

    const text = await file.text();
    setStudentImportFileName(file.name);
    setStudentImportText(text);
  }

  async function exportStudents() {
    try {
      const response = await fetch(`${apiBase}/admin/students/export`, {
        credentials: "include"
      });

      if (!response.ok) {
        throw new Error("Не удалось выгрузить учеников");
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = "students.csv";
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage(error.message || "Не удалось выгрузить учеников");
    }
  }

  async function assignClassTeacher(event) {
    event.preventDefault();
    if (!classTeacherForm.classId || !classTeacherForm.teacherId) {
      setMessage("Выберите класс и учителя");
      return;
    }

    try {
      const assignments = await apiRequest("/class-teacher/assignments", {
        method: "POST",
        body: JSON.stringify({
          classId: Number(classTeacherForm.classId),
          teacherId: Number(classTeacherForm.teacherId)
        })
      });
      setClassTeacherAssignments(sortItems(assignments ?? [], "className", {
        className: (item) => classSortValue(item.className),
        teacherName: (item) => item.teacherName || ""
      }));
      setMessage("Классный руководитель назначен");
    } catch (error) {
      setMessage(error.message || "Не удалось назначить классного руководителя");
    }
  }

  async function createAdminClass(event) {
    event.preventDefault();
    if (!classForm.name.trim()) {
      setMessage("Укажите название класса");
      return;
    }

    try {
      await apiRequest("/classes", {
        method: "POST",
        body: JSON.stringify({ name: classForm.name.trim() })
      });
      setClassForm({ name: "" });
      setMessage("Класс добавлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось добавить класс");
    }
  }

  async function updateAdminClass(event) {
    event.preventDefault();
    if (!editingClass || !editingClass.name.trim()) {
      setMessage("Укажите название класса");
      return;
    }

    try {
      await apiRequest(`/classes/${editingClass.classId}`, {
        method: "PUT",
        body: JSON.stringify({ name: editingClass.name.trim() })
      });
      setEditingClass(null);
      setMessage("Класс обновлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось обновить класс");
    }
  }

  function openClassDeleteDialog(classItem) {
    setClassDeleteTarget(classItem);
    setClassDeleteForm({
      studentAction: "keepWithoutClass",
      targetClassId: ""
    });
  }

  async function deleteAdminClass(event) {
    event.preventDefault();
    if (!classDeleteTarget) {
      return;
    }

    if (classDeleteForm.studentAction === "moveStudents" && !classDeleteForm.targetClassId) {
      setMessage("Выберите класс для перевода учеников");
      return;
    }

    try {
      await apiRequest(`/classes/${classDeleteTarget.classId}`, {
        method: "DELETE",
        body: JSON.stringify({
          studentAction: classDeleteForm.studentAction,
          targetClassId: classDeleteForm.studentAction === "moveStudents"
            ? Number(classDeleteForm.targetClassId)
            : null
        })
      });
      setClassDeleteTarget(null);
      setMessage("Класс удален");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось удалить класс");
    }
  }

  async function removeClassTeacher(classId, teacherId) {
    try {
      const assignments = await apiRequest(`/class-teacher/assignments/${classId}/${teacherId}`, {
        method: "DELETE"
      });
      setClassTeacherAssignments(sortItems(assignments ?? [], "className", {
        className: (item) => classSortValue(item.className),
        teacherName: (item) => item.teacherName || ""
      }));
      setMessage("Назначение классного руководителя удалено");
    } catch (error) {
      setMessage(error.message || "Не удалось удалить назначение");
    }
  }

  async function createSubject(event) {
    event.preventDefault();
    if (!subjectForm.name.trim() || !subjectForm.teacherId || !subjectForm.classId) {
      setMessage("Укажите название предмета, учителя и класс");
      return;
    }

    try {
      await apiRequest("/subjects", {
        method: "POST",
        body: JSON.stringify({
          name: subjectForm.name.trim(),
          teacherId: Number(subjectForm.teacherId),
          classId: Number(subjectForm.classId)
        })
      });
      setSubjectForm({ name: "", teacherId: "", classId: "" });
      setMessage("Предмет создан");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать предмет");
    }
  }

  async function updateSubject(event) {
    event.preventDefault();
    if (!editingSubject || !editingSubject.name.trim()) {
      setMessage("Укажите название предмета");
      return;
    }

    try {
      await apiRequest(`/subjects/${editingSubject.subjectId}`, {
        method: "PUT",
        body: JSON.stringify({
          name: editingSubject.name.trim()
        })
      });
      setEditingSubject(null);
      setMessage("Предмет обновлен");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось обновить предмет");
    }
  }

  async function deleteSubject(subjectId) {
    if (!window.confirm("Удалить предмет? Если к нему есть уроки, система предложит удалить их отдельно.")) {
      return;
    }

    try {
      await apiRequest(`/subjects/${subjectId}`, {
        method: "DELETE"
      });
      setMessage("Предмет удален");
      await loadAdminData();
    } catch (error) {
      const messageText = error.message || "Не удалось удалить предмет";
      if (messageText.includes("привязаны уроки")) {
        const confirmed = window.confirm(`${messageText}\n\nУдалить предмет вместе с уроками, оценками и посещаемостью?`);
        if (!confirmed) {
          setMessage(messageText);
          return;
        }

        try {
          await apiRequest(`/subjects/${subjectId}`, {
            method: "DELETE",
            body: JSON.stringify({ deleteLessons: true })
          });
          setMessage("Предмет и связанные уроки удалены");
          await loadAdminData();
        } catch (cascadeError) {
          setMessage(cascadeError.message || "Не удалось удалить предмет со связанными уроками");
        }
        return;
      }

      setMessage(messageText);
    }
  }

  function openSubjectEditor(subject) {
    setEditingSubject(subject);
    window.requestAnimationFrame(() => {
      document.getElementById("subject-inline-editor")?.scrollIntoView({
        behavior: "smooth",
        block: "center"
      });
    });
  }

  async function assignSubjectToClass(event) {
    event.preventDefault();
    if (!subjectClassAssignmentForm.subjectId || !subjectClassAssignmentForm.classId || !subjectClassAssignmentForm.teacherId) {
      setMessage("Выберите предмет, класс и учителя");
      return;
    }

    try {
      await apiRequest(`/subjects/${subjectClassAssignmentForm.subjectId}/classes`, {
        method: "POST",
        body: JSON.stringify({
          classId: Number(subjectClassAssignmentForm.classId),
          teacherId: Number(subjectClassAssignmentForm.teacherId)
        })
      });
      setSubjectClassAssignmentForm({ subjectId: "", classId: "", teacherId: "" });
      setMessage("Назначение предмета добавлено");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось назначить предмет классу");
    }
  }

  async function removeSubjectAssignment(subjectId, classId, teacherId) {
    try {
      await apiRequest(`/subjects/${subjectId}/classes/${classId}/teachers/${teacherId}`, {
        method: "DELETE"
      });
      setMessage("Назначение предмета снято");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось снять назначение предмета");
    }
  }

  async function exportRosterDocx() {
    try {
      const response = await fetch(`${apiBase}/admin/students/export-docx`, {
        credentials: "include"
      });

      if (!response.ok) {
        throw new Error("Не удалось выгрузить Word-документ");
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = "school-roster.docx";
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage(error.message || "Не удалось выгрузить Word-документ");
    }
  }

  async function importRosterDocx(event) {
    event.preventDefault();
    if (!docxRosterFile) {
      setMessage("Выберите Word-документ со списком");
      return;
    }

    try {
      const data = new FormData();
      data.append("file", docxRosterFile);
      const response = await fetch(`${apiBase}/admin/students/import-docx`, {
        method: "POST",
        credentials: "include",
        headers: await csrfHeaders(),
        body: data
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        throw new Error(payload?.error || "Не удалось импортировать Word-документ");
      }

      const result = await response.json();
      setDocxRosterFile(null);
      setMessage(`Импорт Word завершен: учеников добавлено ${result.imported}, учителей создано ${result.teachersCreated}, связей руководителей ${result.classTeacherLinksCreated}`);
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось импортировать Word-документ");
    }
  }

  async function importParentsDocx(event) {
    event.preventDefault();
    if (!parentRosterFile) {
      setMessage("Выберите Word-документ с родителями");
      return;
    }

    try {
      const data = new FormData();
      data.append("file", parentRosterFile);
      const response = await fetch(`${apiBase}/admin/students/import-parents-docx`, {
        method: "POST",
        credentials: "include",
        headers: await csrfHeaders(),
        body: data
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        throw new Error(payload?.message || payload?.error || "Не удалось импортировать родителей");
      }

      const result = await response.json();
      const createdAccesses = (result.parents ?? []).filter((item) => item.created && item.temporaryPassword);
      setParentRosterFile(null);
      setIssuedBatch(createdAccesses.length > 0 ? createdAccesses : null);
      setMessage(`Импорт родителей завершен: создано ${result.parentsCreated}, найдено ${result.parentsFound}, привязок ${result.linksCreated}${result.errors?.length ? `. Не найдено: ${result.errors.length}` : ""}`);
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось импортировать родителей");
    }
  }

  if (!allowed) {
    return <AccessWarning title="Администрирование доступно только администратору" />;
  }

  const studentRole = roles.find((item) => item.name === "Ученик");
  const teacherRole = roles.find((item) => item.name === "Учитель");
  const sortedClasses = sortItems(classes, "name", { name: (item) => classSortValue(item.name) });
  const teacherUsers = sortItems(users.filter((item) => teacherRole
    ? Number(item.roleId) === Number(teacherRole.id)
    : item.roleName === "Учитель"), "fullName", { fullName: (item) => item.fullName || "" });
  const availableStudentUsers = users.filter((item) => {
    const isStudentRole = studentRole ? Number(item.roleId) === Number(studentRole.id) : item.roleName === "Ученик";
    const alreadyLinked = students.some((student) => Number(student.userId) === Number(item.id));
    return isStudentRole && !alreadyLinked;
  });
  const filteredUsers = sortItems(users.filter((item) => {
    const search = userFilters.search.trim().toLowerCase();
    const matchesSearch = !search
      || item.fullName?.toLowerCase().includes(search)
      || item.login?.toLowerCase().includes(search)
      || item.roleName?.toLowerCase().includes(search);
    const matchesRole = !userFilters.roleId || Number(item.roleId) === Number(userFilters.roleId);
    const matchesStatus = userFilters.status === "all"
      || (userFilters.status === "active" && item.isActive)
      || (userFilters.status === "blocked" && !item.isActive)
      || (userFilters.status === "temporary" && item.mustChangePassword);

    return matchesSearch && matchesRole && matchesStatus;
  }), userFilters.sort, {
    fullName: (item) => item.fullName || "",
    login: (item) => item.login || "",
    role: (item) => item.roleName || ""
  });
  const filteredStudents = sortItems(students.filter((item) => {
    const search = studentFilters.search.trim().toLowerCase();
    const fullName = `${item.lastName} ${item.firstName}`.toLowerCase();
    const matchesSearch = !search
      || fullName.includes(search)
      || item.className?.toLowerCase().includes(search);
    const matchesClass = !studentFilters.classId || Number(item.classId) === Number(studentFilters.classId);
    const matchesAccount = studentFilters.account === "all"
      || (studentFilters.account === "issued" && item.hasAccount)
      || (studentFilters.account === "missing" && !item.hasAccount);

    return matchesSearch && matchesClass && matchesAccount;
  }), studentFilters.sort, {
    name: (item) => `${item.lastName} ${item.firstName}`,
    className: (item) => classSortValue(item.className),
    account: (item) => item.hasAccount ? "1" : "0"
  });
  const filteredSubjects = sortItems(subjects.filter((item) => {
    const search = subjectFilters.search.trim().toLowerCase();
    const assignments = item.classAssignments ?? [];
    const classNames = assignments.map((assignment) => assignment.className).join(" ").toLowerCase();
    const teacherNames = assignments.map((assignment) => assignment.teacherName).join(" ").toLowerCase();
    const matchesSearch = !search
      || item.name?.toLowerCase().includes(search)
      || classNames.includes(search)
      || teacherNames.includes(search);
    const matchesTeacher = !subjectFilters.teacherId
      || assignments.some((assignment) => Number(assignment.teacherId) === Number(subjectFilters.teacherId));
    const matchesClass = !subjectFilters.classId
      || assignments.some((assignment) => Number(assignment.classId) === Number(subjectFilters.classId));

    return matchesSearch && matchesTeacher && matchesClass;
  }), subjectFilters.sort, {
    name: (item) => item.name || "",
    teacher: (item) => (item.classAssignments ?? []).map((assignment) => assignment.teacherName).join("|"),
    classes: (item) => (item.classAssignments ?? []).map((assignment) => classSortValue(assignment.className)).join("|")
  });
  const classStudentCounts = students.reduce((acc, student) => {
    if (student.classId) {
      acc.set(Number(student.classId), (acc.get(Number(student.classId)) ?? 0) + 1);
    }
    return acc;
  }, new Map());
  const classTeacherByClass = classTeacherAssignments.reduce((acc, assignment) => {
    acc.set(Number(assignment.classId), assignment);
    return acc;
  }, new Map());
  const usersWithoutActiveAccount = users.filter((item) => !item.isActive).length;
  const missingStudentAccounts = students.filter((item) => !item.hasAccount).length;
  const adminTabs = [
    { id: "overview", label: "Обзор" },
    { id: "users", label: "Пользователи" },
    { id: "students", label: "Ученики" },
    { id: "subjects", label: "Предметы" },
    { id: "access", label: "Доступы и классы" },
    { id: "import", label: "Импорт" }
  ];

  return (
    <section className="page-stack admin-page">
      <PageHeader
        title="Администрирование"
        subtitle="Пользователи, роли и ученики"
        text="Управление учетными записями, ролями, карточками учеников и выдачей доступа к электронному журналу."
      />
      <StatusLine loading={loading} message={message} />
      <label className="admin-tab-select field">
        <span>Раздел администрирования</span>
        <select value={adminTab} onChange={(event) => setAdminTab(event.target.value)}>
          {adminTabs.map((tab) => (
            <option key={tab.id} value={tab.id}>{tab.label}</option>
          ))}
        </select>
      </label>
      <div className="admin-tabs" role="tablist" aria-label="Разделы администрирования">
        {adminTabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={adminTab === tab.id ? "active" : ""}
            onClick={() => setAdminTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div className={`metric-grid admin-section ${adminTab === "overview" ? "active" : ""}`}>
        <MetricCard label="Пользователей" value={users.length} />
        <MetricCard label="Активных" value={users.filter((item) => item.isActive).length} />
        <MetricCard label="Нужно сменить пароль" value={users.filter((item) => item.mustChangePassword).length} />
        <MetricCard label="Учеников" value={students.length} />
        <MetricCard label="Без аккаунта" value={missingStudentAccounts} />
        <MetricCard label="Отключенных" value={usersWithoutActiveAccount} />
        <MetricCard label="Классов" value={classes.length} />
        <MetricCard label="Классных руководителей" value={classTeacherAssignments.length} />
      </div>
      <form className={`inline-form admin-section ${adminTab === "users" ? "active" : ""}`} onSubmit={createUser}>
        <Field label="Логин" value={form.login} onChange={(value) => setForm({ ...form, login: value })} />
        <Field label="ФИО" value={form.fullName} onChange={(value) => setForm({ ...form, fullName: value })} />
        <Field label="Временный пароль, можно оставить пустым" value={form.password} onChange={(value) => setForm({ ...form, password: value })} />
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
      {editingUser && adminTab === "users" && (
        <Modal title="Редактирование пользователя" onClose={() => setEditingUser(null)}>
        <form className="modal-form" onSubmit={saveUser}>
          <Field label="Логин" value={editingUser.login} onChange={(value) => setEditingUser({ ...editingUser, login: value })} />
          <Field label="ФИО" value={editingUser.fullName} onChange={(value) => setEditingUser({ ...editingUser, fullName: value })} />
          <Field label="Новый пароль" value={editingUser.password || ""} onChange={(value) => setEditingUser({ ...editingUser, password: value })} />
          <label className="field">
            <span>Роль</span>
            <select value={editingUser.roleId} onChange={(event) => setEditingUser({ ...editingUser, roleId: event.target.value })}>
              {roles.map((item) => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="toggle-field">
            <input type="checkbox" checked={editingUser.isActive} onChange={(event) => setEditingUser({ ...editingUser, isActive: event.target.checked })} />
            Активен
          </label>
          <button className="primary-button">Сохранить пользователя</button>
          <button className="ghost-button compact" type="button" onClick={() => setEditingUser(null)}>Отмена</button>
        </form>
        </Modal>
      )}
      {editingStudent && adminTab === "students" && (
        <Modal title="Редактирование ученика" onClose={() => setEditingStudent(null)}>
        <form className="modal-form" onSubmit={saveStudent}>
          <Field label="Фамилия" value={editingStudent.lastName} onChange={(value) => setEditingStudent({ ...editingStudent, lastName: value })} />
          <Field label="Имя" value={editingStudent.firstName} onChange={(value) => setEditingStudent({ ...editingStudent, firstName: value })} />
          <Field label="Дата рождения" type="date" value={editingStudent.birthDate?.slice(0, 10)} onChange={(value) => setEditingStudent({ ...editingStudent, birthDate: value })} />
          <label className="field">
            <span>Класс</span>
            <select value={editingStudent.classId} onChange={(event) => setEditingStudent({ ...editingStudent, classId: event.target.value })}>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <button className="primary-button">Сохранить ученика</button>
          <button className="ghost-button compact" type="button" onClick={() => setEditingStudent(null)}>Отмена</button>
        </form>
        </Modal>
      )}
      {studentAccessTarget && (
        <Modal title="Выдача доступа ученику" onClose={() => setStudentAccessTarget(null)}>
          <form className="modal-form" onSubmit={issueStudentAccess}>
            <p className="modal-hint">
              Система сама создаст логин, временный пароль и привяжет учетную запись к ученику:
              {" "}<strong>{studentAccessTarget.lastName} {studentAccessTarget.firstName}</strong>.
            </p>
            <button className="primary-button">Подготовить доступ</button>
            <button className="ghost-button compact" type="button" onClick={() => setStudentAccessTarget(null)}>Отмена</button>
          </form>
        </Modal>
      )}
      {parentAccessTarget && (
        <Modal title="Выдача доступа родителю" onClose={() => setParentAccessTarget(null)}>
          <form className="modal-form" onSubmit={issueParentAccess}>
            <p className="modal-hint">
              Родитель будет привязан только к ученику: <strong>{parentAccessTarget.lastName} {parentAccessTarget.firstName}</strong>.
            </p>
            <Field label="ФИО родителя" value={parentAccessForm.fullName} onChange={(value) => setParentAccessForm({ ...parentAccessForm, fullName: value })} />
            <Field label="Логин, если нужен свой" value={parentAccessForm.login} onChange={(value) => setParentAccessForm({ ...parentAccessForm, login: value })} />
            <button className="primary-button">Подготовить доступ родителю</button>
            <button className="ghost-button compact" type="button" onClick={() => setParentAccessTarget(null)}>Отмена</button>
          </form>
        </Modal>
      )}
      {issuedAccess && (
        <Modal title="Данные для входа" onClose={() => setIssuedAccess(null)}>
          <div className="access-card">
            <p>Передайте эти данные адресату. Пароль показывается только после выдачи или сброса.</p>
            <div><span>ФИО</span><strong>{issuedAccess.fullName}</strong></div>
            <div><span>Логин</span><strong>{issuedAccess.login}</strong></div>
            <div><span>Временный пароль</span><strong>{issuedAccess.temporaryPassword}</strong></div>
            <button className="primary-button" type="button" onClick={() => copyAccessText(issuedAccess)}>Скопировать для отправки</button>
          </div>
        </Modal>
      )}
      {issuedBatch && (
        <Modal title="Доступы родителей из документа" onClose={() => setIssuedBatch(null)}>
          <div className="access-card">
            <p>Созданные временные пароли показываются только сейчас. Скопируйте список и передайте родителям безопасным способом.</p>
            <button className="primary-button" type="button" onClick={() => copyBatchAccess(issuedBatch)}>Скопировать весь список</button>
            <div className="access-list">
              {issuedBatch.map((item) => (
                <article key={item.id}>
                  <strong>{item.fullName}</strong>
                  <span>Логин: {item.login}</span>
                  <span>Пароль: {item.temporaryPassword}</span>
                  <small>{item.linkedStudents?.join(", ") || "ученик привязан"}</small>
                </article>
              ))}
            </div>
          </div>
        </Modal>
      )}
      <section className={`table-card admin-section ${adminTab === "subjects" ? "active" : ""}`}>
        <div className="table-title">Предметы и учителя</div>
        <form className="inline-form attach-form" onSubmit={assignSubjectToClass}>
          <label className="field">
            <span>Существующий предмет</span>
            <select value={subjectClassAssignmentForm.subjectId} onChange={(event) => setSubjectClassAssignmentForm({ ...subjectClassAssignmentForm, subjectId: event.target.value })}>
              <option value="">Выберите предмет</option>
              {subjects.map((item) => (
                <option key={item.subjectId} value={item.subjectId}>{getAdminSubjectLabel(item)}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Преподаватель для класса</span>
            <select value={subjectClassAssignmentForm.teacherId} onChange={(event) => setSubjectClassAssignmentForm({ ...subjectClassAssignmentForm, teacherId: event.target.value })}>
              <option value="">Выберите учителя</option>
              {teacherUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Новый класс для предмета</span>
            <select value={subjectClassAssignmentForm.classId} onChange={(event) => setSubjectClassAssignmentForm({ ...subjectClassAssignmentForm, classId: event.target.value })}>
              <option value="">Выберите класс</option>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <button className="primary-button">Добавить класс</button>
        </form>
        <p className="modal-hint inline-editor-hint">Если предмет уже есть, используйте верхнюю форму: она добавит только новую связку предмет + класс + преподаватель. Кнопка «Снять» ниже удаляет только конкретную связку, а «Удалить предмет целиком» удаляет сам предмет и отдельно спросит подтверждение, если есть уроки.</p>
        <form className="inline-form attach-form" onSubmit={createSubject}>
          <label className="field">
            <span>Новый предмет</span>
            <input type="text" value={subjectForm.name} onChange={(event) => setSubjectForm({ ...subjectForm, name: event.target.value })} />
          </label>
          <label className="field">
            <span>Первый преподаватель</span>
            <select value={subjectForm.teacherId} onChange={(event) => setSubjectForm({ ...subjectForm, teacherId: event.target.value })}>
              <option value="">Выберите учителя</option>
              {teacherUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Первый класс</span>
            <select value={subjectForm.classId} onChange={(event) => setSubjectForm({ ...subjectForm, classId: event.target.value })}>
              <option value="">Выберите класс</option>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <button className="ghost-button">Создать новый предмет</button>
        </form>
        <div className="filter-panel">
          <Field label="Поиск" value={subjectFilters.search} onChange={(value) => setSubjectFilters({ ...subjectFilters, search: value })} />
          <label className="field">
            <span>Учитель</span>
            <select value={subjectFilters.teacherId} onChange={(event) => setSubjectFilters({ ...subjectFilters, teacherId: event.target.value })}>
              <option value="">Все учителя</option>
              {teacherUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Класс</span>
            <select value={subjectFilters.classId} onChange={(event) => setSubjectFilters({ ...subjectFilters, classId: event.target.value })}>
              <option value="">Все классы</option>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Сортировка</span>
            <select value={subjectFilters.sort} onChange={(event) => setSubjectFilters({ ...subjectFilters, sort: event.target.value })}>
              <option value="name">По предмету</option>
              <option value="teacher">По учителю</option>
              <option value="classes">По классу</option>
            </select>
          </label>
        </div>
        <DataTable
          title={`Предметы (${filteredSubjects.length})`}
          className="nested-table"
          columns={["Предмет", "Назначения", "Действие"]}
          rows={filteredSubjects.map((item) => [
            item.name,
            (item.classAssignments?.length ? (
              <div className="row-actions" key={`assignments-${item.subjectId}`}>
                {item.classAssignments.map((assignment) => (
                  <button
                    className="table-action"
                    key={`${assignment.classId}-${assignment.teacherId}`}
                    type="button"
                    onClick={() => removeSubjectAssignment(item.subjectId, assignment.classId, assignment.teacherId)}
                    title="Снять только это назначение"
                  >
                    Снять: {assignment.className} · {assignment.teacherName}
                  </button>
                ))}
              </div>
            ) : "Нет назначенных классов"),
            <div key={item.subjectId} style={{ display: "flex", gap: "8px" }}>
              <button className="table-action" type="button" onClick={() => openSubjectEditor(item)}>Изменить</button>
              <button className="table-action danger-action" type="button" onClick={() => deleteSubject(item.subjectId)}>Удалить предмет целиком</button>
            </div>
          ])}
        />
      </section>
      {editingSubject && adminTab === "subjects" && (
        <section className="table-card inline-editor-card" id="subject-inline-editor">
          <div className="table-title">Редактирование предмета</div>
          <form className="inline-form attach-form" onSubmit={updateSubject}>
            <Field label="Название предмета" value={editingSubject.name} onChange={(value) => setEditingSubject({ ...editingSubject, name: value })} />
            <button className="primary-button">Сохранить</button>
            <button className="ghost-button compact" type="button" onClick={() => setEditingSubject(null)}>Отмена</button>
          </form>
          <p className="modal-hint inline-editor-hint">Преподаватели назначаются только по конкретным классам в списке выше. Здесь меняется только название предмета.</p>
        </section>
      )}
      {classDeleteTarget && adminTab === "access" && (
        <Modal title={`Удаление класса ${classDeleteTarget.name}`} onClose={() => setClassDeleteTarget(null)}>
          <form className="modal-form" onSubmit={deleteAdminClass}>
            <p className="modal-hint">Выберите, что сделать с учениками этого класса перед удалением.</p>
            <label className="field">
              <span>Действие с учениками</span>
              <select value={classDeleteForm.studentAction} onChange={(event) => setClassDeleteForm({
                ...classDeleteForm,
                studentAction: event.target.value,
                targetClassId: event.target.value === "moveStudents" ? classDeleteForm.targetClassId : ""
              })}>
                <option value="keepWithoutClass">Оставить без класса</option>
                <option value="moveStudents">Перевести в другой класс</option>
                <option value="deleteStudents">Удалить вместе с классом</option>
              </select>
            </label>
            {classDeleteForm.studentAction === "moveStudents" && (
              <label className="field">
                <span>Класс для перевода</span>
                <select value={classDeleteForm.targetClassId} onChange={(event) => setClassDeleteForm({ ...classDeleteForm, targetClassId: event.target.value })}>
                  <option value="">Выберите класс</option>
                  {sortedClasses
                    .filter((item) => Number(item.classId) !== Number(classDeleteTarget.classId))
                    .map((item) => (
                      <option key={item.classId} value={item.classId}>{item.name}</option>
                    ))}
                </select>
              </label>
            )}
            {classDeleteForm.studentAction === "deleteStudents" && (
              <p className="modal-hint danger-hint">Ученики класса будут удалены вместе с их оценками, посещаемостью и привязками родителей.</p>
            )}
            <button className="danger-button">Удалить класс</button>
            <button className="ghost-button compact" type="button" onClick={() => setClassDeleteTarget(null)}>Отмена</button>
          </form>
        </Modal>
      )}
      {editingClass && adminTab === "access" && (
        <Modal title={`Изменение класса ${editingClass.name}`} onClose={() => setEditingClass(null)}>
          <form className="modal-form" onSubmit={updateAdminClass}>
            <Field label="Название класса" value={editingClass.name} onChange={(value) => setEditingClass({ ...editingClass, name: value })} />
            <button className="primary-button">Сохранить класс</button>
            <button className="ghost-button compact" type="button" onClick={() => setEditingClass(null)}>Отмена</button>
          </form>
        </Modal>
      )}
      <section className={`table-card admin-section ${adminTab === "access" ? "active" : ""}`}>
        <div className="table-title">Привязка готового аккаунта ученика</div>
        <form className="inline-form attach-form" onSubmit={attachStudentAccount}>
          <label className="field">
            <span>Карточка ученика без аккаунта</span>
            <select value={studentAccountLink.studentId} onChange={(event) => setStudentAccountLink({ ...studentAccountLink, studentId: event.target.value })}>
              <option value="">Выберите ученика</option>
              {students.filter((item) => !item.hasAccount).map((item) => (
                <option key={item.studentId} value={item.studentId}>{item.lastName} {item.firstName} · {item.className || "без класса"}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Свободная учетная запись ученика</span>
            <select value={studentAccountLink.userId} onChange={(event) => setStudentAccountLink({ ...studentAccountLink, userId: event.target.value })}>
              <option value="">Выберите пользователя</option>
              {availableStudentUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
              ))}
            </select>
          </label>
          <button className="primary-button">Привязать профиль</button>
        </form>
      </section>
      <section className={`table-card admin-section ${adminTab === "access" ? "active" : ""}`}>
        <div className="table-title">Классы</div>
        <form className="inline-form attach-form" onSubmit={createAdminClass}>
          <Field label="Новый класс" value={classForm.name} onChange={(value) => setClassForm({ name: value })} />
          <button className="primary-button">Добавить класс</button>
        </form>
        <DataTable
          title={`Классы (${sortedClasses.length})`}
          className="nested-table"
          columns={["Класс", "Ученики", "Классный руководитель", "Действие"]}
          rows={sortedClasses.map((item) => {
            const classTeacher = classTeacherByClass.get(Number(item.classId));
            return [
              item.name,
              classStudentCounts.get(Number(item.classId)) ?? 0,
              classTeacher?.teacherName ?? "Не назначен",
              <div className="row-actions">
                <button className="table-action" type="button" onClick={() => setEditingClass(item)}>Изменить</button>
                <button className="table-action" type="button" onClick={() => openClassDeleteDialog(item)}>Удалить</button>
              </div>
            ];
          })}
        />
      </section>
      <section className={`table-card admin-section ${adminTab === "access" ? "active" : ""}`}>
        <div className="table-title">Классные руководители</div>
        <form className="inline-form attach-form" onSubmit={assignClassTeacher}>
          <label className="field">
            <span>Класс</span>
            <select value={classTeacherForm.classId} onChange={(event) => setClassTeacherForm({ ...classTeacherForm, classId: event.target.value })}>
              <option value="">Выберите класс</option>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Учитель</span>
            <select value={classTeacherForm.teacherId} onChange={(event) => setClassTeacherForm({ ...classTeacherForm, teacherId: event.target.value })}>
              <option value="">Выберите учителя</option>
              {teacherUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
              ))}
            </select>
          </label>
          <button className="primary-button">Назначить / переназначить</button>
        </form>
        <DataTable
          title={`Назначения (${classTeacherAssignments.length})`}
          className="nested-table"
          columns={["Класс", "Классный руководитель", "Действие"]}
          rows={classTeacherAssignments.map((item) => [
            item.className,
            item.teacherName,
            <button className="table-action" type="button" onClick={() => removeClassTeacher(item.classId, item.teacherId)}>Снять</button>
          ])}
        />
      </section>
      <section className={`table-card import-card admin-section ${adminTab === "import" ? "active" : ""}`}>
        <div className="table-title">Импорт и экспорт учеников</div>
        <form className="import-panel" onSubmit={importRosterDocx}>
          <label className="file-drop">
            <input type="file" accept=".docx" onChange={(event) => setDocxRosterFile(event.target.files?.[0] ?? null)} />
            <strong>{docxRosterFile?.name || "Выберите Word-документ"}</strong>
            <span>Система достанет учеников, учителей и классных руководителей из таблиц документа.</span>
          </label>
          <button className="primary-button compact">Импортировать Word-документ</button>
        </form>
        <form className="import-panel" onSubmit={importParentsDocx}>
          <label className="file-drop">
            <input type="file" accept=".docx" onChange={(event) => setParentRosterFile(event.target.files?.[0] ?? null)} />
            <strong>{parentRosterFile?.name || "Выберите Word-документ с родителями"}</strong>
            <span>Система найдет учеников по классу и дате рождения, создаст родительские доступы и привяжет их к детям.</span>
          </label>
          <button className="primary-button compact">Импортировать родителей</button>
        </form>
        <form className="import-panel" onSubmit={importStudents}>
          <label className="file-drop">
            <input type="file" accept=".csv,.txt" onChange={(event) => readStudentImportFile(event.target.files?.[0])} />
            <strong>{studentImportFileName || "Выберите CSV-файл"}</strong>
            <span>Формат: Фамилия;Имя;Дата рождения;Класс</span>
          </label>
          <label className="field">
            <span>Или вставьте строки вручную</span>
            <textarea
              value={studentImportText}
              onChange={(event) => setStudentImportText(event.target.value)}
              placeholder={"Фамилия;Имя;Дата рождения;Класс\nИванов;Артем;12.04.2012;7Г"}
            />
          </label>
          <div className="button-row">
            <button className="primary-button compact">Импортировать</button>
            <button className="ghost-button compact" type="button" onClick={exportStudents}>Выгрузить CSV</button>
            <button className="ghost-button compact" type="button" onClick={exportRosterDocx}>Выгрузить Word</button>
          </div>
        </form>
      </section>
      <section className={`table-card admin-section ${adminTab === "users" ? "active" : ""}`}>
        <div className="table-title">Фильтр пользователей</div>
        <div className="filter-panel">
          <Field label="Поиск" value={userFilters.search} onChange={(value) => setUserFilters({ ...userFilters, search: value })} />
          <label className="field">
            <span>Роль</span>
            <select value={userFilters.roleId} onChange={(event) => setUserFilters({ ...userFilters, roleId: event.target.value })}>
              <option value="">Все роли</option>
              {roles.map((item) => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Статус</span>
            <select value={userFilters.status} onChange={(event) => setUserFilters({ ...userFilters, status: event.target.value })}>
              <option value="all">Все</option>
              <option value="active">Активные</option>
              <option value="blocked">Отключенные</option>
              <option value="temporary">С временным паролем</option>
            </select>
          </label>
          <label className="field">
            <span>Сортировка</span>
            <select value={userFilters.sort} onChange={(event) => setUserFilters({ ...userFilters, sort: event.target.value })}>
              <option value="fullName">По ФИО</option>
              <option value="login">По логину</option>
              <option value="role">По роли</option>
            </select>
          </label>
        </div>
      </section>
      <DataTable
        title={`Пользователи (${filteredUsers.length})`}
        className={`admin-section ${adminTab === "users" ? "active" : ""}`}
        columns={["ФИО", "Логин", "Роль", "Статус", "Пароль", "Действие"]}
        rows={filteredUsers.map((item) => [
          item.fullName,
          item.login,
          item.roleName,
          item.isActive ? "Активен" : "Отключен",
          item.mustChangePassword ? "Нужно сменить" : "Постоянный",
          <div className="row-actions">
            <button className="table-action" type="button" onClick={() => setEditingUser({ ...item, password: "" })}>Редактировать</button>
            {!item.isActive && <button className="table-action" type="button" onClick={() => activateUser(item)}>Активировать</button>}
            <button className="table-action" type="button" onClick={() => resetUserPassword(item)}>Временный пароль</button>
            <button className="table-action danger-action" type="button" onClick={() => deleteUser(item)}>Удалить</button>
          </div>
        ])}
      />
      <section className={`table-card admin-section ${adminTab === "students" ? "active" : ""}`}>
        <div className="table-title">Фильтр учеников</div>
        <div className="filter-panel">
          <Field label="Поиск" value={studentFilters.search} onChange={(value) => setStudentFilters({ ...studentFilters, search: value })} />
          <label className="field">
            <span>Класс</span>
            <select value={studentFilters.classId} onChange={(event) => setStudentFilters({ ...studentFilters, classId: event.target.value })}>
              <option value="">Все классы</option>
              {sortedClasses.map((item) => (
                <option key={item.classId} value={item.classId}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Аккаунт</span>
            <select value={studentFilters.account} onChange={(event) => setStudentFilters({ ...studentFilters, account: event.target.value })}>
              <option value="all">Все</option>
              <option value="issued">Выдан</option>
              <option value="missing">Не выдан</option>
            </select>
          </label>
          <label className="field">
            <span>Сортировка</span>
            <select value={studentFilters.sort} onChange={(event) => setStudentFilters({ ...studentFilters, sort: event.target.value })}>
              <option value="name">По ФИО</option>
              <option value="className">По классу</option>
              <option value="account">По аккаунту</option>
            </select>
          </label>
        </div>
      </section>
      <DataTable
        title={`Ученики (${filteredStudents.length})`}
        className={`admin-section ${adminTab === "students" ? "active" : ""}`}
        columns={["ФИО", "Класс", "Аккаунт", "Действие"]}
        rows={filteredStudents.map((item) => [
          `${item.lastName} ${item.firstName}`,
          item.className || "Без класса",
          item.hasAccount ? "Есть" : "Не выдан",
          <div className="row-actions">
            <button className="table-action" type="button" onClick={() => setEditingStudent({ ...item, birthDate: item.birthDate?.slice(0, 10) })}>Редактировать</button>
            <button className="table-action" type="button" onClick={() => setStudentAccessTarget(item)}>{item.hasAccount ? "Сбросить доступ" : "Выдать доступ"}</button>
            <button className="table-action" type="button" onClick={() => setParentAccessTarget(item)}>Доступ родителю</button>
          </div>
        ])}
      />
    </section>
  );
}

function DirectorPage({ role }) {
  const allowed = role === "Директор" || role === "Администратор";
  const [periodMode, setPeriodMode] = useState("week");
  const [period, setPeriod] = useState(() => getDirectorPeriod("week"));
  const [dailyDate, setDailyDate] = useState(() => getDirectorPeriod("week").end);
  const [summary, setSummary] = useState(null);
  const [attendance, setAttendance] = useState(null);
  const [daily, setDaily] = useState(null);
  const [teachers, setTeachers] = useState(null);
  const [classTeachers, setClassTeachers] = useState(null);
  const [problematic, setProblematic] = useState(null);
  const [directorTab, setDirectorTab] = useState("overview");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  async function loadReports() {
    setLoading(true);
    setMessage("");
    const query = `startDate=${period.start}&endDate=${period.end}`;
    try {
      const [classSummary, attendanceStats, dailyReport, teacherSummary, classTeacherSummary, problematicStudents] = await Promise.all([
        apiRequest(`/director/report/class-summary?${query}`),
        apiRequest(`/director/report/attendance?${query}`),
        apiRequest(`/director/report/daily?date=${dailyDate}`),
        apiRequest(`/director/report/teachers?${query}`),
        apiRequest(`/director/report/class-teachers?${query}`),
        apiRequest(`/director/report/problematic?${query}`)
      ]);
      setSummary(classSummary);
      setAttendance(attendanceStats);
      setDaily(dailyReport);
      setTeachers(teacherSummary);
      setClassTeachers(classTeacherSummary);
      setProblematic(problematicStudents);
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
  }, [allowed, period.start, period.end, dailyDate]);

  if (!allowed) {
    return <AccessWarning title="Отчеты доступны директору и администратору" />;
  }

  const classRows = sortItems(summary?.classSummary ?? [], "className", { className: (item) => classSortValue(item.className) });
  const attendanceRows = sortItems(attendance?.statistics ?? [], "className", { className: (item) => classSortValue(item.className) });
  const dailyRows = daily?.report ?? [];
  const teacherRows = teachers?.teachers ?? [];
  const classTeacherRows = classTeachers?.classTeachers ?? [];
  const problemRows = problematic?.problematicStudents ?? [];
  const averageAttendance = attendanceRows.length
    ? attendanceRows.reduce((sum, item) => sum + Number(item.presentPercentage ?? 0), 0) / attendanceRows.length
    : 0;
  const averageClassGrade = calculateAverage(classRows.map((item) => item.averageGrade).filter((value) => Number(value) > 0));
  const lowGradeTotal = problemRows.reduce((sum, item) => sum + Number(item.lowGrades ?? 0), 0);
  const absenceTotal = attendanceRows.reduce((sum, item) => sum + Number(item.absent ?? 0), 0);

  function applyMode(mode) {
    const nextPeriod = getDirectorPeriod(mode);
    setPeriodMode(mode);
    setPeriod(nextPeriod);
    setDailyDate(nextPeriod.end);
  }

  return (
    <section className="page-stack">
      <PageHeader
        title="Панель директора"
        subtitle={`${formatDate(period.start)} - ${formatDate(period.end)}`}
        text="Контроль успеваемости, посещаемости, заполнения журналов и учеников, которым нужно внимание."
      />
      <StatusLine loading={loading} message={message} />
      <div className="director-filter-panel">
        <div className="period-buttons">
          <button className={periodMode === "day" ? "active" : ""} type="button" onClick={() => applyMode("day")}>День</button>
          <button className={periodMode === "week" ? "active" : ""} type="button" onClick={() => applyMode("week")}>Неделя</button>
          <button className={periodMode === "month" ? "active" : ""} type="button" onClick={() => applyMode("month")}>Месяц</button>
        </div>
        <Field label="Начало" type="date" value={period.start} onChange={(value) => {
            setPeriodMode("custom");
            setPeriod({ ...period, start: value });
          }} />
        <Field label="Конец" type="date" value={period.end} onChange={(value) => {
            setPeriodMode("custom");
            setPeriod({ ...period, end: value });
          }} />
        <Field label="День контроля" type="date" value={dailyDate} onChange={setDailyDate} />
        <a className="ghost-button compact" href="#/teacher">Открыть журналы</a>
        <a className="ghost-button compact" href="#/schedule">Открыть расписание</a>
      </div>
      <div className="metric-grid">
        <MetricCard label="Уроков в контрольный день" value={daily?.totalLessons ?? 0} />
        <MetricCard label="Средняя посещаемость" value={`${formatNumber(averageAttendance)}%`} />
        <MetricCard label="Средняя оценка" value={formatNumber(averageClassGrade)} />
        <MetricCard label="Учителей с уроками" value={teacherRows.length} />
        <MetricCard label="Ученики в зоне внимания" value={problemRows.length} />
        <MetricCard label="Низких оценок" value={lowGradeTotal} />
        <MetricCard label="Пропусков за период" value={absenceTotal} />
        <MetricCard label="Уроков без оценок" value={(daily?.report ?? []).filter((item) => Number(item.gradesPercentage ?? 0) === 0).length} />
      </div>
      <div className="director-tabs" role="tablist" aria-label="Разделы панели директора">
        {[
          ["overview", "Обзор"],
          ["classes", "Классы"],
          ["students", "Ученики"],
          ["teachers", "Учителя"],
          ["mentors", "Классные руководители"],
          ["daily", "Контроль дня"]
        ].map(([key, label]) => (
          <button className={directorTab === key ? "active" : ""} key={key} type="button" onClick={() => setDirectorTab(key)}>{label}</button>
        ))}
      </div>
      {directorTab === "overview" && (
        <div className="director-chart-grid">
          <BarChartCard
            title="Средняя оценка по классам"
            items={classRows.filter((item) => Number(item.averageGrade) > 0).map((item) => ({
              label: item.className,
              value: Number(item.averageGrade),
              max: 5,
              suffix: ""
            }))}
          />
          <BarChartCard
            title="Посещаемость по классам"
            items={attendanceRows.map((item) => ({
              label: item.className,
              value: Number(item.presentPercentage ?? 0),
              max: 100,
              suffix: "%"
            }))}
          />
          <BarChartCard
            title="Ученики с двойками"
            items={problemRows.filter((item) => Number(item.lowGrades) > 0).slice(0, 10).map((item) => ({
              label: `${item.lastName} ${item.firstName} (${item.class})`,
              value: Number(item.lowGrades ?? 0),
              max: Math.max(5, ...problemRows.map((row) => Number(row.lowGrades ?? 0))),
              suffix: " дв.",
              format: "int"
            }))}
          />
          <BarChartCard
            title="Пропуски по ученикам"
            items={problemRows.filter((item) => Number(item.absences) > 0).slice(0, 10).map((item) => ({
              label: `${item.lastName} ${item.firstName} (${item.class})`,
              value: Number(item.absences ?? 0),
              max: Math.max(5, ...problemRows.map((row) => Number(row.absences ?? 0))),
              suffix: " Н",
              format: "int"
            }))}
          />
        </div>
      )}
      {directorTab === "classes" && (
        <SmartDataTable
          title="Классы: оценки и посещаемость"
          rows={classRows}
          columns={[
            { key: "className", label: "Класс", sortValue: (item) => classSortValue(item.className) },
            { key: "studentCount", label: "Учеников", type: "number" },
            { key: "averageAbsences", label: "Н-пропусков на ученика", type: "number", render: (item) => formatNumber(item.averageAbsences) },
            { key: "averageGrade", label: "Средняя оценка", type: "number", render: (item) => formatNumber(item.averageGrade) }
          ]}
        />
      )}
      {directorTab === "students" && (
        <SmartDataTable
          title="Ученики в зоне внимания"
          rows={problemRows}
          columns={[
            { key: "student", label: "Ученик", getValue: (item) => `${item.lastName} ${item.firstName}` },
            { key: "class", label: "Класс", sortValue: (item) => classSortValue(item.class) },
            { key: "absences", label: "Н-пропуски", type: "number" },
            { key: "lowGrades", label: "Двойки", type: "number" },
            { key: "averageGrade", label: "Средняя", type: "number", render: (item) => formatNumber(item.averageGrade) },
            { key: "totalGrades", label: "Всего оценок", type: "number" }
          ]}
        />
      )}
      {directorTab === "teachers" && (
        <div className="page-stack">
          <BarChartCard
            title="Темы и ДЗ у учителей"
            items={teacherRows.slice(0, 12).map((item) => ({
              label: item.teacher,
              value: Math.min(Number(item.topicsCompletionPercentage ?? 0), Number(item.homeworkCompletionPercentage ?? 0)),
              max: 100,
              suffix: "%"
            }))}
          />
          <SmartDataTable
            title="Учителя: заполнение журналов"
            rows={teacherRows}
            columns={[
            { key: "teacher", label: "Учитель" },
            { key: "lessonsCount", label: "Уроков", type: "number" },
            { key: "topicsCompletionPercentage", label: "Темы заполнены", type: "number", render: (item) => `${formatNumber(item.topicsCompletionPercentage)}%` },
            { key: "homeworkCompletionPercentage", label: "ДЗ заполнено", type: "number", render: (item) => `${formatNumber(item.homeworkCompletionPercentage)}%` },
            { key: "gradesCompletionPercentage", label: "Уроки с оценками", type: "number", render: (item) => `${formatNumber(item.gradesCompletionPercentage)}%` },
            { key: "attendanceProblems", label: "Проблем посещаемости", type: "number" }
            ]}
          />
        </div>
      )}
      {directorTab === "mentors" && (
        <SmartDataTable
          title="Классные руководители"
          rows={classTeacherRows}
          columns={[
            { key: "className", label: "Класс", sortValue: (item) => classSortValue(item.className) },
            { key: "teacher", label: "Классный руководитель" },
            { key: "studentsCount", label: "Учеников", type: "number" },
            { key: "absences", label: "Н-пропуски", type: "number" },
            { key: "lowGrades", label: "Двойки", type: "number" },
            { key: "averageGrade", label: "Средняя", type: "number", render: (item) => formatNumber(item.averageGrade) }
          ]}
        />
      )}
      {directorTab === "daily" && (
        <SmartDataTable
          title={`Контроль дня: ${formatDate(dailyDate)}`}
          rows={dailyRows}
          columns={[
            { key: "name", label: "Урок" },
            { key: "teacher", label: "Учитель" },
            { key: "class", label: "Класс", sortValue: (item) => classSortValue(item.class) },
            { key: "gradesPercentage", label: "Оценки внесены", type: "number", render: (item) => `${formatNumber(item.gradesPercentage)}%` },
            { key: "attendancePercentage", label: "Посещаемость", type: "number", render: (item) => `${formatNumber(item.attendancePercentage)}%` }
          ]}
        />
      )}
    </section>
  );
}

function ReportSection({ title, subtitle, defaultOpen = false, children }) {
  return (
    <details className="report-section" open={defaultOpen}>
      <summary>
        <span>
          <strong>{title}</strong>
          {subtitle && <small>{subtitle}</small>}
        </span>
        <b>Открыть</b>
      </summary>
      <div className="report-section-body">{children}</div>
    </details>
  );
}

function BarChartCard({ title, items }) {
  const safeItems = (items ?? []).filter((item) => Number.isFinite(Number(item.value)));

  return (
    <section className="table-card chart-card">
      <div className="table-title">{title}</div>
      <div className="bar-chart-list">
        {safeItems.length === 0 ? (
          <p className="empty-text padded">Данных для графика пока нет</p>
        ) : safeItems.map((item, index) => {
          const max = Number(item.max || 100);
          const value = Number(item.value || 0);
          const width = max > 0 ? Math.max(2, Math.min(100, value / max * 100)) : 0;
          const displayValue = item.format === "int" ? String(Math.round(value)) : formatNumber(value);
          return (
            <div className="bar-chart-row" key={`${item.label}-${index}`}>
              <div className="bar-chart-label">
                <span>{item.label}</span>
                <strong>{displayValue}{item.suffix ?? ""}</strong>
              </div>
              <div className="bar-track" aria-hidden="true">
                <div className="bar-fill" style={{ width: `${width}%` }} />
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function TeacherPage({ role, user }) {
  const allowed = role === "Учитель" || role === "Администратор" || role === "Директор";
  const readOnly = role === "Директор";
  const [classes, setClasses] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [lessons, setLessons] = useState([]);
  const [students, setStudents] = useState([]);
  const [gradesByLesson, setGradesByLesson] = useState({});
  const [attendanceByLesson, setAttendanceByLesson] = useState({});
  const [selectedClassId, setSelectedClassId] = useState("");
  const [selectedSubjectId, setSelectedSubjectId] = useState("");
  const [selectedLessonId, setSelectedLessonId] = useState("");
  const [journalDateFrom, setJournalDateFrom] = useState("");
  const [journalDateTo, setJournalDateTo] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [lessonForm, setLessonForm] = useState({
    subjectId: "",
    classId: "",
    topic: "",
    date: new Date().toISOString().slice(0, 10),
    homework: ""
  });

  async function loadTeacherData() {
    setLoading(true);
    setMessage("");
    try {
      const adminMode = role === "Администратор" || role === "Директор";
      const [classData, subjectData, lessonData] = role === "Директор"
        ? await Promise.all([
          apiRequest("/schedule/editor/metadata"),
          apiRequest("/lessons")
        ]).then(([metadataData, lessonRows]) => [metadataData?.classes ?? [], metadataData?.subjects ?? [], lessonRows])
        : await Promise.all([
          apiRequest(adminMode ? "/classes" : `/teacher/classes?teacherId=${user.id}`),
          apiRequest(adminMode ? "/subjects" : `/teacher/subjects?teacherId=${user.id}`),
          apiRequest(adminMode ? "/lessons" : `/teacher/lessons?teacherId=${user.id}`)
        ]);
      setClasses(sortItems(classData ?? [], "name", { name: (item) => classSortValue(item.name) }));
      setSubjects(adminMode
        ? (subjectData ?? []).map((subject) => ({
          ...subject,
          classIds: subject.classAssignments
            ? subject.classAssignments.map((assignment) => assignment.classId)
            : subject.classIds ?? [],
          classes: subject.classAssignments
            ? subject.classAssignments.map((assignment) => assignment.className).join(", ")
            : subject.classes ?? ""
        }))
        : subjectData ?? []);
      setLessons(lessonData ?? []);
    } catch (error) {
      setMessage(error.message || "Не удалось загрузить кабинет учителя");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (allowed) {
      loadTeacherData();
    }
  }, [allowed, role, user.id]);

  useEffect(() => {
    if (!selectedClassId) {
      setStudents([]);
      return;
    }

    apiRequest(`/teacher/classes/${selectedClassId}/students`)
      .then((data) => setStudents(data ?? []))
      .catch((error) => setMessage(error.message || "Не удалось загрузить учеников класса"));
  }, [selectedClassId]);

  useEffect(() => {
    if (!selectedClassId || !selectedSubjectId || lessons.length === 0) {
      return;
    }

    const selectedClassForLoad = classes.find((item) => String(item.classId ?? item.id) === String(selectedClassId));
    const lessonsToLoad = lessons
      .filter((lesson) => String(lesson.classId ?? "") === String(selectedClassId) || lesson.className === selectedClassForLoad?.name)
      .filter((lesson) => String(lesson.subjectId ?? "") === String(selectedSubjectId))
      .filter((lesson) => !selectedLessonId || String(lesson.lessonId) === String(selectedLessonId))
      .filter((lesson) => !journalDateFrom || new Date(lesson.date) >= new Date(journalDateFrom))
      .filter((lesson) => !journalDateTo || new Date(lesson.date) <= new Date(journalDateTo));

    if (lessonsToLoad.length === 0) {
      return;
    }

    const missingLessons = lessonsToLoad.filter((lesson) => !gradesByLesson[lesson.lessonId] || !attendanceByLesson[lesson.lessonId]);
    if (missingLessons.length === 0) {
      return;
    }

    setLoading(true);
    Promise.all(missingLessons.map(async (lesson) => {
      const [gradeData, attendanceData] = await Promise.all([
        apiRequest(`/teacher/grades/${lesson.lessonId}`),
        apiRequest(`/teacher/attendance/${lesson.lessonId}`)
      ]);
      return { lessonId: lesson.lessonId, grades: gradeData ?? [], attendance: attendanceData ?? [] };
    }))
      .then((items) => {
        setGradesByLesson((current) => ({
          ...current,
          ...Object.fromEntries(items.map((item) => [item.lessonId, item.grades]))
        }));
        setAttendanceByLesson((current) => ({
          ...current,
          ...Object.fromEntries(items.map((item) => [item.lessonId, item.attendance]))
        }));
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить журнал класса"))
      .finally(() => setLoading(false));
  }, [selectedClassId, selectedSubjectId, selectedLessonId, journalDateFrom, journalDateTo, lessons, classes]);

  async function loadLessonMarks(lessonId) {
    if (!lessonId) {
      setSelectedLessonId("");
      return;
    }

    setSelectedLessonId(lessonId);
    setLoading(true);
    setMessage("");
    try {
      const [gradeData, attendanceData] = await Promise.all([
        apiRequest(`/teacher/grades/${lessonId}`),
        apiRequest(`/teacher/attendance/${lessonId}`)
      ]);
      setGradesByLesson((current) => ({ ...current, [lessonId]: gradeData ?? [] }));
      setAttendanceByLesson((current) => ({ ...current, [lessonId]: attendanceData ?? [] }));
    } catch (error) {
      setMessage(error.message || "Не удалось загрузить журнал урока");
    } finally {
      setLoading(false);
    }
  }

  async function createLesson(event) {
    event.preventDefault();
    setMessage("");

    if (!lessonForm.subjectId || !lessonForm.classId || !lessonForm.topic || !lessonForm.date) {
      setMessage("Заполните предмет, класс, тему и дату урока");
      return;
    }

    try {
      await apiRequest("/teacher/lessons", {
        method: "POST",
        body: JSON.stringify({
          subjectId: Number(lessonForm.subjectId),
          classId: Number(lessonForm.classId),
          topic: lessonForm.topic.trim(),
          date: lessonForm.date,
          homework: lessonForm.homework.trim()
        })
      });
      setLessonForm({
        subjectId: "",
        classId: "",
        topic: "",
        date: new Date().toISOString().slice(0, 10),
        homework: ""
      });
      setMessage("Урок создан");
      await loadTeacherData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать урок");
    }
  }

  async function saveGrade(studentId, value) {
    if (!selectedLessonId || !value) {
      return;
    }

    await saveGradeForLesson(selectedLessonId, studentId, value);
  }

  async function saveGradeForLesson(lessonId, studentId, value) {
    if (!lessonId || !value) {
      return;
    }

    try {
      const savedGrade = await apiRequest("/teacher/grades", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(lessonId),
          studentId,
          value: Number(value)
        })
      });
      setGradesByLesson((current) => ({
        ...current,
        [lessonId]: [...(current[lessonId] ?? []), savedGrade]
      }));
      setMessage("");
    } catch (error) {
      setMessage(error.message || "Не удалось сохранить оценку");
    }
  }

  async function deleteGrade(gradeId, lessonId = selectedLessonId) {
    try {
      await apiRequest(`/teacher/grades/${gradeId}`, { method: "DELETE" });
      if (lessonId) {
        setGradesByLesson((current) => ({
          ...current,
          [lessonId]: (current[lessonId] ?? []).filter((grade) => String(grade.gradeId) !== String(gradeId))
        }));
      }
    } catch (error) {
      setMessage(error.message || "Не удалось удалить оценку");
    }
  }

  async function saveAttendance(studentId, status) {
    if (!selectedLessonId) {
      return;
    }

    await saveAttendanceForLesson(selectedLessonId, studentId, status);
  }

  async function saveAttendanceForLesson(lessonId, studentId, status) {
    if (!lessonId || status === "") {
      return;
    }

    const normalizedStatus = status === "present" ? 1 : Number(status);

    try {
      await apiRequest("/teacher/attendance", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(lessonId),
          studentId,
          status: normalizedStatus
        })
      });
      await loadLessonMarks(lessonId);
    } catch (error) {
      setMessage(error.message || "Не удалось сохранить посещаемость");
    }
  }

  if (!allowed) {
    return <AccessWarning title="Кабинет учителя доступен учителю и администратору" />;
  }

  const selectedClass = classes.find((item) => String(item.classId ?? item.id) === String(selectedClassId));
  const classLessons = lessons.filter((lesson) => {
    if (!selectedClassId) {
      return true;
    }

    return String(lesson.classId ?? "") === String(selectedClassId)
      || lesson.className === selectedClass?.name;
  });
  const classSubjectIds = new Set(classLessons.map((lesson) => String(lesson.subjectId ?? "")));
  const availableJournalSubjects = subjects.filter((subject) => {
    const subjectId = String(subject.subjectId ?? subject.id ?? "");
    const assignedClassIds = subject.classIds ?? [];
    return !selectedClassId
      || assignedClassIds.some((classId) => String(classId) === String(selectedClassId))
      || classSubjectIds.has(subjectId);
  });
  const journalLessons = selectedClassId ? classLessons
    .filter((lesson) => !selectedSubjectId || String(lesson.subjectId ?? "") === String(selectedSubjectId))
    .filter((lesson) => !selectedLessonId || String(lesson.lessonId) === String(selectedLessonId))
    .filter((lesson) => !journalDateFrom || new Date(lesson.date) >= new Date(journalDateFrom))
    .filter((lesson) => !journalDateTo || new Date(lesson.date) <= new Date(journalDateTo))
    .sort((a, b) => new Date(a.date) - new Date(b.date)) : [];
  const allVisibleGrades = journalLessons.flatMap((lesson) => gradesByLesson[lesson.lessonId] ?? []);
  const allVisibleAttendance = journalLessons.flatMap((lesson) => attendanceByLesson[lesson.lessonId] ?? []);
  const classAverage = calculateAverage(allVisibleGrades.map((item) => item.value));
  const absentCount = allVisibleAttendance.filter((item) => Number(item.status) === 0).length;
  const lateCount = allVisibleAttendance.filter((item) => Number(item.status) === 2).length;
  const presentCount = students.length && journalLessons.length
    ? (students.length * journalLessons.length) - absentCount
    : 0;
  const recentLessons = (selectedClassId ? classLessons : lessons)
    .slice()
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .slice(0, 8);

  return (
    <section className="page-stack">
      <PageHeader
        title={readOnly ? "Журналы классов" : "Кабинет учителя"}
        subtitle={readOnly ? "Просмотр оценок и посещаемости" : "Уроки, журнал, оценки и посещаемость"}
        text={readOnly
          ? "Директорский режим просмотра журналов классов, оценок и посещаемости."
          : "Рабочее место преподавателя для планирования уроков, заполнения оценок и отметок посещаемости."}
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Уроков" value={lessons.length} />
        <MetricCard label="Учеников в классе" value={students.length} />
        <MetricCard label="Средняя оценка" value={formatNumber(classAverage)} />
        <MetricCard label="Отметок присутствия" value={presentCount} />
        <MetricCard label="Неявок" value={absentCount} />
        <MetricCard label="Опозданий" value={lateCount} />
      </div>
      {!readOnly && <form className="inline-form lesson-form" onSubmit={createLesson}>
        <label className="field">
          <span>Предмет</span>
          <select value={lessonForm.subjectId} onChange={(event) => setLessonForm({ ...lessonForm, subjectId: event.target.value })}>
            <option value="">Выберите предмет</option>
            {subjects.map((item) => (
              <option key={item.subjectId ?? item.id} value={item.subjectId ?? item.id}>{item.name}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Класс</span>
          <select value={lessonForm.classId} onChange={(event) => setLessonForm({ ...lessonForm, classId: event.target.value })}>
            <option value="">Выберите класс</option>
            {classes.map((item) => (
              <option key={item.classId ?? item.id} value={item.classId ?? item.id}>{item.name}</option>
            ))}
          </select>
        </label>
        <Field label="Тема" value={lessonForm.topic} onChange={(value) => setLessonForm({ ...lessonForm, topic: value })} />
        <Field label="Дата" type="date" value={lessonForm.date} onChange={(value) => setLessonForm({ ...lessonForm, date: value })} />
        <Field label="Домашнее задание" value={lessonForm.homework} onChange={(value) => setLessonForm({ ...lessonForm, homework: value })} />
        <button className="primary-button">Создать урок</button>
      </form>}
      <div className="split-grid">
        <section className="table-card">
          <div className="table-title">Выбор класса и урока</div>
          <div className="picker-panel">
            <label className="field">
              <span>Класс</span>
              <select value={selectedClassId} onChange={(event) => {
                setSelectedClassId(event.target.value);
                setSelectedSubjectId("");
                setSelectedLessonId("");
              }}>
                <option value="">Все классы</option>
                {classes.map((item) => (
                  <option key={item.classId ?? item.id} value={item.classId ?? item.id}>{item.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Предмет</span>
              <select value={selectedSubjectId} onChange={(event) => {
                setSelectedSubjectId(event.target.value);
                setSelectedLessonId("");
              }} disabled={!selectedClassId}>
                <option value="">Выберите предмет</option>
                {availableJournalSubjects.map((subject) => (
                  <option key={subject.subjectId ?? subject.id} value={subject.subjectId ?? subject.id}>{subject.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Урок</span>
              <select value={selectedLessonId} onChange={(event) => loadLessonMarks(event.target.value)}>
                <option value="">Все уроки предмета</option>
                {classLessons
                  .filter((lesson) => !selectedSubjectId || String(lesson.subjectId ?? "") === String(selectedSubjectId))
                  .map((lesson) => (
                  <option key={lesson.lessonId} value={lesson.lessonId}>
                    {lesson.subjectName} · {formatLessonTopic(lesson.topic)} · {formatDate(lesson.date)}
                  </option>
                ))}
              </select>
            </label>
            <Field label="От" type="date" value={journalDateFrom} onChange={setJournalDateFrom} />
            <Field label="До" type="date" value={journalDateTo} onChange={setJournalDateTo} />
            <button className="ghost-button compact" type="button" onClick={() => {
              setJournalDateFrom("");
              setJournalDateTo("");
              setSelectedLessonId("");
            }}>Сбросить фильтр</button>
          </div>
        </section>
        <DataTable
          title={selectedClassId ? `Последние уроки: ${selectedClass?.name ?? "выбранный класс"}` : "Последние уроки"}
          columns={["Дата", "Предмет", "Класс", "Тема", "ДЗ"]}
          rows={recentLessons.map((lesson) => [
            formatDate(lesson.date),
            lesson.subjectName,
            lesson.className,
            formatLessonTopic(lesson.topic),
            lesson.homework || "—"
          ])}
        />
      </div>
      <section className="table-card">
        <div className="table-title">Журнал класса</div>
        {!selectedClassId ? (
          <p className="empty-text padded">Выберите класс, чтобы увидеть сетку журнала.</p>
        ) : !selectedSubjectId ? (
          <p className="empty-text padded">Выберите предмет, чтобы открыть журнал класса.</p>
        ) : students.length === 0 || journalLessons.length === 0 ? (
          <p className="empty-text padded">Нет уроков или учеников по выбранным фильтрам.</p>
        ) : (
          <div className="gradebook-wrap">
            <table className="gradebook-table">
              <thead>
                <tr>
                  <th className="student-sticky">ФИО ученика</th>
                  {journalLessons.map((lesson, index) => (
                    <th key={lesson.lessonId}>
                      <span>{formatDate(lesson.date)}</span>
                      <small>{formatLessonTopic(lesson.topic)}</small>
                    </th>
                  ))}
                  <th>Средняя</th>
                  <th>Медиана</th>
                </tr>
              </thead>
              <tbody>
                {students.map((student) => {
                  const studentGrades = journalLessons.flatMap((lesson) =>
                    (gradesByLesson[lesson.lessonId] ?? []).filter((grade) => grade.studentId === student.studentId)
                  );
                  return (
                    <tr key={student.studentId}>
                      <td className="student-sticky">{student.lastName} {student.firstName}</td>
                      {journalLessons.map((lesson) => {
                        const grades = (gradesByLesson[lesson.lessonId] ?? []).filter((grade) => grade.studentId === student.studentId);
                        const attendance = (attendanceByLesson[lesson.lessonId] ?? []).find((item) => item.studentId === student.studentId);
                        const attendanceStatus = attendance?.status ?? 1;
                        const gradeCellClass = grades.some((grade) => Number(grade.value) === 2) ? "gradebook-cell-low-grade" : "";
                        return (
                          <td key={`${student.studentId}-${lesson.lessonId}`} className={`gradebook-cell ${attendanceClassName(attendanceStatus)} ${gradeCellClass}`}>
                            <div className="grade-stack">
                              {grades.map((grade) => (
                                <button
                                  className={`grade-pill grade-${grade.value}`}
                                  key={grade.gradeId}
                                  title={readOnly ? "Оценка" : "Удалить оценку"}
                                  type="button"
                                  onClick={() => !readOnly && deleteGrade(grade.gradeId, lesson.lessonId)}
                                >
                                  {grade.value}
                                </button>
                              ))}
                              {!readOnly && <select defaultValue="" onChange={(event) => {
                                const gradeValue = event.target.value;
                                event.target.value = "";
                                saveGradeForLesson(lesson.lessonId, student.studentId, gradeValue);
                              }}>
                                <option value="">+</option>
                                {[2, 3, 4, 5].map((value) => (
                                  <option key={value} value={value}>{value}</option>
                                ))}
                              </select>}
                            </div>
                            <select className="attendance-select" value={String(attendanceStatus)} disabled={readOnly} onChange={(event) => saveAttendanceForLesson(lesson.lessonId, student.studentId, event.target.value)}>
                              <option value="1">Присутствует</option>
                              <option value="2">Опоздал</option>
                              <option value="0">Не явился</option>
                            </select>
                          </td>
                        );
                      })}
                      <td>{studentGrades.length ? formatNumber(calculateAverage(studentGrades.map((grade) => grade.value))) : "—"}</td>
                      <td>{studentGrades.length ? formatNumber(calculateMedian(studentGrades.map((grade) => grade.value))) : "—"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  );
}

function StudentPage({ role, view = "full" }) {
  const allowed = role === "Ученик" || role === "Администратор";
  const adminMode = role === "Администратор";
  const [students, setStudents] = useState([]);
  const [selectedStudentId, setSelectedStudentId] = useState("");
  const [info, setInfo] = useState(null);
  const [schedule, setSchedule] = useState([]);
  const [grades, setGrades] = useState([]);
  const [homework, setHomework] = useState([]);
  const [attendance, setAttendance] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!allowed) {
      return;
    }

    if (adminMode) {
      setLoading(true);
      apiRequest("/admin/students")
        .then((data) => {
          const list = data ?? [];
          setStudents(list);
          if (!selectedStudentId && list.length > 0) {
            setSelectedStudentId(String(list[0].studentId));
          }
        })
        .catch((error) => setMessage(error.message || "Не удалось загрузить список учеников"))
        .finally(() => setLoading(false));
      return;
    }

    setLoading(true);
    Promise.all([
      apiRequest("/student/me/class"),
      apiRequest("/student/me/schedule"),
      apiRequest("/student/me/grades"),
      apiRequest("/student/me/homework"),
      apiRequest("/student/me/attendance")
    ])
      .then(([infoData, scheduleData, gradeData, homeworkData, attendanceData]) => {
        setInfo(infoData);
        setSchedule(scheduleData ?? []);
        setGrades(gradeData ?? []);
        setHomework(homeworkData ?? []);
        setAttendance(attendanceData ?? []);
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить кабинет ученика"))
      .finally(() => setLoading(false));
  }, [allowed, adminMode]);

  useEffect(() => {
    if (!allowed || !adminMode || !selectedStudentId) {
      return;
    }

    const selectedStudent = students.find((item) => String(item.studentId) === String(selectedStudentId));
    setLoading(true);
    Promise.all([
      apiRequest(`/parent/student/${selectedStudentId}/schedule`),
      apiRequest(`/parent/student/${selectedStudentId}/grades`),
      apiRequest(`/parent/student/${selectedStudentId}/homework`),
      apiRequest(`/parent/student/${selectedStudentId}/attendance`)
    ])
      .then(([scheduleData, gradeData, homeworkData, attendanceData]) => {
        setInfo(selectedStudent
          ? {
            studentId: selectedStudent.studentId,
            firstName: selectedStudent.firstName,
            lastName: selectedStudent.lastName,
            className: selectedStudent.className
          }
          : null);
        setSchedule(scheduleData ?? []);
        setGrades(gradeData ?? []);
        setHomework(homeworkData ?? []);
        setAttendance(attendanceData ?? []);
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить данные ученика"))
      .finally(() => setLoading(false));
  }, [allowed, adminMode, selectedStudentId, students]);

  if (!allowed) {
    return <AccessWarning title="Кабинет ученика доступен ученику и администратору" />;
  }

  const studentName = info
    ? `${info.lastName || ""} ${info.firstName || ""}`.trim() || "Ученик"
    : "Ученик";
  const studentClassName = info?.class?.name || info?.className || "класс не указан";

  if (adminMode) {
    return (
      <section className="page-stack">
        <PageHeader
          title="Данные ученика"
          subtitle={info ? `${studentName} · ${studentClassName}` : "Выберите ученика"}
          text="Административный просмотр расписания, оценок, домашних заданий и посещаемости выбранного ученика."
        />
        <StatusLine loading={loading} message={message} />
        <label className="field wide-field">
          <span>Ученик</span>
          <select value={selectedStudentId} onChange={(event) => setSelectedStudentId(event.target.value)}>
            {students.length === 0 ? (
              <option value="">Ученики не найдены</option>
            ) : sortItems(students, "name", {
              name: (item) => `${item.lastName} ${item.firstName}`,
              className: (item) => classSortValue(item.className)
            }).map((student) => (
              <option key={student.studentId} value={student.studentId}>
                {student.lastName} {student.firstName} · {student.className || "без класса"}
              </option>
            ))}
          </select>
        </label>
        <LearningSections schedule={schedule} grades={grades} homework={homework} attendance={attendance} view={view} />
      </section>
    );
  }

  return (
    <LearningPage
      title={view === "schedule" ? "Мое расписание" : "Кабинет ученика"}
      subtitle={info ? `${studentName} · ${studentClassName}` : "Учебная информация"}
      description="Здесь собраны расписание, оценки, домашние задания и посещаемость текущего ученика."
      schedule={schedule}
      grades={grades}
      homework={homework}
      attendance={attendance}
      loading={loading}
      message={message}
      view={view}
    />
  );
}

function ParentPage({ role }) {
  const allowed = role === "Родитель" || role === "Администратор";
  const adminMode = role === "Администратор";
  const [parents, setParents] = useState([]);
  const [selectedParentId, setSelectedParentId] = useState("");
  const [students, setStudents] = useState([]);
  const [selectedStudentId, setSelectedStudentId] = useState("");
  const [schedule, setSchedule] = useState([]);
  const [grades, setGrades] = useState([]);
  const [homework, setHomework] = useState([]);
  const [attendance, setAttendance] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!allowed) {
      return;
    }

    setLoading(true);
    if (adminMode) {
      apiRequest("/users")
        .then((data) => {
          const parentUsers = (data ?? []).filter((item) => item.roleName === "Родитель");
          setParents(parentUsers);
          if (!selectedParentId && parentUsers.length > 0) {
            setSelectedParentId(String(parentUsers[0].id));
          }
        })
        .catch((error) => setMessage(error.message || "Не удалось загрузить список родителей"))
        .finally(() => setLoading(false));
      return;
    }

    apiRequest("/parent/students")
      .then((data) => {
        const list = data ?? [];
        setStudents(list);
        if (list.length > 0) {
          setSelectedStudentId(String(list[0].studentId));
        }
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить список детей"))
      .finally(() => setLoading(false));
  }, [allowed, adminMode]);

  useEffect(() => {
    if (!allowed || !adminMode || !selectedParentId) {
      return;
    }

    setLoading(true);
    apiRequest(`/users/${selectedParentId}/students`)
      .then((data) => {
        const list = data ?? [];
        setStudents(list);
        setSelectedStudentId(list.length > 0 ? String(list[0].studentId) : "");
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить учеников родителя"))
      .finally(() => setLoading(false));
  }, [allowed, adminMode, selectedParentId]);

  useEffect(() => {
    if (!selectedStudentId) {
      return;
    }

    setLoading(true);
    Promise.all([
      apiRequest(`/parent/student/${selectedStudentId}/schedule`),
      apiRequest(`/parent/student/${selectedStudentId}/grades`),
      apiRequest(`/parent/student/${selectedStudentId}/homework`),
      apiRequest(`/parent/student/${selectedStudentId}/attendance`)
    ])
      .then(([scheduleData, gradeData, homeworkData, attendanceData]) => {
        setSchedule(scheduleData ?? []);
        setGrades(gradeData ?? []);
        setHomework(homeworkData ?? []);
        setAttendance(attendanceData ?? []);
      })
      .catch((error) => setMessage(error.message || "Не удалось загрузить данные ученика"))
      .finally(() => setLoading(false));
  }, [selectedStudentId]);

  if (!allowed) {
    return <AccessWarning title="Кабинет родителя доступен родителю и администратору" />;
  }

  const selectedStudent = students.find((item) => String(item.studentId) === String(selectedStudentId));

  return (
    <section className="page-stack">
      <PageHeader
        title={adminMode ? "Данные родителя" : "Кабинет родителя"}
        subtitle={selectedStudent ? `${selectedStudent.firstName} ${selectedStudent.lastName}` : "Выберите ученика"}
        text={adminMode
          ? "Административный просмотр данных детей, привязанных к выбранному родителю."
          : "Доступ к успеваемости, посещаемости, расписанию и домашним заданиям привязанных учеников."}
      />
      <StatusLine loading={loading} message={message} />
      {adminMode && (
        <label className="field wide-field">
          <span>Родитель</span>
          <select value={selectedParentId} onChange={(event) => setSelectedParentId(event.target.value)}>
            {parents.length === 0 ? (
              <option value="">Родители не найдены</option>
            ) : sortItems(parents, "fullName", { fullName: (item) => item.fullName || "" }).map((parent) => (
              <option key={parent.id} value={parent.id}>
                {parent.fullName} · {parent.login}
              </option>
            ))}
          </select>
        </label>
      )}
      <label className="field wide-field">
        <span>Ученик</span>
        <select value={selectedStudentId} onChange={(event) => setSelectedStudentId(event.target.value)}>
          {students.length === 0 ? (
            <option value="">Нет привязанных учеников</option>
          ) : students.map((student) => (
            <option key={student.studentId} value={student.studentId}>
              {student.firstName} {student.lastName} · {student.class?.name || student.className || "класс не указан"}
            </option>
          ))}
        </select>
      </label>
      <LearningSections schedule={schedule} grades={grades} homework={homework} attendance={attendance} />
    </section>
  );
}

function LearningPage({ title, subtitle, description, schedule, grades, homework, attendance, loading, message, view = "full" }) {
  return (
    <section className="page-stack">
      <PageHeader title={title} subtitle={subtitle} text={description} />
      <StatusLine loading={loading} message={message} />
      <LearningSections schedule={schedule} grades={grades} homework={homework} attendance={attendance} view={view} />
    </section>
  );
}

function LearningSections({ schedule, grades, homework, attendance, view = "full" }) {
  const [attendanceOpen, setAttendanceOpen] = useState(false);
  const [attendanceFilter, setAttendanceFilter] = useState("problem");
  const [attendanceSort, setAttendanceSort] = useState("date-desc");
  const averageGrade = grades.length
    ? grades.reduce((sum, item) => sum + Number(item.value ?? 0), 0) / grades.length
    : 0;
  const present = attendance.filter((item) => Number(item.status ?? 1) === 1).length;
  const problemAttendance = attendance.filter((item) => Number(item.status ?? 1) !== 1).length;
  const [learningWeekStart, setLearningWeekStart] = useState(() => getInitialLearningWeekStart(schedule, attendance));

  useEffect(() => {
    setLearningWeekStart(getInitialLearningWeekStart(schedule, attendance));
  }, [schedule, attendance]);

  const showOnlySchedule = view === "schedule";
  const todaySchedule = getTodaySchedule(schedule);

  return (
    <>
      <div className="metric-grid">
        <MetricCard label={showOnlySchedule ? "Уроков в расписании" : "Уроков сегодня"} value={showOnlySchedule ? schedule.length : todaySchedule.length} />
        <MetricCard label="Оценок" value={grades.length} />
        <MetricCard label="Средний балл" value={formatNumber(averageGrade)} />
        <MetricCard label="Пропусков/опозданий" value={problemAttendance} />
      </div>
      {showOnlySchedule ? (
        <WeeklyLearningSchedule
          schedule={schedule}
          attendance={attendance}
          weekStart={learningWeekStart}
          onWeekStartChange={setLearningWeekStart}
        />
      ) : (
        <>
          <TodaySchedulePanel schedule={todaySchedule} attendance={attendance} />
          <DataTable
            title="Последние оценки"
            columns={["Дата", "Предмет", "Тема", "Оценка"]}
            rows={grades.slice(0, 16).map((grade) => [
              formatDate(grade.date),
              grade.subject || grade.subjectName || "—",
              formatLessonTopic(grade.topic),
              grade.value
            ])}
          />
          <CardGrid
            title="Домашние задания"
            items={homework.slice(0, 8).map((item) => ({
              title: item.subject || item.subjectName || item.name || "Предмет",
              text: item.homework || item.task || "Домашнее задание не указано",
              meta: `${formatDate(item.date)} · ${formatLessonTopic(item.topic)}`
            }))}
          />
          <AttendanceReviewPanel
            attendance={attendance}
            isOpen={attendanceOpen}
            filter={attendanceFilter}
            sort={attendanceSort}
            onToggle={() => setAttendanceOpen((current) => !current)}
            onFilterChange={setAttendanceFilter}
            onSortChange={setAttendanceSort}
          />
        </>
      )}
    </>
  );
}

function TodaySchedulePanel({ schedule, attendance }) {
  const attendanceByLesson = new Map(attendance.map((item) => [Number(item.lessonId), item]));

  return (
    <section className="table-card today-schedule-card">
      <div className="table-title">Сегодняшнее расписание</div>
      <div className="today-schedule-list">
        {schedule.length === 0 ? (
          <p className="empty-text padded">На сегодня уроков нет</p>
        ) : schedule.map((lesson) => {
          const status = attendanceByLesson.get(Number(lesson.lessonId))?.status ?? 1;
          return (
            <article className={`today-lesson-row ${attendanceClassName(status)}`} key={lesson.lessonId}>
              <div className="lesson-time-row">
                <span>{lesson.lessonNumber ? `${lesson.lessonNumber} урок` : "Урок"}</span>
                <span>{formatTimeRange(lesson)}</span>
              </div>
              <div>
                <strong>{lesson.subject || lesson.subjectName || lesson.name || "Предмет"}</strong>
                <small>{lesson.teacher || lesson.teacherName || "Учитель не указан"}</small>
              </div>
              {lesson.homework && <p className="homework-note">ДЗ: {lesson.homework}</p>}
            </article>
          );
        })}
      </div>
    </section>
  );
}

function WeeklyLearningSchedule({ schedule, attendance, weekStart, onWeekStartChange }) {
  const weekDays = getLearningWeekDays(weekStart);
  const weekLessons = filterItemsByWeek(schedule, weekStart);
  const attendanceByLesson = new Map(attendance.map((item) => [Number(item.lessonId), item]));

  return (
    <section className="table-card learning-week-card">
      <div className="learning-week-title">
        <div>
          <strong>Расписание недели</strong>
          <span>{getWeekCaption(weekStart)}</span>
        </div>
        <div className="learning-week-actions">
          <button className="ghost-button compact" type="button" onClick={() => onWeekStartChange(shiftWeek(weekStart, -7))}>Назад</button>
          <button className="ghost-button compact" type="button" onClick={() => onWeekStartChange(toIsoDate(getMonday(new Date())))}>Сегодня</button>
          <button className="ghost-button compact" type="button" onClick={() => onWeekStartChange(shiftWeek(weekStart, 7))}>Вперед</button>
        </div>
      </div>
      <div className="learning-week-grid">
        {weekDays.map((date) => {
          const dayLessons = weekLessons
            .filter((lesson) => getDateKey(lesson.date) === date)
            .sort(comparePortalLessons);

          return (
            <div className="learning-day-column" key={date}>
              <div className="learning-day-heading">
                <strong>{dayName(getDayIndex(date))}</strong>
                <span>{formatDate(date)}</span>
              </div>
              <div className="learning-day-lessons">
                {dayLessons.length === 0 ? (
                  <p className="empty-text">Уроков нет</p>
                ) : dayLessons.map((lesson) => {
                  const attendanceItem = attendanceByLesson.get(Number(lesson.lessonId));
                  const status = attendanceItem?.status ?? 1;
                  return (
                    <article className={`learning-lesson-card ${attendanceClassName(status)}`} key={lesson.lessonId}>
                      <div className="lesson-time-row">
                        <span>{lesson.lessonNumber ? `${lesson.lessonNumber} урок` : "Урок"}</span>
                        <span>{formatTimeRange(lesson)}</span>
                      </div>
                      <strong>{lesson.subject || lesson.subjectName || lesson.name || "Предмет"}</strong>
                      <small>{lesson.teacher || lesson.teacherName || "Учитель не указан"}</small>
                      {lesson.topic && <p>{formatLessonTopic(lesson.topic)}</p>}
                      {lesson.homework && <p className="homework-note">ДЗ: {lesson.homework}</p>}
                    </article>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function WeeklyAttendancePanel({ schedule, attendance, weekStart }) {
  const weekLessons = filterItemsByWeek(schedule, weekStart).sort(comparePortalLessons);
  const attendanceByLesson = new Map(attendance.map((item) => [Number(item.lessonId), item]));

  return (
    <section className="table-card attendance-week-card">
      <div className="table-title">Посещаемость за неделю</div>
      <div className="attendance-week-list">
        {weekLessons.length === 0 ? (
          <p className="empty-text padded">Нет уроков на выбранной неделе</p>
        ) : weekLessons.map((lesson) => {
          const attendanceItem = attendanceByLesson.get(Number(lesson.lessonId));
          const status = attendanceItem?.status ?? 1;
          return (
            <article className={`attendance-week-row ${attendanceClassName(status)}`} key={lesson.lessonId}>
              <div>
                <strong>{lesson.subject || lesson.subjectName || lesson.name || "Предмет"}</strong>
                <span>{formatDate(lesson.date)} · {formatTimeRange(lesson)}</span>
              </div>
              <b>{attendanceLabel(status)}</b>
            </article>
          );
        })}
      </div>
    </section>
  );
}

function AttendanceReviewPanel({ attendance, isOpen, filter, sort, onToggle, onFilterChange, onSortChange }) {
  const filteredAttendance = getFilteredAttendance(attendance, filter, sort);
  const problemCount = attendance.filter((item) => Number(item.status ?? 1) !== 1).length;

  return (
    <section className="table-card attendance-review-card">
      <button className="attendance-review-header" type="button" onClick={onToggle} aria-expanded={isOpen}>
        <span>
          <strong>Посещаемость</strong>
          <small>{problemCount ? `Пропусков и опозданий: ${problemCount}` : "Все отмеченные уроки без проблем"}</small>
        </span>
        <b>{isOpen ? "Свернуть" : "Развернуть"}</b>
      </button>
      {isOpen && (
        <>
          <div className="attendance-review-filters">
            <label className="field">
              <span>Показать</span>
              <select value={filter} onChange={(event) => onFilterChange(event.target.value)}>
                <option value="problem">Только отсутствия и опоздания</option>
                <option value="absent">Только отсутствия</option>
                <option value="late">Только опоздания</option>
                <option value="all">Все записи</option>
              </select>
            </label>
            <label className="field">
              <span>Сортировка</span>
              <select value={sort} onChange={(event) => onSortChange(event.target.value)}>
                <option value="date-desc">Сначала новые</option>
                <option value="date-asc">Сначала старые</option>
                <option value="subject">По предмету</option>
              </select>
            </label>
          </div>
          <div className="attendance-week-list">
            {filteredAttendance.length === 0 ? (
              <p className="empty-text padded">Записей по выбранному фильтру нет</p>
            ) : filteredAttendance.map((item) => {
              const status = item.status ?? 1;
              return (
                <article className={`attendance-week-row ${attendanceClassName(status)}`} key={`${item.lessonId}-${item.attendanceId ?? "default"}`}>
                  <div>
                    <strong>{item.subject || item.subjectName || item.name || "Предмет"}</strong>
                    <span>{formatDate(item.date)} · {formatLessonTopic(item.topic)}</span>
                  </div>
                  <b>{attendanceLabel(status)}</b>
                </article>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}

function SchedulePage({ role, user }) {
  const allowed = role === "Менеджер расписания" || role === "Администратор" || role === "Директор" || role === "Учитель";
  const editable = role === "Менеджер расписания" || role === "Администратор";
  const teacherView = role === "Учитель";
  const [weekStart, setWeekStart] = useState(() => toIsoDate(getMonday(new Date())));
  const [week, setWeek] = useState({ lessons: [] });
  const [metadata, setMetadata] = useState(null);
  const [selectedCell, setSelectedCell] = useState(null);
  const [draggedLesson, setDraggedLesson] = useState(null);
  const [dragOverCell, setDragOverCell] = useState(null);
  const [copiedLesson, setCopiedLesson] = useState(null);
  const [copiedLessons, setCopiedLessons] = useState([]);
  const [clipboardMode, setClipboardMode] = useState("copy");
  const [selectedLessonIds, setSelectedLessonIds] = useState([]);
  const [copyWeekTarget, setCopyWeekTarget] = useState(() => shiftWeek(weekStart, 7));
  const [scheduleMenu, setScheduleMenu] = useState(null);
  const [lessonEditorOpen, setLessonEditorOpen] = useState(false);
  const [isScheduleFullscreen, setIsScheduleFullscreen] = useState(false);
  const [scheduleScope, setScheduleScope] = useState("mine");
  const [teacherClasses, setTeacherClasses] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [className, setClassName] = useState("");
  const [lessonForm, setLessonForm] = useState({
    subjectId: "",
    subjectName: "",
    subjectInput: "",
    teacherId: "",
    teacherName: "",
    homework: ""
  });

  async function loadScheduleEditor(options = {}) {
    setLoading(true);
    setMessage("");
    try {
      const shouldRefreshMetadata = options.refreshMetadata || !metadata;
      const [weekData, metadataData] = await Promise.all([
        apiRequest(`/schedule/editor/week?weekStart=${weekStart}`),
        shouldRefreshMetadata ? apiRequest("/schedule/editor/metadata") : Promise.resolve(metadata)
      ]);
      setWeek(weekData ?? { lessons: [] });
      if (shouldRefreshMetadata) {
        setMetadata(metadataData);
      }
    } catch (error) {
      setMessage(error.message || "Не удалось загрузить редактор расписания");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!allowed) {
      return;
    }

    loadScheduleEditor();
  }, [allowed, weekStart]);

  useEffect(() => {
    if (!teacherView || !user?.id) {
      return;
    }

    apiRequest(`/teacher/classes?teacherId=${user.id}`)
      .then((items) => setTeacherClasses(items ?? []))
      .catch(() => setTeacherClasses([]));
  }, [teacherView, user?.id]);

  useEffect(() => {
    setCopyWeekTarget(shiftWeek(weekStart, 7));
    setSelectedLessonIds([]);
  }, [weekStart]);

  function selectCell(classItem, slot, lesson) {
    setScheduleMenu(null);
    setSelectedCell({ classItem, slot, lesson });
    const subjectId = lesson?.subjectId ?? "";
    const teacherId = lesson?.teacherId
      ?? metadata?.subjects?.find((item) => Number(item.subjectId) === Number(subjectId))?.teacherId
      ?? "";
    const teacherName = metadata?.teachers?.find((item) => Number(item.id) === Number(teacherId))?.fullName ?? "";
    const subjectName = lesson?.subjectName
      ?? metadata?.subjects?.find((item) => Number(item.subjectId) === Number(subjectId))?.name
      ?? "";
    const subject = metadata?.subjects?.find((item) => Number(item.subjectId) === Number(subjectId));
    setLessonForm({
      subjectId,
      subjectName,
      subjectInput: subject ? getScheduleSubjectLabel(subject) : subjectName,
      teacherId,
      teacherName,
      homework: lesson?.homework ?? ""
    });
  }

  function openLessonEditor(classItem, slot, lesson) {
    selectCell(classItem, slot, lesson);
    if (lesson?.lessonId) {
      setSelectedLessonIds([String(lesson.lessonId)]);
    }
    setLessonEditorOpen(true);
  }

  function selectScheduleCell(event, classItem, slot, lesson) {
    if (!lesson) {
      selectCell(classItem, slot, null);
      setSelectedLessonIds([]);
      return;
    }

    selectCell(classItem, slot, lesson);
    setSelectedLessonIds((current) => {
      const lessonId = String(lesson.lessonId);
      if (!event?.shiftKey) {
        return [lessonId];
      }

      return current.includes(lessonId)
        ? current.filter((item) => item !== lessonId)
        : [...current, lessonId];
    });
  }

  function getSelectedScheduleLessons(fallbackLesson = null) {
    const selectedIds = new Set(selectedLessonIds.map(String));
    const selectedLessons = (week.lessons ?? []).filter((lesson) => selectedIds.has(String(lesson.lessonId)));
    if (selectedLessons.length > 0) {
      return selectedLessons;
    }

    return fallbackLesson ? [fallbackLesson] : [];
  }

  function updateWeekLessons(updater) {
    setWeek((current) => ({
      ...current,
      lessons: updater(current.lessons ?? [])
    }));
  }

  function replaceWeekLesson(lessonId, nextLesson) {
    updateWeekLessons((lessons) => lessons.map((lesson) =>
      String(lesson.lessonId) === String(lessonId) ? nextLesson : lesson
    ));
  }

  function removeWeekLesson(lessonId) {
    updateWeekLessons((lessons) => lessons.filter((lesson) => String(lesson.lessonId) !== String(lessonId)));
  }

  function makeScheduleLesson(lesson, targetClass, targetSlot, payload = null) {
    const subjectId = payload?.subjectId ?? lesson.subjectId;
    const teacherId = payload?.teacherId ?? lesson.teacherId;
    const subject = subjects.find((item) => Number(item.subjectId) === Number(subjectId));
    const teacher = teachers.find((item) => Number(item.id) === Number(teacherId));
    return {
      ...lesson,
      classId: targetClass.classId,
      className: targetClass.name,
      subjectId,
      subjectName: subject?.name ?? payload?.subjectName ?? lesson.subjectName,
      teacherId,
      teacherName: teacher?.fullName ?? lesson.teacherName,
      scheduleId: targetSlot.scheduleId,
      dayOfWeek: targetSlot.dayOfWeek,
      lessonNumber: targetSlot.lessonNumber,
      startTime: targetSlot.startTime,
      endTime: targetSlot.endTime,
      date: payload?.date ?? getLessonDateForSlot(weekStart, targetSlot),
      homework: payload?.homework ?? lesson.homework ?? ""
    };
  }

  function makeTempLesson(lesson, targetClass, targetSlot, payload = null) {
    return makeScheduleLesson({
      ...lesson,
      lessonId: `temp-${Date.now()}-${Math.random().toString(16).slice(2)}`
    }, targetClass, targetSlot, payload);
  }

  function openScheduleMenu(event, classItem, slot, lesson) {
    if (!editable) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    selectCell(classItem, slot, lesson);
    const editorRect = event.currentTarget.closest(".schedule-editor")?.getBoundingClientRect();
    const menuWidth = 280;
    const menuHeight = 220;
    const relativeX = editorRect ? event.clientX - editorRect.left : event.clientX;
    const relativeY = editorRect ? event.clientY - editorRect.top : event.clientY;
    const maxX = editorRect ? editorRect.width - menuWidth - 12 : window.innerWidth - menuWidth - 12;
    const maxY = editorRect ? editorRect.height - menuHeight - 12 : window.innerHeight - menuHeight - 12;
    setScheduleMenu({
      x: Math.max(12, Math.min(relativeX, maxX)),
      y: Math.max(12, Math.min(relativeY, maxY)),
      classItem,
      slot,
      lesson
    });
  }

  async function saveLesson(event) {
    event.preventDefault();
    const subjectName = lessonForm.subjectName.trim();
    const canCreateSubjectInSchedule = editable;
    const shouldRefreshMetadataAfterSave = !lessonForm.subjectId;
    if (!selectedCell || (!lessonForm.subjectId && (!canCreateSubjectInSchedule || !subjectName)) || !lessonForm.teacherId) {
      setMessage("Выберите ячейку, предмет и преподавателя");
      return;
    }

    const payload = {
      classId: selectedCell.classItem.classId,
      subjectId: Number(lessonForm.subjectId || 0),
      subjectName,
      teacherId: Number(lessonForm.teacherId),
      scheduleId: selectedCell.slot.scheduleId,
      date: getLessonDateForSlot(weekStart, selectedCell.slot),
      homework: lessonForm.homework.trim()
    };

    try {
      if (selectedCell.lesson) {
        const lessonId = selectedCell.lesson.lessonId;
        const optimisticLesson = makeScheduleLesson(selectedCell.lesson, selectedCell.classItem, selectedCell.slot, payload);
        replaceWeekLesson(lessonId, optimisticLesson);
        setLessonEditorOpen(false);
        setSelectedCell(null);
        const savedLesson = await apiRequest(`/schedule/editor/lesson/${lessonId}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });
        replaceWeekLesson(lessonId, savedLesson ?? optimisticLesson);
        setMessage("Урок обновлен");
      } else {
        const optimisticLesson = makeTempLesson({}, selectedCell.classItem, selectedCell.slot, payload);
        updateWeekLessons((lessons) => [...lessons, optimisticLesson]);
        setLessonEditorOpen(false);
        setSelectedCell(null);
        const savedLesson = await apiRequest("/schedule/editor/lesson", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        replaceWeekLesson(optimisticLesson.lessonId, savedLesson ?? optimisticLesson);
        setMessage("Урок добавлен в сетку");
      }
      if (shouldRefreshMetadataAfterSave) {
        await loadScheduleEditor({ refreshMetadata: true });
      }
    } catch (error) {
      await loadScheduleEditor();
      setMessage(error.message || "Не удалось сохранить урок");
    }
  }

  async function deleteScheduleLesson(lesson = selectedCell?.lesson) {
    const lessonsToDelete = getSelectedScheduleLessons(lesson);
    if (lessonsToDelete.length === 0) {
      return;
    }

    const removedLessons = lessonsToDelete;
    const idsToDelete = new Set(lessonsToDelete.map((item) => String(item.lessonId)));
    updateWeekLessons((lessons) => lessons.filter((item) => !idsToDelete.has(String(item.lessonId))));
    setLessonEditorOpen(false);
    setSelectedCell(null);
    setSelectedLessonIds([]);
    setScheduleMenu(null);
    try {
      await Promise.all(lessonsToDelete.map((item) =>
        apiRequest(`/schedule/editor/lesson/${item.lessonId}`, { method: "DELETE" })
      ));
      setMessage("Урок удален из сетки");
    } catch (error) {
      updateWeekLessons((lessons) => [...lessons, ...removedLessons]);
      setMessage(error.message || "Не удалось удалить урок");
    }
  }

  function canPlaceLessonInClass(lesson, targetClass) {
    const subject = subjects.find((item) => Number(item.subjectId) === Number(lesson.subjectId));
    const allowedClassIds = subject?.classIds ?? [];
    return allowedClassIds.length === 0 || allowedClassIds.some((classId) => Number(classId) === Number(targetClass.classId));
  }

  function buildScheduleLessonPayload(lesson, targetClass, targetSlot) {
    return {
      classId: targetClass.classId,
      subjectId: lesson.subjectId,
      teacherId: lesson.teacherId,
      scheduleId: targetSlot.scheduleId,
      date: getLessonDateForSlot(weekStart, targetSlot),
      homework: lesson.homework ?? ""
    };
  }

  function validateLessonPlacement(lesson, targetClass, targetSlot) {
    if (!lesson || !targetClass || !targetSlot) {
      return "Не выбран урок или ячейка";
    }

    const targetKey = `${targetClass.classId}_${targetSlot.scheduleId}`;
    if (lessonMap.get(targetKey)) {
      return "В этой ячейке уже есть урок. Сначала освободите слот или удалите лишний урок.";
    }

    if (!canPlaceLessonInClass(lesson, targetClass)) {
      return `Предмет "${lesson.subjectName}" не назначен классу ${targetClass.name}`;
    }

    return "";
  }

  async function moveScheduleLesson(lesson, targetClass, targetSlot) {
    const sourceKey = `${lesson?.classId}_${lesson?.scheduleId}`;
    const targetKey = `${targetClass?.classId}_${targetSlot?.scheduleId}`;
    if (sourceKey === targetKey) {
      return;
    }

    const validationMessage = validateLessonPlacement(lesson, targetClass, targetSlot);
    if (validationMessage) {
      setMessage(validationMessage);
      return;
    }

    const optimisticLesson = makeScheduleLesson(lesson, targetClass, targetSlot);
    replaceWeekLesson(lesson.lessonId, optimisticLesson);
    setSelectedCell(null);
    setScheduleMenu(null);
    try {
      const savedLesson = await apiRequest(`/schedule/editor/lesson/${lesson.lessonId}`, {
        method: "PUT",
        body: JSON.stringify(buildScheduleLessonPayload(lesson, targetClass, targetSlot))
      });
      replaceWeekLesson(lesson.lessonId, savedLesson ?? optimisticLesson);
      setMessage(`Урок перенесен: ${targetClass.name}, ${dayName(targetSlot.dayOfWeek)}, ${targetSlot.lessonNumber} урок`);
    } catch (error) {
      replaceWeekLesson(lesson.lessonId, lesson);
      setMessage(error.message || "Не удалось перенести урок");
    }
  }

  function copyScheduleLesson(lesson, mode = "copy") {
    const lessonsToCopy = getSelectedScheduleLessons(lesson);
    if (lessonsToCopy.length === 0) {
      return;
    }

    setCopiedLesson(lessonsToCopy[0]);
    setCopiedLessons(lessonsToCopy);
    setClipboardMode(mode);
    setScheduleMenu(null);
    setMessage(mode === "cut"
      ? `Вырезано уроков: ${lessonsToCopy.length}`
      : `Скопировано уроков: ${lessonsToCopy.length}`);
  }

  function cutScheduleLesson(lesson) {
    copyScheduleLesson(lesson, "cut");
  }

  async function pasteCopiedLesson(targetCell = selectedCell) {
    const lessonsToPaste = copiedLessons.length > 0 ? copiedLessons : (copiedLesson ? [copiedLesson] : []);
    if (lessonsToPaste.length === 0) {
      setMessage("Сначала скопируйте или вырежьте урок");
      return;
    }

    if (!targetCell) {
      setMessage("Выберите свободную ячейку для вставки");
      return;
    }

    const orderedClasses = classes;
    const orderedSlots = [...slots].sort((left, right) =>
      left.dayOfWeek - right.dayOfWeek || left.lessonNumber - right.lessonNumber
    );
    const baseLesson = lessonsToPaste[0];
    const baseClassIndex = orderedClasses.findIndex((item) => Number(item.classId) === Number(baseLesson.classId));
    const baseSlotIndex = orderedSlots.findIndex((slot) => Number(slot.scheduleId) === Number(baseLesson.scheduleId));
    const targetClassIndex = orderedClasses.findIndex((item) => Number(item.classId) === Number(targetCell.classItem.classId));
    const targetSlotIndex = orderedSlots.findIndex((slot) => Number(slot.scheduleId) === Number(targetCell.slot.scheduleId));

    if ([baseClassIndex, baseSlotIndex, targetClassIndex, targetSlotIndex].some((index) => index < 0)) {
      setMessage("Не удалось сопоставить выбранные уроки с сеткой расписания");
      return;
    }

    const placements = [];
    const targetKeys = new Set();
    for (const lesson of lessonsToPaste) {
      const sourceClassIndex = orderedClasses.findIndex((item) => Number(item.classId) === Number(lesson.classId));
      const sourceSlotIndex = orderedSlots.findIndex((slot) => Number(slot.scheduleId) === Number(lesson.scheduleId));
      const targetClass = orderedClasses[targetClassIndex + sourceClassIndex - baseClassIndex];
      const targetSlot = orderedSlots[targetSlotIndex + sourceSlotIndex - baseSlotIndex];

      if (!targetClass || !targetSlot) {
        setMessage("Блок уроков не помещается в выбранное место");
        return;
      }

      const targetKey = `${targetClass.classId}_${targetSlot.scheduleId}`;
      if (targetKeys.has(targetKey)) {
        setMessage("В выбранном блоке есть повторяющиеся ячейки");
        return;
      }

      const validationMessage = validateLessonPlacement(lesson, targetClass, targetSlot);
      if (validationMessage) {
        setMessage(validationMessage);
        return;
      }

      targetKeys.add(targetKey);
      placements.push({ lesson, targetClass, targetSlot });
    }

    const optimisticLessons = placements.map(({ lesson, targetClass, targetSlot }) =>
      makeTempLesson(lesson, targetClass, targetSlot)
    );
    updateWeekLessons((lessons) => [...lessons, ...optimisticLessons]);
    setSelectedCell(null);
    setSelectedLessonIds([]);
    setScheduleMenu(null);
    try {
      const savedLessons = await Promise.all(placements.map(({ lesson, targetClass, targetSlot }) =>
        apiRequest("/schedule/editor/lesson", {
          method: "POST",
          body: JSON.stringify(buildScheduleLessonPayload(lesson, targetClass, targetSlot))
        })
      ));

      savedLessons.forEach((savedLesson, index) => {
        replaceWeekLesson(optimisticLessons[index].lessonId, savedLesson ?? optimisticLessons[index]);
      });

      if (clipboardMode === "cut") {
        await Promise.all(lessonsToPaste.map((lesson) =>
          apiRequest(`/schedule/editor/lesson/${lesson.lessonId}`, { method: "DELETE" })
        ));
        const sourceIds = new Set(lessonsToPaste.map((lesson) => String(lesson.lessonId)));
        updateWeekLessons((lessons) => lessons.filter((lesson) => !sourceIds.has(String(lesson.lessonId))));
        setCopiedLesson(null);
        setCopiedLessons([]);
        setClipboardMode("copy");
      }

      setMessage(lessonsToPaste.length > 1
        ? `Вставлено уроков: ${lessonsToPaste.length}`
        : `Урок вставлен: ${targetCell.classItem.name}, ${dayName(targetCell.slot.dayOfWeek)}, ${targetCell.slot.lessonNumber} урок`);
    } catch (error) {
      const optimisticIds = new Set(optimisticLessons.map((lesson) => String(lesson.lessonId)));
      updateWeekLessons((lessons) => lessons.filter((lesson) => !optimisticIds.has(String(lesson.lessonId))));
      setMessage(error.message || "Не удалось вставить урок");
    }
  }

  async function duplicateLessonToNextFreeSlot(lesson = selectedCell?.lesson) {
    if (!lesson) {
      return;
    }

    const currentClass = classes.find((item) => Number(item.classId) === Number(lesson.classId));
    if (!currentClass) {
      setMessage("Класс урока не найден");
      return;
    }

    const orderedSlots = [...slots].sort((left, right) =>
      left.dayOfWeek - right.dayOfWeek || left.lessonNumber - right.lessonNumber
    );
    const currentIndex = orderedSlots.findIndex((slot) => Number(slot.scheduleId) === Number(lesson.scheduleId));
    const rotatedSlots = [...orderedSlots.slice(currentIndex + 1), ...orderedSlots.slice(0, Math.max(currentIndex, 0))];
    const freeSlot = rotatedSlots.find((slot) => !lessonMap.get(`${currentClass.classId}_${slot.scheduleId}`));

    if (!freeSlot) {
      setMessage(`В классе ${currentClass.name} нет свободных слотов на этой неделе`);
      return;
    }

    await pasteLessonIntoCell(lesson, currentClass, freeSlot, "Урок продублирован");
  }

  async function pasteLessonIntoCell(lesson, targetClass, targetSlot, successPrefix) {
    const validationMessage = validateLessonPlacement(lesson, targetClass, targetSlot);
    if (validationMessage) {
      setMessage(validationMessage);
      return;
    }

    const optimisticLesson = makeTempLesson(lesson, targetClass, targetSlot);
    updateWeekLessons((lessons) => [...lessons, optimisticLesson]);
    setSelectedCell(null);
    setScheduleMenu(null);
    try {
      const savedLesson = await apiRequest("/schedule/editor/lesson", {
        method: "POST",
        body: JSON.stringify(buildScheduleLessonPayload(lesson, targetClass, targetSlot))
      });
      replaceWeekLesson(optimisticLesson.lessonId, savedLesson ?? optimisticLesson);
      setMessage(`${successPrefix}: ${targetClass.name}, ${dayName(targetSlot.dayOfWeek)}, ${targetSlot.lessonNumber} урок`);
    } catch (error) {
      removeWeekLesson(optimisticLesson.lessonId);
      setMessage(error.message || "Не удалось создать копию урока");
    }
  }

  async function copyFullScheduleWeek(event) {
    event.preventDefault();
    const normalizedTarget = toIsoDate(getMonday(new Date(copyWeekTarget)));
    if (normalizedTarget === weekStart) {
      setMessage("Выберите другую неделю для копирования");
      return;
    }

    setLoading(true);
    try {
      const result = await apiRequest("/schedule/editor/week/copy", {
        method: "POST",
        body: JSON.stringify({
          sourceWeekStart: weekStart,
          targetWeekStart: normalizedTarget
        })
      });
      setWeekStart(normalizedTarget);
      const copiedCount = result?.copiedCount ?? 0;
      const skippedCount = result?.skippedCount ?? 0;
      setMessage(skippedCount > 0
        ? `Добавлено уроков: ${copiedCount}. Уже было на месте: ${skippedCount}`
        : `Неделя скопирована без изменений. Уроков: ${copiedCount}`);
    } catch (error) {
      setMessage(error.message || "Не удалось скопировать неделю расписания");
    } finally {
      setLoading(false);
    }
  }

  async function createClass(event) {
    event.preventDefault();
    if (!className.trim()) {
      return;
    }

    try {
      await apiRequest("/schedule/editor/class", {
        method: "POST",
        body: JSON.stringify({ name: className.trim() })
      });
      setClassName("");
      setMessage("Класс добавлен");
      await loadScheduleEditor({ refreshMetadata: true });
    } catch (error) {
      setMessage(error.message || "Не удалось создать класс");
    }
  }

  useEffect(() => {
    if (!editable) {
      return undefined;
    }

    const closeMenu = () => setScheduleMenu(null);
    const handleKeyDown = (event) => {
      const target = event.target;
      const tagName = target?.tagName?.toLowerCase();
      const isTyping = target?.isContentEditable || ["input", "select", "textarea"].includes(tagName);
      if (isTyping) {
        return;
      }

      const isCopyShortcut = (event.ctrlKey || event.metaKey) && event.code === "KeyC";
      const isCutShortcut = (event.ctrlKey || event.metaKey) && event.code === "KeyX";
      const isPasteShortcut = (event.ctrlKey || event.metaKey) && event.code === "KeyV";
      if (isCopyShortcut) {
        if (selectedCell?.lesson) {
          event.preventDefault();
          copyScheduleLesson(selectedCell.lesson);
        }
        return;
      }

      if (isCutShortcut) {
        if (selectedCell?.lesson) {
          event.preventDefault();
          cutScheduleLesson(selectedCell.lesson);
        }
        return;
      }

      if (isPasteShortcut) {
        if (selectedCell && !selectedCell.lesson && (copiedLesson || copiedLessons.length > 0)) {
          event.preventDefault();
          pasteCopiedLesson(selectedCell);
        }
        return;
      }

      if (event.key === "Delete" && selectedCell?.lesson) {
        event.preventDefault();
        deleteScheduleLesson(selectedCell.lesson);
        return;
      }

      if (event.key === "Escape") {
        setScheduleMenu(null);
      }
    };

    window.addEventListener("click", closeMenu);
    window.addEventListener("scroll", closeMenu, true);
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("click", closeMenu);
      window.removeEventListener("scroll", closeMenu, true);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [editable, selectedCell, copiedLesson, copiedLessons, selectedLessonIds, week.lessons, metadata]);

  useEffect(() => {
    document.body.classList.toggle("schedule-fullscreen-lock", isScheduleFullscreen);
    return () => document.body.classList.remove("schedule-fullscreen-lock");
  }, [isScheduleFullscreen]);

  useEffect(() => {
    const handleFullscreenKeyDown = (event) => {
      if (event.key === "Escape") {
        setIsScheduleFullscreen(false);
      }
    };

    window.addEventListener("keydown", handleFullscreenKeyDown);
    return () => window.removeEventListener("keydown", handleFullscreenKeyDown);
  }, []);

  if (!allowed) {
    return <AccessWarning title="Расписание недоступно для текущей роли" />;
  }

  const allClasses = sortItems(metadata?.classes ?? [], "name", { name: (item) => classSortValue(item.name) });
  const subjects = metadata?.subjects ?? [];
  const teachers = metadata?.teachers ?? [];
  const slots = metadata?.slots ?? [];
  const teacherId = Number(user?.id ?? 0);
  const teacherClassIds = new Set([
    ...teacherClasses.map((classItem) => Number(classItem.classId ?? classItem.id)),
    ...(week.lessons ?? [])
      .filter((lesson) => Number(lesson.teacherId) === teacherId)
      .map((lesson) => Number(lesson.classId))
  ]);
  const visibleLessons = teacherView && scheduleScope === "mine"
    ? (week.lessons ?? []).filter((lesson) => Number(lesson.teacherId) === teacherId)
    : (week.lessons ?? []);
  const visibleSubjects = teacherView && scheduleScope === "mine"
    ? subjects.filter((subject) => {
        const teacherIds = subject.teacherIds?.length ? subject.teacherIds : [subject.teacherId];
        return teacherIds.some((item) => Number(item) === teacherId);
      })
    : subjects;
  const classes = teacherView && scheduleScope === "mine"
    ? allClasses.filter((classItem) => teacherClassIds.has(Number(classItem.classId)))
    : allClasses;
  const lessonMap = buildScheduleLessonMap(visibleLessons);
  const selectedLessonIdSet = new Set(selectedLessonIds.map(String));
  const groupedSlots = groupSlotsByDay(slots);
  const weekCaption = getWeekCaption(weekStart);
  const selectedClassId = selectedCell?.classItem?.classId ?? null;
    const canCreateSubjectInSchedule = editable;
  const teacherOptionsId = "schedule-teacher-options";
  const subjectOptionsId = "schedule-subject-options";
  const filteredSubjects = subjects.filter((subject) => {
    const subjectTeacherIds = subject.teacherIds?.length ? subject.teacherIds : [subject.teacherId];
    const subjectClassIds = subject.classIds ?? [];
    const teacherMatches = !lessonForm.teacherId
      || subjectTeacherIds.some((teacherId) => Number(teacherId) === Number(lessonForm.teacherId));
    const classMatches = !selectedClassId
      || subjectClassIds.length === 0
      || subjectClassIds.some((classId) => Number(classId) === Number(selectedClassId));
    return teacherMatches && classMatches;
  });
  const filteredSubjectOptions = filteredSubjects.map((subject) => ({
    subject,
    label: getScheduleSubjectLabel(subject)
  }));

  return (
    <section className="page-stack">
      <PageHeader
        title={editable ? "Редактор расписания" : teacherView ? "Расписание учителя" : "Расписание школы"}
        subtitle={weekCaption}
        text={editable
          ? "Недельная сетка занятий с назначением предметов, преподавателей, домашних заданий и примечаний."
          : teacherView
            ? "Недельная сетка ваших уроков с возможностью переключиться на общее расписание школы."
            : "Директорский режим просмотра недельной сетки уроков, классов и преподавателей."}
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Уроков недели" value={visibleLessons.length} />
        <MetricCard label="Классов" value={classes.length} />
        <MetricCard label="Предметов" value={visibleSubjects.length} />
        <MetricCard label="Слотов звонков" value={slots.length} />
      </div>
      <div className="schedule-toolbar">
        {teacherView && (
          <div className="schedule-scope-switch" role="group" aria-label="Режим просмотра расписания">
            <button className={scheduleScope === "mine" ? "active" : ""} type="button" onClick={() => setScheduleScope("mine")}>Мое расписание</button>
            <button className={scheduleScope === "all" ? "active" : ""} type="button" onClick={() => setScheduleScope("all")}>Все расписание</button>
          </div>
        )}
        <button className="ghost-button compact" onClick={() => setWeekStart(shiftWeek(weekStart, -7))}>Предыдущая</button>
        <div className="schedule-week-picker">
          <Field label="Неделя" type="date" value={weekStart} onChange={(value) => setWeekStart(toIsoDate(getMonday(new Date(value))))} />
        </div>
        <button className="ghost-button compact" onClick={() => setWeekStart(shiftWeek(weekStart, 7))}>Следующая</button>
        <button className="ghost-button compact schedule-fullscreen-toggle" type="button" onClick={() => setIsScheduleFullscreen((current) => !current)}>
          {isScheduleFullscreen ? "Закрыть полный экран" : "На весь экран"}
        </button>
        {editable && (
          <form className="week-copy-form" onSubmit={copyFullScheduleWeek}>
            <div className="schedule-week-picker">
              <Field label="Дата недели" type="date" value={copyWeekTarget} onChange={(value) => setCopyWeekTarget(toIsoDate(getMonday(new Date(value))))} />
            </div>
            <button className="ghost-button compact">Скопировать неделю</button>
          </form>
        )}
        {editable && (
          <form className="class-create-form" onSubmit={createClass}>
            <input id="schedule-new-class" name="scheduleNewClass" value={className} placeholder="Новый класс" onChange={(event) => setClassName(event.target.value)} />
            <button className="primary-button compact">Добавить класс</button>
          </form>
        )}
      </div>
      <div className={`schedule-editor ${isScheduleFullscreen ? "schedule-editor-fullscreen" : ""}`}>
        {isScheduleFullscreen && (
          <div className="schedule-fullscreen-head">
            <div>
              <strong>Расписание</strong>
              <span>{weekCaption}</span>
            </div>
            <button className="ghost-button compact" type="button" onClick={() => setIsScheduleFullscreen(false)}>Закрыть</button>
          </div>
        )}
        <div className="schedule-grid-wrap">
          <table className="schedule-grid-table">
            <thead>
              <tr>
                <th className="schedule-day-head">День</th>
                <th className="schedule-time-head">Время</th>
                <th className="schedule-number-head">№</th>
                {classes.map((classItem) => (
                  <th key={classItem.classId}>{classItem.name}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {groupedSlots.map(({ day, daySlots }) => daySlots.map((slot, slotIndex) => (
                <tr key={`${day}-${slot.scheduleId}`}>
                  {slotIndex === 0 && <td rowSpan={daySlots.length} className="day-cell schedule-day-col">{dayName(day)}</td>}
                  <td className="schedule-time-col">{slot.startTime} - {slot.endTime}</td>
                  <td className="schedule-number-col">{slot.lessonNumber}</td>
                  {classes.map((classItem) => {
                    const lesson = lessonMap.get(`${classItem.classId}_${slot.scheduleId}`) ?? null;
                    const selected = selectedCell?.classItem.classId === classItem.classId
                      && selectedCell?.slot.scheduleId === slot.scheduleId;
                    const lessonSelected = lesson && selectedLessonIdSet.has(String(lesson.lessonId));
                    const cellKey = `${classItem.classId}_${slot.scheduleId}`;
                    const dragTarget = dragOverCell === cellKey;
                    return (
                      <td
                        className={`schedule-cell ${lesson ? "has-lesson" : "empty"} ${selected ? "selected" : ""} ${dragTarget ? "drag-target" : ""}`}
                        key={`${classItem.classId}-${slot.scheduleId}`}
                        onClick={(event) => editable && selectScheduleCell(event, classItem, slot, lesson)}
                        onDoubleClick={() => editable && openLessonEditor(classItem, slot, lesson)}
                        onContextMenu={(event) => openScheduleMenu(event, classItem, slot, lesson)}
                        onDragOver={(event) => {
                          if (!editable) {
                            return;
                          }

                          event.preventDefault();
                          event.dataTransfer.dropEffect = "move";
                          setDragOverCell(cellKey);
                        }}
                        onDragLeave={() => setDragOverCell((current) => current === cellKey ? null : current)}
                        onDrop={async (event) => {
                          event.preventDefault();
                          const draggedLessonId = event.dataTransfer.getData("text/plain");
                          const lessonToMove = draggedLesson
                            ?? (week.lessons ?? []).find((item) => String(item.lessonId) === String(draggedLessonId));
                          setDraggedLesson(null);
                          setDragOverCell(null);
                          if (editable && lessonToMove) {
                            await moveScheduleLesson(lessonToMove, classItem, slot);
                          } else if (editable && (copiedLesson || copiedLessons.length > 0) && !lesson) {
                            await pasteCopiedLesson({ classItem, slot, lesson: null });
                          }
                        }}
                      >
                        {lesson ? (
                          <div
                            className={`schedule-lesson-card ${lessonSelected ? "selected-lesson" : ""}`}
                            draggable={editable}
                            onDragStart={(event) => {
                              event.stopPropagation();
                              setDraggedLesson(lesson);
                              event.dataTransfer.effectAllowed = "move";
                              event.dataTransfer.setData("text/plain", String(lesson.lessonId));
                            }}
                            onDragEnd={() => {
                              setDraggedLesson(null);
                              setDragOverCell(null);
                            }}
                          >
                            <strong>{lesson.subjectName}</strong>
                            <span>{lesson.teacherName}</span>
                            {lesson.homework && <small>ДЗ: {lesson.homework}</small>}
                          </div>
                        ) : "Свободно"}
                      </td>
                    );
                  })}
                </tr>
              )))}
            </tbody>
          </table>
        </div>
        {editable && lessonEditorOpen && selectedCell && (
          <Modal title={selectedCell.lesson ? "Редактирование урока" : "Новый урок"} onClose={() => setLessonEditorOpen(false)}>
            <form className="modal-form schedule-modal-editor" onSubmit={saveLesson}>
              <div className="lesson-editor-summary">
                <span>{selectedCell.classItem.name}</span>
                <strong>{dayName(selectedCell.slot.dayOfWeek)}, {selectedCell.slot.lessonNumber} урок</strong>
                <small>{selectedCell.slot.startTime} - {selectedCell.slot.endTime}</small>
              </div>
              <div className="lesson-editor-grid">
                <label className="field">
                  <span>Преподаватель</span>
                  <input id="schedule-lesson-teacher" name="scheduleLessonTeacher" list={teacherOptionsId} value={lessonForm.teacherName} onChange={(event) => {
                    const teacherName = event.target.value;
                    const teacher = teachers.find((item) => item.fullName === teacherName);
                    const teacherId = teacher?.id ? String(teacher.id) : "";
                    const currentSubject = subjects.find((item) => Number(item.subjectId) === Number(lessonForm.subjectId));
                    const currentSubjectTeacherIds = currentSubject?.teacherIds?.length
                      ? currentSubject.teacherIds
                      : [currentSubject?.teacherId];
                    const currentSubjectClassIds = currentSubject?.classIds ?? [];
                    const subjectStillMatches = currentSubject
                      && currentSubjectTeacherIds.some((item) => Number(item) === Number(teacherId))
                      && (currentSubjectClassIds.length === 0
                        || currentSubjectClassIds.some((classId) => Number(classId) === Number(selectedClassId)));

                    setLessonForm({
                      ...lessonForm,
                      teacherName,
                      teacherId,
                      subjectId: subjectStillMatches ? lessonForm.subjectId : "",
                      subjectName: subjectStillMatches ? lessonForm.subjectName : "",
                      subjectInput: subjectStillMatches ? lessonForm.subjectInput : ""
                    });
                  }} placeholder="Выберите преподавателя" />
                  <datalist id={teacherOptionsId}>
                    {teachers.map((teacher) => <option key={teacher.id} value={teacher.fullName} />)}
                  </datalist>
                </label>
                <label className="field">
                  <span>Предмет</span>
                  <input id="schedule-lesson-subject" name="scheduleLessonSubject" list={subjectOptionsId} value={lessonForm.subjectInput} disabled={!lessonForm.teacherId} onChange={(event) => {
                    const subjectInput = event.target.value;
                    const matchedOption = filteredSubjectOptions.find((item) => item.label === subjectInput);
                    const sameNameSubjects = filteredSubjects.filter((item) => item.name === subjectInput);
                    const subject = matchedOption?.subject ?? (sameNameSubjects.length === 1 ? sameNameSubjects[0] : null);
                    setLessonForm({
                      ...lessonForm,
                      subjectInput,
                      subjectName: subject?.name ?? subjectInput,
                      subjectId: subject?.subjectId ? String(subject.subjectId) : ""
                    });
                  }} placeholder={canCreateSubjectInSchedule ? "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0438\u043b\u0438 \u0432\u0432\u0435\u0434\u0438\u0442\u0435 \u043d\u043e\u0432\u044b\u0439 \u043f\u0440\u0435\u0434\u043c\u0435\u0442" : "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u043f\u0440\u0435\u0434\u043c\u0435\u0442"} />
                  <datalist id={subjectOptionsId}>
                    {filteredSubjectOptions.map(({ subject, label }) => <option key={subject.subjectId} value={label} />)}
                  </datalist>
                </label>
              </div>
              <label className="field">
                <span>Домашнее задание / примечание</span>
                <textarea id="schedule-lesson-homework" name="scheduleLessonHomework" value={lessonForm.homework} onChange={(event) => setLessonForm({ ...lessonForm, homework: event.target.value })} />
              </label>
              <div className="lesson-editor-actions">
                <button className="primary-button compact" disabled={!selectedCell}>Сохранить урок</button>
                {!selectedCell?.lesson && copiedLesson && (
                  <button className="ghost-button compact" type="button" disabled={!selectedCell} onClick={() => pasteCopiedLesson()}>
                    Вставить: {copiedLesson.subjectName}
                  </button>
                )}
              </div>
              {selectedCell?.lesson && (
                <div className="lesson-editor-secondary">
                  <button className="ghost-button compact" type="button" onClick={() => copyScheduleLesson(selectedCell.lesson)}>Копировать урок</button>
                  <button className="ghost-button compact" type="button" onClick={() => cutScheduleLesson(selectedCell.lesson)}>Вырезать</button>
                  <button className="ghost-button compact" type="button" onClick={() => duplicateLessonToNextFreeSlot(selectedCell.lesson)}>Дублировать</button>
                  <button className="danger-button compact" type="button" onClick={() => deleteScheduleLesson()}>Удалить</button>
                </div>
              )}
              {copiedLesson && <small className="row-note">Скопировано: {copiedLesson.subjectName} · {copiedLesson.className}</small>}
            </form>
          </Modal>
        )}
        {editable && scheduleMenu && (
          <div
            className="schedule-context-menu"
            style={{ left: scheduleMenu.x, top: scheduleMenu.y }}
            onClick={(event) => event.stopPropagation()}
          >
            <strong>{scheduleMenu.lesson ? scheduleMenu.lesson.subjectName : "Свободная ячейка"}</strong>
            <small>{scheduleMenu.classItem.name}, {dayName(scheduleMenu.slot.dayOfWeek)}, {scheduleMenu.slot.lessonNumber} урок</small>
            {scheduleMenu.lesson && (
              <>
                <button type="button" onClick={() => copyScheduleLesson(scheduleMenu.lesson)}>
                  <span>Копировать</span>
                  <kbd>Ctrl+C</kbd>
                </button>
                <button type="button" onClick={() => cutScheduleLesson(scheduleMenu.lesson)}>
                  <span>Вырезать</span>
                  <kbd>Ctrl+X</kbd>
                </button>
                <button type="button" onClick={() => duplicateLessonToNextFreeSlot(scheduleMenu.lesson)}>Дублировать</button>
                <button type="button" className="danger-menu-item" onClick={() => deleteScheduleLesson(scheduleMenu.lesson)}>
                  <span>Удалить</span>
                  <kbd>Del</kbd>
                </button>
              </>
            )}
            {!scheduleMenu.lesson && (copiedLesson || copiedLessons.length > 0) && (
              <button type="button" onClick={() => pasteCopiedLesson({ classItem: scheduleMenu.classItem, slot: scheduleMenu.slot, lesson: null })}>
                <span>Вставить</span>
                <kbd>Ctrl+V</kbd>
              </button>
            )}
            {!scheduleMenu.lesson && !copiedLesson && <span>Скопируйте урок, чтобы вставить его сюда</span>}
          </div>
        )}
      </div>
    </section>
  );
}

function TeacherPersonalSchedule({ user }) {
  const [lessons, setLessons] = useState([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    apiRequest(`/teacher/lessons?teacherId=${user.id}`)
      .then((data) => setLessons(data ?? []))
      .catch((error) => setMessage(error.message || "Не удалось загрузить личное расписание"))
      .finally(() => setLoading(false));
  }, [user.id]);

  const upcomingLessons = lessons
    .filter((lesson) => new Date(lesson.date) >= getTodayStart())
    .sort((a, b) => new Date(a.date) - new Date(b.date));

  return (
    <section className="page-stack">
      <PageHeader
        title="Мое расписание"
        subtitle={user.fullName || user.login}
        text="Личная сетка уроков учителя: классы, темы, домашние задания и ближайшие занятия без доступа к общему редактору расписания."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Всего уроков" value={lessons.length} />
        <MetricCard label="Ближайших" value={upcomingLessons.length} />
        <MetricCard label="Классов" value={new Set(lessons.map((item) => item.className)).size} />
        <MetricCard label="Предметов" value={new Set(lessons.map((item) => item.subjectName)).size} />
      </div>
      <DataTable
        title="Ближайшие уроки"
        columns={["Дата", "Класс", "Предмет", "Тема", "Домашнее задание"]}
        rows={upcomingLessons.slice(0, 20).map((lesson) => [
          formatDate(lesson.date),
          lesson.className,
          lesson.subjectName,
          formatLessonTopic(lesson.topic),
          lesson.homework || "—"
        ])}
      />
    </section>
  );
}

function ClassTeacherPage({ role }) {
  const allowed = role === "Учитель" || role === "Администратор";
  const [dashboard, setDashboard] = useState(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!allowed) {
      return;
    }

    setLoading(true);
    apiRequest("/class-teacher/me/dashboard")
      .then(setDashboard)
      .catch((error) => setMessage(error.message || "Не удалось загрузить сводку классного руководителя"))
      .finally(() => setLoading(false));
  }, [allowed]);

  if (!allowed) {
    return <AccessWarning title="Раздел классного руководителя доступен учителю и администратору" />;
  }

  const classes = sortItems(dashboard?.classes ?? [], "className", { className: (item) => classSortValue(item.className) });
  const totalStudents = classes.reduce((sum, item) => sum + item.studentsCount, 0);
  const totalAbsences = classes.reduce((sum, item) => sum + item.absencesCount, 0);
  const totalGrades = classes.reduce((sum, item) => sum + item.gradesCount, 0);

  return (
    <section className="page-stack">
      <PageHeader
        title="Классное руководство"
        subtitle={dashboard?.teacherName || "Сводка по закрепленным классам"}
        text="Здесь собрана общая картина по классам классного руководителя: предметы, оценки, посещаемость, ученики и собственные уроки учителя."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Закрепленных классов" value={classes.length} />
        <MetricCard label="Учеников" value={totalStudents} />
        <MetricCard label="Оценок" value={totalGrades} />
        <MetricCard label="Пропусков" value={totalAbsences} />
      </div>
      {classes.length === 0 ? (
        <section className="table-card">
          <div className="table-title">Назначения не найдены</div>
          <p className="empty-text padded">Администратор еще не привязал учителя как классного руководителя.</p>
        </section>
      ) : classes.map((classItem) => (
        <section className="table-card class-teacher-card" key={classItem.classId}>
          <div className="table-title">{classItem.className}</div>
          <div className="metric-grid compact-metrics">
            <MetricCard label="Учеников" value={classItem.studentsCount} />
            <MetricCard label="Уроков" value={classItem.lessonsCount} />
            <MetricCard label="Средняя" value={formatNumber(classItem.averageGrade)} />
            <MetricCard label="Пропусков" value={classItem.absencesCount} />
          </div>
          <DataTable
            title="Сводка по предметам"
            columns={["Предмет", "Учитель", "Уроков", "Оценок", "Средняя", "Пропуски"]}
            rows={classItem.subjects.map((item) => [
              item.subjectName,
              item.teacherName,
              item.lessonsCount,
              item.gradesCount,
              formatNumber(item.averageGrade),
              item.absencesCount
            ])}
          />
          <DataTable
            title="Ученики класса"
            columns={["ФИО", "Оценок", "Средняя", "Пропуски"]}
            rows={classItem.students.map((item) => [
              item.fullName,
              item.gradesCount,
              formatNumber(item.averageGrade),
              item.absencesCount
            ])}
          />
        </section>
      ))}
      <DataTable
        title="Мои уроки"
        columns={["Дата", "Класс", "Предмет", "Тема", "Домашнее задание"]}
        rows={(dashboard?.ownLessons ?? []).slice(0, 20).map((lesson) => [
          formatDate(lesson.date),
          lesson.className,
          lesson.subjectName,
          formatLessonTopic(lesson.topic),
          lesson.homework || "—"
        ])}
      />
    </section>
  );
}

function BrandPanel() {
  return (
    <section className="brand-card">
      <div className="eyebrow">ClassBook · электронный журнал</div>
      <h2 className="brand-title">Учебный день под контролем.</h2>
      <p className="brand-copy">
        Единое пространство для расписания, оценок, посещаемости и связи школы
        с семьей. Данные доступны по ролям, обновляются в реальном времени и
        остаются защищенными внутри школьной системы.
      </p>
      <div className="role-grid">
        <RoleTile title="Оценки и дневник" text="Цветные отметки, средний балл, домашние задания и история уроков." />
        <RoleTile title="Посещаемость" text="Отметки Н, Б, Ув, Неуд и сводка по ученикам и классам." />
        <RoleTile title="Расписание" text="Актуальная недельная сетка, кабинеты, замены и примечания к урокам." />
        <RoleTile title="Отчетность" text="Контроль заполнения журнала, аналитика и прозрачность действий." />
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

function SmartDataTable({ title, columns, rows }) {
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState({ key: columns[0]?.key ?? "", direction: "asc" });
  const [filters, setFilters] = useState({});

  function getColumnValue(row, column) {
    const value = column.getValue ? column.getValue(row) : row[column.key];
    return value ?? "";
  }

  function toggleSort(column) {
    setSort((current) => ({
      key: column.key,
      direction: current.key === column.key && current.direction === "asc" ? "desc" : "asc"
    }));
  }

  const visibleRows = (rows ?? [])
    .filter((row) => {
      const haystack = columns.map((column) => String(getColumnValue(row, column))).join(" ").toLowerCase();
      const matchesSearch = !query.trim() || haystack.includes(query.trim().toLowerCase());
      const matchesColumns = columns.every((column) => {
        const filterValue = String(filters[column.key] ?? "").trim().toLowerCase();
        return !filterValue || String(getColumnValue(row, column)).toLowerCase().includes(filterValue);
      });
      return matchesSearch && matchesColumns;
    })
    .sort((a, b) => {
      const column = columns.find((item) => item.key === sort.key) ?? columns[0];
      const aValue = column?.sortValue ? column.sortValue(a) : getColumnValue(a, column);
      const bValue = column?.sortValue ? column.sortValue(b) : getColumnValue(b, column);
      const result = column?.type === "number"
        ? Number(aValue ?? 0) - Number(bValue ?? 0)
        : String(aValue ?? "").localeCompare(String(bValue ?? ""), "ru");
      return sort.direction === "asc" ? result : -result;
    });

  return (
    <section className="table-card smart-table-card">
      <div className="table-title">{title}</div>
      <div className="smart-table-tools">
        <Field label="Поиск по таблице" value={query} onChange={setQuery} />
        {columns.map((column) => (
          <Field
            key={column.key}
            label={column.label}
            value={filters[column.key] ?? ""}
            onChange={(value) => setFilters((current) => ({ ...current, [column.key]: value }))}
          />
        ))}
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column.key}>
                  <button className="table-sort-button" type="button" onClick={() => toggleSort(column)}>
                    {column.label}{sort.key === column.key ? (sort.direction === "asc" ? " ↑" : " ↓") : ""}
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visibleRows.length === 0 ? (
              <tr>
                <td colSpan={columns.length}>Данных пока нет</td>
              </tr>
            ) : visibleRows.map((row, index) => (
              <tr key={`${title}-${index}`}>
                {columns.map((column) => (
                  <td data-label={column.label} key={`${title}-${index}-${column.key}`}>
                    {column.render ? column.render(row) : getColumnValue(row, column)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DataTable({ title, columns, rows, className = "" }) {
  return (
    <section className={`table-card ${className}`}>
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
                  <td data-label={columns[cellIndex]} key={`${title}-${index}-${cellIndex}`}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Modal({ title, children, onClose }) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <section className="modal-card">
        <div className="modal-header">
          <h3>{title}</h3>
          <button type="button" onClick={onClose} aria-label="Закрыть">×</button>
        </div>
        {children}
      </section>
    </div>
  );
}

function CardGrid({ title, items }) {
  return (
    <section className="table-card">
      <div className="table-title">{title}</div>
      <div className="card-grid">
        {items.length === 0 ? (
          <p className="empty-text">Данных пока нет</p>
        ) : items.map((item, index) => (
          <article className="info-card" key={`${title}-${index}`}>
            <span>{item.meta}</span>
            <strong>{item.title}</strong>
            <p>{item.text}</p>
          </article>
        ))}
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
        text="У вашей учетной записи нет прав на просмотр этого раздела. Обратитесь к администратору, если доступ нужен для работы."
      />
    </section>
  );
}

function StatusLine({ loading, message }) {
  if (loading) {
    return <p className="status-line">Загружаем данные...</p>;
  }

  if (message) {
    const isError = /не удалось|недоступно|ошиб|не хватает прав/i.test(message);
    return <p className={`status-line ${isError ? "status-error" : "status-success"}`} role={isError ? "alert" : "status"}>{message}</p>;
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
  const inputId = useId();
  const inputName = `field-${inputId.replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const [localValue, setLocalValue] = useState(value ?? "");
  const shouldDebounce = type === "date";

  useEffect(() => {
    setLocalValue(value ?? "");
  }, [value]);

  useEffect(() => {
    if (!shouldDebounce || localValue === (value ?? "")) {
      return undefined;
    }

    const timer = window.setTimeout(() => onChange(localValue), 3000);
    return () => window.clearTimeout(timer);
  }, [localValue, onChange, shouldDebounce, value]);

  function handleChange(nextValue) {
    setLocalValue(nextValue);
    if (!shouldDebounce) {
      onChange(nextValue);
    }
  }

  function flushDateValue() {
    if (shouldDebounce && localValue !== (value ?? "")) {
      onChange(localValue);
    }
  }

  return (
    <div className="field">
      <label htmlFor={inputId}>{label}</label>
      <input
        id={inputId}
        name={inputName}
        type={type}
        value={localValue}
        autoComplete={autoComplete}
        onChange={(event) => handleChange(event.target.value)}
        onBlur={flushDateValue}
      />
    </div>
  );
}

function isPlaceholderTopic(value) {
  const text = String(value || "").trim().toLowerCase();
  return !text
    || text === "????"
    || text.includes("???")
    || text.includes("будет указана")
    || text.includes("не указана");
}

function formatLessonTopic(value) {
  return isPlaceholderTopic(value) ? "Тема будет указана позже" : value;
}

function formatNumber(value) {
  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric) ? numeric.toFixed(1) : "0.0";
}

function calculateAverage(values) {
  const numericValues = values.map(Number).filter(Number.isFinite);
  return numericValues.length
    ? numericValues.reduce((sum, value) => sum + value, 0) / numericValues.length
    : 0;
}

function calculateMedian(values) {
  const numericValues = values.map(Number).filter(Number.isFinite).sort((left, right) => left - right);
  if (numericValues.length === 0) {
    return 0;
  }

  const middle = Math.floor(numericValues.length / 2);
  return numericValues.length % 2
    ? numericValues[middle]
    : (numericValues[middle - 1] + numericValues[middle]) / 2;
}

function sortItems(items, sortKey, selectors) {
  const selector = selectors[sortKey] || Object.values(selectors)[0];
  return [...items].sort((left, right) =>
    String(selector(left)).localeCompare(String(selector(right)), "ru", {
      numeric: true,
      sensitivity: "base"
    })
  );
}

function classSortValue(value) {
  const normalized = String(value || "").trim().toUpperCase().replace(/[«»"]/g, "").replace(/\s+/g, "");
  const match = normalized.match(/^(\d+)([А-ЯЁA-Z]*)$/);

  if (!match) {
    return `999|${normalized}`;
  }

  return `${String(Number(match[1])).padStart(3, "0")}|${match[2] || ""}`;
}

function generateTemporaryPassword() {
  const bytes = new Uint8Array(6);
  crypto.getRandomValues(bytes);
  return `Cb-${Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("").toUpperCase()}!`;
}

function formatDate(value) {
  if (!value) {
    return "—";
  }

  const date = parseLocalDate(value);
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleDateString("ru-RU");
}

function formatTimeRange(item) {
  if (item.startTime && item.endTime) {
    return `${item.startTime} - ${item.endTime}`;
  }

  if (item.time) {
    return item.time;
  }

  return "—";
}

function attendanceLabel(status) {
  const numeric = Number(status);
  if (numeric === 1) {
    return "Присутствовал";
  }

  if (numeric === 0) {
    return "Не явился";
  }

  if (numeric === 2) {
    return "Опоздал";
  }

  return "Присутствовал";
}

function attendanceClassName(status) {
  const numeric = Number(status);
  if (numeric === 0) {
    return "attendance-absent";
  }

  if (numeric === 2) {
    return "attendance-late";
  }

  return "attendance-present";
}

function toIsoDate(date) {
  if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
    return new Date().toISOString().slice(0, 10);
  }

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function parseLocalDate(value) {
  if (value instanceof Date) {
    return value;
  }

  const text = String(value || "");
  const match = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (match) {
    return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  }

  return new Date(value);
}

function getDateKey(value) {
  return toIsoDate(parseLocalDate(value));
}

function getMonday(date) {
  const result = parseLocalDate(date);
  const day = result.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  result.setDate(result.getDate() + diff);
  return result;
}

function getTodayStart() {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return today;
}

function shiftWeek(weekStart, days) {
  const date = parseLocalDate(weekStart);
  date.setDate(date.getDate() + days);
  return toIsoDate(getMonday(date));
}

function getWeekCaption(weekStart) {
  const start = parseLocalDate(weekStart);
  const end = new Date(start);
  end.setDate(end.getDate() + 5);
  return `${formatDate(start)} - ${formatDate(end)}`;
}

function getInitialLearningWeekStart(schedule = [], attendance = []) {
  const todayWeek = toIsoDate(getMonday(new Date()));
  const allItems = [...schedule, ...attendance].filter((item) => item?.date);
  if (allItems.some((item) => toIsoDate(getMonday(parseLocalDate(item.date))) === todayWeek)) {
    return todayWeek;
  }

  const upcoming = allItems
    .map((item) => parseLocalDate(item.date))
    .filter((date) => !Number.isNaN(date.getTime()) && date >= getTodayStart())
    .sort((left, right) => left - right)[0];

  if (upcoming) {
    return toIsoDate(getMonday(upcoming));
  }

  const latest = allItems
    .map((item) => parseLocalDate(item.date))
    .filter((date) => !Number.isNaN(date.getTime()))
    .sort((left, right) => right - left)[0];

  return latest ? toIsoDate(getMonday(latest)) : todayWeek;
}

function getLearningWeekDays(weekStart) {
  return Array.from({ length: 6 }, (_, index) => {
    const date = parseLocalDate(weekStart);
    date.setDate(date.getDate() + index);
    return toIsoDate(date);
  });
}

function getDayIndex(dateValue) {
  const day = parseLocalDate(dateValue).getDay();
  return day === 0 ? 6 : day - 1;
}

function filterItemsByWeek(items, weekStart) {
  const start = parseLocalDate(weekStart);
  const end = new Date(start);
  end.setDate(end.getDate() + 6);
  return (items ?? []).filter((item) => {
    const date = parseLocalDate(item.date);
    return !Number.isNaN(date.getTime()) && date >= start && date < end;
  });
}

function comparePortalLessons(left, right) {
  const leftDate = parseLocalDate(left.date);
  const rightDate = parseLocalDate(right.date);
  const dateDiff = leftDate - rightDate;
  if (dateDiff !== 0) {
    return dateDiff;
  }

  return Number(left.lessonNumber ?? 999) - Number(right.lessonNumber ?? 999);
}

function getTodaySchedule(schedule) {
  const today = toIsoDate(new Date());
  return (schedule ?? [])
    .filter((lesson) => getDateKey(lesson.date) === today)
    .sort(comparePortalLessons);
}

function getFilteredAttendance(attendance, filter, sort) {
  const filtered = (attendance ?? []).filter((item) => {
    const status = Number(item.status ?? 1);
    if (filter === "absent") {
      return status === 0;
    }

    if (filter === "late") {
      return status === 2;
    }

    if (filter === "problem") {
      return status !== 1;
    }

    return true;
  });

  return filtered.sort((left, right) => {
    if (sort === "date-asc") {
      return parseLocalDate(left.date) - parseLocalDate(right.date);
    }

    if (sort === "subject") {
      return String(left.subject || left.subjectName || "").localeCompare(String(right.subject || right.subjectName || ""), "ru", {
        numeric: true,
        sensitivity: "base"
      });
    }

    return parseLocalDate(right.date) - parseLocalDate(left.date);
  });
}

function getDirectorPeriod(mode) {
  const today = getTodayStart();
  const end = toIsoDate(today);

  if (mode === "day") {
    return { start: end, end };
  }

  if (mode === "month") {
    const start = new Date(today);
    start.setDate(start.getDate() - 29);
    return { start: toIsoDate(start), end };
  }

  const start = getMonday(today);
  return { start: toIsoDate(start), end };
}

function getLessonDateForSlot(weekStart, slot) {
  const date = parseLocalDate(weekStart);
  date.setDate(date.getDate() + Number(slot.dayOfWeek ?? 0));
  return toIsoDate(date);
}

function buildScheduleLessonMap(lessons) {
  const map = new Map();
  lessons.forEach((lesson) => {
    if (lesson.classId && lesson.scheduleId) {
      map.set(`${lesson.classId}_${lesson.scheduleId}`, lesson);
    }
  });
  return map;
}

function groupSlotsByDay(slots) {
  const groups = new Map();
  slots.forEach((slot) => {
    if (!groups.has(slot.dayOfWeek)) {
      groups.set(slot.dayOfWeek, []);
    }
    groups.get(slot.dayOfWeek).push(slot);
  });

  return [...groups.entries()]
    .sort(([a], [b]) => a - b)
    .map(([day, daySlots]) => ({
      day,
      daySlots: daySlots.sort((a, b) => a.lessonNumber - b.lessonNumber)
    }));
}

function getScheduleSubjectLabel(subject) {
  const teacher = subject.teacherName ? ` - ${subject.teacherName}` : "";
  const classes = subject.classes ? ` - ${subject.classes}` : "";
  return `${subject.name}${teacher}${classes}`;
}

function getAdminSubjectLabel(subject) {
  return [subject.name, subject.teacherName].filter(Boolean).join(" - ");
}

function dayName(day) {
  return ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"][Number(day)] ?? String(day);
}

createRoot(document.getElementById("root")).render(<App />);
