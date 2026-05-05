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
      return <TeacherPage role={role} user={user} />;
    case "student":
      return <StudentPage role={role} />;
    case "parent":
      return <ParentPage role={role} />;
    case "schedule":
      return <SchedulePage role={role} />;
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

function TeacherPage({ role, user }) {
  const allowed = role === "Учитель" || role === "Администратор";
  const [classes, setClasses] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [lessons, setLessons] = useState([]);
  const [students, setStudents] = useState([]);
  const [gradesByLesson, setGradesByLesson] = useState({});
  const [attendanceByLesson, setAttendanceByLesson] = useState({});
  const [selectedClassId, setSelectedClassId] = useState("");
  const [selectedLessonId, setSelectedLessonId] = useState("");
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
      const [classData, subjectData, lessonData] = await Promise.all([
        apiRequest(`/teacher/classes?teacherId=${user.id}`),
        apiRequest(`/teacher/subjects?teacherId=${user.id}`),
        apiRequest(`/teacher/lessons?teacherId=${user.id}`)
      ]);
      setClasses(classData ?? []);
      setSubjects(subjectData ?? []);
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
  }, [allowed]);

  useEffect(() => {
    if (!selectedClassId) {
      setStudents([]);
      return;
    }

    apiRequest(`/teacher/classes/${selectedClassId}/students`)
      .then((data) => setStudents(data ?? []))
      .catch((error) => setMessage(error.message || "Не удалось загрузить учеников класса"));
  }, [selectedClassId]);

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

    try {
      await apiRequest("/teacher/grades", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(selectedLessonId),
          studentId,
          value: Number(value)
        })
      });
      await loadLessonMarks(selectedLessonId);
    } catch (error) {
      setMessage(error.message || "Не удалось сохранить оценку");
    }
  }

  async function saveAttendance(studentId, status) {
    if (!selectedLessonId) {
      return;
    }

    try {
      await apiRequest("/teacher/attendance", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(selectedLessonId),
          studentId,
          status: Number(status)
        })
      });
      await loadLessonMarks(selectedLessonId);
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
  const lessonGrades = gradesByLesson[selectedLessonId] ?? [];
  const lessonAttendance = attendanceByLesson[selectedLessonId] ?? [];

  return (
    <section className="page-stack">
      <PageHeader
        title="Кабинет учителя"
        subtitle="Уроки, журнал, оценки и посещаемость"
        text="Страница работает как самостоятельный React-интерфейс: загружает классы и предметы учителя, создает уроки и позволяет заполнять журнал выбранного урока."
      />
      <StatusLine loading={loading} message={message} />
      <form className="inline-form lesson-form" onSubmit={createLesson}>
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
      </form>
      <div className="split-grid">
        <section className="table-card">
          <div className="table-title">Выбор класса и урока</div>
          <div className="picker-panel">
            <label className="field">
              <span>Класс</span>
              <select value={selectedClassId} onChange={(event) => {
                setSelectedClassId(event.target.value);
                setSelectedLessonId("");
              }}>
                <option value="">Все классы</option>
                {classes.map((item) => (
                  <option key={item.classId ?? item.id} value={item.classId ?? item.id}>{item.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Урок</span>
              <select value={selectedLessonId} onChange={(event) => loadLessonMarks(event.target.value)}>
                <option value="">Выберите урок</option>
                {classLessons.map((lesson) => (
                  <option key={lesson.lessonId} value={lesson.lessonId}>
                    {lesson.subjectName} · {lesson.topic} · {formatDate(lesson.date)}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </section>
        <DataTable
          title="Последние уроки"
          columns={["Дата", "Предмет", "Класс", "Тема", "ДЗ"]}
          rows={lessons.slice(0, 8).map((lesson) => [
            formatDate(lesson.date),
            lesson.subjectName,
            lesson.className,
            lesson.topic,
            lesson.homework || "—"
          ])}
        />
      </div>
      <section className="table-card">
        <div className="table-title">Журнал выбранного урока</div>
        <div className="journal-list">
          {students.length === 0 ? (
            <p className="empty-text">Выберите класс, чтобы увидеть учеников.</p>
          ) : students.map((student) => {
            const grade = lessonGrades.find((item) => item.studentId === student.studentId);
            const attendance = lessonAttendance.find((item) => item.studentId === student.studentId);
            return (
              <div className="journal-row" key={student.studentId}>
                <strong>{student.lastName} {student.firstName}</strong>
                <select defaultValue="" onChange={(event) => saveGrade(student.studentId, event.target.value)}>
                  <option value="">{grade ? `Оценка: ${grade.value}` : "Поставить оценку"}</option>
                  {[2, 3, 4, 5].map((value) => (
                    <option key={value} value={value}>{value}</option>
                  ))}
                </select>
                <select value={attendance?.status ?? ""} onChange={(event) => saveAttendance(student.studentId, event.target.value)}>
                  <option value="">Не отмечено</option>
                  <option value="1">Присутствовал</option>
                  <option value="0">Отсутствовал</option>
                  <option value="2">Опоздал</option>
                </select>
              </div>
            );
          })}
        </div>
      </section>
    </section>
  );
}

function StudentPage({ role }) {
  const allowed = role === "Ученик" || role === "Администратор";
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
  }, [allowed]);

  if (!allowed) {
    return <AccessWarning title="Кабинет ученика доступен ученику и администратору" />;
  }

  return (
    <LearningPage
      title="Кабинет ученика"
      subtitle={info ? `${info.name || "Ученик"} · ${info.className || "класс не указан"}` : "Учебная информация"}
      description="Здесь собраны расписание, оценки, домашние задания и посещаемость текущего ученика."
      schedule={schedule}
      grades={grades}
      homework={homework}
      attendance={attendance}
      loading={loading}
      message={message}
    />
  );
}

function ParentPage({ role }) {
  const allowed = role === "Родитель" || role === "Администратор";
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
  }, [allowed]);

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
        title="Кабинет родителя"
        subtitle={selectedStudent ? `${selectedStudent.firstName} ${selectedStudent.lastName}` : "Выберите ученика"}
        text="Родитель видит только привязанных учеников. Данные подгружаются из отдельных родительских API без старой страницы."
      />
      <StatusLine loading={loading} message={message} />
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

function LearningPage({ title, subtitle, description, schedule, grades, homework, attendance, loading, message }) {
  return (
    <section className="page-stack">
      <PageHeader title={title} subtitle={subtitle} text={description} />
      <StatusLine loading={loading} message={message} />
      <LearningSections schedule={schedule} grades={grades} homework={homework} attendance={attendance} />
    </section>
  );
}

function LearningSections({ schedule, grades, homework, attendance }) {
  const averageGrade = grades.length
    ? grades.reduce((sum, item) => sum + Number(item.value ?? 0), 0) / grades.length
    : 0;
  const present = attendance.filter((item) => Number(item.status) === 1).length;

  return (
    <>
      <div className="metric-grid">
        <MetricCard label="Уроков в расписании" value={schedule.length} />
        <MetricCard label="Оценок" value={grades.length} />
        <MetricCard label="Средний балл" value={formatNumber(averageGrade)} />
        <MetricCard label="Присутствий" value={present} />
      </div>
      <DataTable
        title="Расписание"
        columns={["Дата", "Время", "Предмет", "Тема", "Учитель"]}
        rows={schedule.slice(0, 12).map((lesson) => [
          formatDate(lesson.date),
          formatTimeRange(lesson),
          lesson.subject || lesson.subjectName || lesson.name || "—",
          lesson.topic || "—",
          lesson.teacher || lesson.teacherName || "—"
        ])}
      />
      <DataTable
        title="Оценки"
        columns={["Дата", "Предмет", "Тема", "Оценка"]}
        rows={grades.slice(0, 16).map((grade) => [
          formatDate(grade.date),
          grade.subject || grade.subjectName || "—",
          grade.topic || "—",
          grade.value
        ])}
      />
      <CardGrid
        title="Домашние задания"
        items={homework.slice(0, 8).map((item) => ({
          title: item.subject || item.subjectName || item.name || "Предмет",
          text: item.homework || item.task || "Домашнее задание не указано",
          meta: `${formatDate(item.date)} · ${item.topic || "без темы"}`
        }))}
      />
      <DataTable
        title="Посещаемость"
        columns={["Дата", "Предмет", "Статус"]}
        rows={attendance.slice(0, 16).map((item) => [
          formatDate(item.date),
          item.subject || item.subjectName || item.name || "—",
          attendanceLabel(item.status)
        ])}
      />
    </>
  );
}

function SchedulePage({ role }) {
  const allowed = role === "Менеджер расписания" || role === "Администратор" || role === "Директор";
  const [week, setWeek] = useState([]);
  const [metadata, setMetadata] = useState(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!allowed) {
      return;
    }

    setLoading(true);
    Promise.allSettled([
      apiRequest("/schedule/week"),
      apiRequest("/schedule/editor/metadata")
    ])
      .then(([weekResult, metadataResult]) => {
        if (weekResult.status === "fulfilled") {
          const value = weekResult.value;
          setWeek(Array.isArray(value) ? value : Object.values(value ?? {}).flat());
        }

        if (metadataResult.status === "fulfilled") {
          setMetadata(metadataResult.value);
        }

        const rejected = [weekResult, metadataResult].find((item) => item.status === "rejected");
        if (rejected) {
          setMessage(rejected.reason?.message || "Часть данных расписания не загрузилась");
        }
      })
      .finally(() => setLoading(false));
  }, [allowed]);

  if (!allowed) {
    return <AccessWarning title="Расписание доступно менеджеру расписания, директору и администратору" />;
  }

  return (
    <section className="page-stack">
      <PageHeader
        title="Расписание"
        subtitle="Неделя и справочники"
        text="React-страница показывает недельную сетку и справочную информацию для дальнейшего полноценного редактора расписания."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Записей недели" value={week.length} />
        <MetricCard label="Классов" value={metadata?.classes?.length ?? 0} />
        <MetricCard label="Предметов" value={metadata?.subjects?.length ?? 0} />
        <MetricCard label="Учителей" value={metadata?.teachers?.length ?? 0} />
      </div>
      <DataTable
        title="Недельное расписание"
        columns={["День", "Время", "Класс", "Предмет", "Учитель", "Кабинет"]}
        rows={week.slice(0, 30).map((item) => [
          item.dayOfWeekName || item.dayOfWeek || formatDate(item.date),
          formatTimeRange(item),
          item.className || item.class || "—",
          item.subjectName || item.subject || "—",
          item.teacherName || item.teacher || "—",
          item.room || item.classroom || "—"
        ])}
      />
      <CardGrid
        title="Справочники"
        items={[
          { title: "Классы", text: (metadata?.classes ?? []).map((item) => item.name).join(", ") || "Нет данных", meta: "Для выбора класса" },
          { title: "Предметы", text: (metadata?.subjects ?? []).map((item) => item.name).join(", ") || "Нет данных", meta: "Для сетки уроков" },
          { title: "Учителя", text: (metadata?.teachers ?? []).map((item) => item.fullName || item.name).join(", ") || "Нет данных", meta: "Для назначения преподавателя" }
        ]}
      />
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

function formatDate(value) {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
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
    return "Отсутствовал";
  }

  if (numeric === 2) {
    return "Опоздал";
  }

  return "Не отмечено";
}

createRoot(document.getElementById("root")).render(<App />);
