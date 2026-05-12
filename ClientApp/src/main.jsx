import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";
import { apiBase, apiRequest } from "./api.js";
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

function AuthenticatedShell({ route, user, onLogout }) {
  const role = getRole(user);
  const navItems = getNavItemsForRole(role);

  return (
    <main className="app-shell app-layout">
      <aside className="side-panel">
        <div>
          <div className="eyebrow">ClassBook</div>
          <h1>Электронный журнал</h1>
          <p>{user.fullName || user.login}</p>
          <p className="role-badge">{role}</p>
        </div>
        <nav className="app-nav">
          {navItems.map((item) => (
            <NavLink key={item.route} route={item.route} current={route}>{item.label}</NavLink>
          ))}
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

function getNavItemsForRole(role) {
  const common = [{ route: "home", label: "Главная" }];
  const byRole = {
    "Администратор": [
      { route: "admin", label: "Администрирование" },
      { route: "director", label: "Отчеты директора" },
      { route: "teacher", label: "Журнал учителя" },
      { route: "student", label: "Дневник ученика" },
      { route: "parent", label: "Кабинет родителя" },
      { route: "schedule", label: "Расписание" }
    ],
    "Директор": [
      { route: "director", label: "Отчеты директора" }
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
        return <TeacherPersonalSchedule user={user} />;
      }
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
        text="Быстрый доступ к основным разделам электронного журнала, доступным вашей учетной записи."
      />
      <div className="module-grid">
        <ModuleCard
          href={primaryTarget}
          title="Открыть мой раздел"
          text="Основной рабочий экран для текущей роли."
        />
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
    teacherId: ""
  });
  const [editingSubject, setEditingSubject] = useState(null);
  const [subjectClassAssignmentForm, setSubjectClassAssignmentForm] = useState({
    classId: "",
    teacherId: ""
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
      setSubjects(subjectsData ?? []);
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
    await navigator.clipboard.writeText(text);
    setMessage("Данные доступа скопированы");
  }

  async function copyBatchAccess(items) {
    const text = items
      .map((item) => `${item.fullName}\nЛогин: ${item.login}\nВременный пароль: ${item.temporaryPassword}\n`)
      .join("\n");
    await navigator.clipboard.writeText(text);
    setMessage("Список доступов скопирован");
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
    if (!subjectForm.name.trim() || !subjectForm.teacherId) {
      setMessage("Укажите название предмета и выберите учителя");
      return;
    }

    try {
      await apiRequest("/subjects", {
        method: "POST",
        body: JSON.stringify({
          name: subjectForm.name.trim(),
          teacherId: Number(subjectForm.teacherId)
        })
      });
      setSubjectForm({ name: "", teacherId: "" });
      setMessage("Предмет создан");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось создать предмет");
    }
  }

  async function updateSubject(event) {
    event.preventDefault();
    if (!editingSubject || !editingSubject.name.trim() || !editingSubject.teacherId) {
      setMessage("Укажите название предмета и выберите учителя");
      return;
    }

    try {
      await apiRequest(`/subjects/${editingSubject.subjectId}`, {
        method: "PUT",
        body: JSON.stringify({
          name: editingSubject.name.trim(),
          teacherId: Number(editingSubject.teacherId)
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
    if (!window.confirm("Удалить предмет? Это действие удалит все связанные уроки.")) {
      return;
    }

    try {
      await apiRequest(`/subjects/${subjectId}`, {
        method: "DELETE"
      });
      setMessage("Предмет удален");
      await loadAdminData();
    } catch (error) {
      setMessage(error.message || "Не удалось удалить предмет");
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
    <section className="page-stack">
      <PageHeader
        title="Администрирование"
        subtitle="Пользователи, роли и ученики"
        text="Управление учетными записями, ролями, карточками учеников и выдачей доступа к электронному журналу."
      />
      <StatusLine loading={loading} message={message} />
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
      <form className={`inline-form attach-form admin-section ${adminTab === "access" ? "active" : ""}`} onSubmit={attachStudentAccount}>
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
      <section className={`table-card admin-section ${adminTab === "subjects" ? "active" : ""}`}>
        <div className="table-title">Предметы и учителя</div>
        <form className="inline-form attach-form" onSubmit={createSubject}>
          <label className="field">
            <span>Название предмета</span>
            <input type="text" value={subjectForm.name} onChange={(event) => setSubjectForm({ ...subjectForm, name: event.target.value })} />
          </label>
          <label className="field">
            <span>Учитель</span>
            <select value={subjectForm.teacherId} onChange={(event) => setSubjectForm({ ...subjectForm, teacherId: event.target.value })}>
              <option value="">Выберите учителя</option>
              {teacherUsers.map((item) => (
                <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
              ))}
            </select>
          </label>
          <button className="primary-button">Создать предмет</button>
        </form>
        <DataTable
          title={`Предметы (${subjects.length})`}
          className="nested-table"
          columns={["Предмет", "Учитель", "Действие"]}
          rows={subjects.map((item) => [
            item.name,
            item.teacherName,
            <div key={item.subjectId} style={{ display: "flex", gap: "8px" }}>
              <button className="table-action" type="button" onClick={() => setEditingSubject(item)}>Изменить</button>
              <button className="table-action" type="button" onClick={() => deleteSubject(item.subjectId)}>Удалить</button>
            </div>
          ])}
        />
      </section>
      {editingSubject && adminTab === "subjects" && (
        <Modal title="Редактирование предмета" onClose={() => setEditingSubject(null)}>
          <form className="modal-form" onSubmit={updateSubject}>
            <Field label="Название предмета" value={editingSubject.name} onChange={(value) => setEditingSubject({ ...editingSubject, name: value })} />
            <label className="field">
              <span>Учитель</span>
              <select value={editingSubject.teacherId} onChange={(event) => setEditingSubject({ ...editingSubject, teacherId: event.target.value })}>
                {teacherUsers.map((item) => (
                  <option key={item.id} value={item.id}>{item.fullName} · {item.login}</option>
                ))}
              </select>
            </label>
            <button className="primary-button">Сохранить предмет</button>
            <button className="ghost-button compact" type="button" onClick={() => setEditingSubject(null)}>Отмена</button>
          </form>
        </Modal>
      )}
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
          <button className="primary-button">Назначить</button>
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
            <button className="table-action" type="button" onClick={() => resetUserPassword(item)}>Временный пароль</button>
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

  const classRows = sortItems(summary?.classSummary ?? [], "className", { className: (item) => classSortValue(item.className) });
  const attendanceRows = sortItems(attendance?.statistics ?? [], "className", { className: (item) => classSortValue(item.className) });
  const dailyRows = daily?.report ?? [];

  return (
    <section className="page-stack">
      <PageHeader
        title="Отчеты директора"
        subtitle="Аналитика учебного процесса"
        text="Сводная аналитика по классам, посещаемости, заполнению журнала и действиям пользователей."
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
      setClasses(sortItems(classData ?? [], "name", { name: (item) => classSortValue(item.name) }));
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

  useEffect(() => {
    if (!selectedClassId || lessons.length === 0) {
      return;
    }

    const selectedClassForLoad = classes.find((item) => String(item.classId ?? item.id) === String(selectedClassId));
    const lessonsToLoad = lessons
      .filter((lesson) => String(lesson.classId ?? "") === String(selectedClassId) || lesson.className === selectedClassForLoad?.name)
      .slice(0, 10);

    if (lessonsToLoad.length === 0) {
      return;
    }

    Promise.all(lessonsToLoad.map(async (lesson) => {
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
      .catch((error) => setMessage(error.message || "Не удалось загрузить журнал класса"));
  }, [selectedClassId, lessons, classes]);

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
      await apiRequest("/teacher/grades", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(lessonId),
          studentId,
          value: Number(value)
        })
      });
      await loadLessonMarks(lessonId);
    } catch (error) {
      setMessage(error.message || "Не удалось сохранить оценку");
    }
  }

  async function deleteGrade(gradeId, lessonId = selectedLessonId) {
    try {
      await apiRequest(`/teacher/grades/${gradeId}`, { method: "DELETE" });
      if (lessonId) {
        await loadLessonMarks(lessonId);
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

    try {
      await apiRequest("/teacher/attendance", {
        method: "POST",
        body: JSON.stringify({
          lessonId: Number(lessonId),
          studentId,
          status: Number(status)
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
  const journalLessons = selectedClassId ? classLessons.slice(0, 10) : [];
  const allVisibleGrades = journalLessons.flatMap((lesson) => gradesByLesson[lesson.lessonId] ?? []);
  const allVisibleAttendance = journalLessons.flatMap((lesson) => attendanceByLesson[lesson.lessonId] ?? []);
  const classAverage = calculateAverage(allVisibleGrades.map((item) => item.value));
  const presentCount = allVisibleAttendance.filter((item) => Number(item.status) === 1).length;

  return (
    <section className="page-stack">
      <PageHeader
        title="Кабинет учителя"
        subtitle="Уроки, журнал, оценки и посещаемость"
        text="Рабочее место преподавателя для планирования уроков, заполнения оценок и отметок посещаемости."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Уроков" value={lessons.length} />
        <MetricCard label="Учеников в классе" value={students.length} />
        <MetricCard label="Средняя оценка" value={formatNumber(classAverage)} />
        <MetricCard label="Присутствуют" value={presentCount} />
      </div>
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
        <div className="table-title">Журнал класса</div>
        {students.length === 0 || journalLessons.length === 0 ? (
          <p className="empty-text padded">Выберите класс, чтобы увидеть сетку журнала.</p>
        ) : (
          <div className="gradebook-wrap">
            <table className="gradebook-table">
              <thead>
                <tr>
                  <th className="student-sticky">ФИО ученика</th>
                  {journalLessons.map((lesson, index) => (
                    <th key={lesson.lessonId}>
                      <span>{index + 1} урок</span>
                      <small>{lesson.subjectName}</small>
                    </th>
                  ))}
                  <th>Средняя</th>
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
                        return (
                          <td key={`${student.studentId}-${lesson.lessonId}`} className="gradebook-cell">
                            <div className="grade-stack">
                              {grades.map((grade) => (
                                <button
                                  className={`grade-pill grade-${grade.value}`}
                                  key={grade.gradeId}
                                  title="Удалить оценку"
                                  type="button"
                                  onClick={() => deleteGrade(grade.gradeId, lesson.lessonId)}
                                >
                                  {grade.value}
                                </button>
                              ))}
                              <select defaultValue="" onChange={(event) => saveGradeForLesson(lesson.lessonId, student.studentId, event.target.value)}>
                                <option value="">+</option>
                                {[2, 3, 4, 5].map((value) => (
                                  <option key={value} value={value}>{value}</option>
                                ))}
                              </select>
                            </div>
                            <select className="attendance-select" value={attendance?.status ?? ""} onChange={(event) => saveAttendanceForLesson(lesson.lessonId, student.studentId, event.target.value)}>
                              <option value="">—</option>
                              <option value="1">П</option>
                              <option value="0">Н</option>
                              <option value="2">Б</option>
                              <option value="3">Ув</option>
                              <option value="4">Неуд</option>
                            </select>
                          </td>
                        );
                      })}
                      <td>{studentGrades.length ? formatNumber(calculateAverage(studentGrades.map((grade) => grade.value))) : "—"}</td>
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
        text="Доступ к успеваемости, посещаемости, расписанию и домашним заданиям привязанных учеников."
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
  const allowed = role === "Менеджер расписания" || role === "Администратор";
  const editable = role === "Менеджер расписания" || role === "Администратор";
  const [weekStart, setWeekStart] = useState(() => toIsoDate(getMonday(new Date())));
  const [week, setWeek] = useState({ lessons: [] });
  const [metadata, setMetadata] = useState(null);
  const [selectedCell, setSelectedCell] = useState(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [className, setClassName] = useState("");
  const [lessonForm, setLessonForm] = useState({
    subjectId: "",
    teacherId: "",
    homework: ""
  });

  async function loadScheduleEditor() {
    setLoading(true);
    setMessage("");
    try {
      const [weekData, metadataData] = await Promise.all([
        apiRequest(`/schedule/editor/week?weekStart=${weekStart}`),
        apiRequest("/schedule/editor/metadata")
      ]);
      setWeek(weekData ?? { lessons: [] });
      setMetadata(metadataData);
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

  function selectCell(classItem, slot, lesson) {
    setSelectedCell({ classItem, slot, lesson });
    const subjectId = lesson?.subjectId ?? "";
    const teacherId = lesson?.teacherId
      ?? metadata?.subjects?.find((item) => Number(item.subjectId) === Number(subjectId))?.teacherId
      ?? "";
    setLessonForm({
      subjectId,
      teacherId,
      homework: lesson?.homework ?? ""
    });
  }

  async function saveLesson(event) {
    event.preventDefault();
    if (!selectedCell || !lessonForm.subjectId || !lessonForm.teacherId) {
      setMessage("Выберите ячейку, предмет и преподавателя");
      return;
    }

    const payload = {
      classId: selectedCell.classItem.classId,
      subjectId: Number(lessonForm.subjectId),
      teacherId: Number(lessonForm.teacherId),
      scheduleId: selectedCell.slot.scheduleId,
      date: getLessonDateForSlot(weekStart, selectedCell.slot),
      homework: lessonForm.homework.trim()
    };

    try {
      if (selectedCell.lesson) {
        await apiRequest(`/schedule/editor/lesson/${selectedCell.lesson.lessonId}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });
        setMessage("Урок обновлен");
      } else {
        await apiRequest("/schedule/editor/lesson", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        setMessage("Урок добавлен в сетку");
      }
      setSelectedCell(null);
      await loadScheduleEditor();
    } catch (error) {
      setMessage(error.message || "Не удалось сохранить урок");
    }
  }

  async function deleteScheduleLesson() {
    if (!selectedCell?.lesson) {
      return;
    }

    try {
      await apiRequest(`/schedule/editor/lesson/${selectedCell.lesson.lessonId}`, { method: "DELETE" });
      setMessage("Урок удален из сетки");
      setSelectedCell(null);
      await loadScheduleEditor();
    } catch (error) {
      setMessage(error.message || "Не удалось удалить урок");
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
      await loadScheduleEditor();
    } catch (error) {
      setMessage(error.message || "Не удалось создать класс");
    }
  }

  if (!allowed) {
    return <AccessWarning title="Расписание доступно менеджеру расписания, директору и администратору" />;
  }

  const classes = sortItems(metadata?.classes ?? [], "name", { name: (item) => classSortValue(item.name) });
  const subjects = metadata?.subjects ?? [];
  const teachers = metadata?.teachers ?? [];
  const slots = metadata?.slots ?? [];
  const lessonMap = buildScheduleLessonMap(week.lessons ?? []);
  const weekCaption = getWeekCaption(weekStart);
  const selectedSubject = subjects.find((item) => Number(item.subjectId) === Number(lessonForm.subjectId));

  return (
    <section className="page-stack">
      <PageHeader
        title="Редактор расписания"
        subtitle={weekCaption}
        text="Недельная сетка занятий с назначением предметов, преподавателей, домашних заданий и примечаний."
      />
      <StatusLine loading={loading} message={message} />
      <div className="metric-grid">
        <MetricCard label="Уроков недели" value={(week.lessons ?? []).length} />
        <MetricCard label="Классов" value={classes.length} />
        <MetricCard label="Предметов" value={subjects.length} />
        <MetricCard label="Слотов звонков" value={slots.length} />
      </div>
      <div className="schedule-toolbar">
        <button className="ghost-button compact" onClick={() => setWeekStart(shiftWeek(weekStart, -7))}>Предыдущая</button>
        <Field label="Неделя" type="date" value={weekStart} onChange={(value) => setWeekStart(toIsoDate(getMonday(new Date(value))))} />
        <button className="ghost-button compact" onClick={() => setWeekStart(shiftWeek(weekStart, 7))}>Следующая</button>
        {editable && (
          <form className="class-create-form" onSubmit={createClass}>
            <input value={className} placeholder="Новый класс" onChange={(event) => setClassName(event.target.value)} />
            <button className="primary-button compact">Добавить класс</button>
          </form>
        )}
      </div>
      <div className="schedule-editor">
        <div className="schedule-grid-wrap">
          <table className="schedule-grid-table">
            <thead>
              <tr>
                <th>День</th>
                <th>Время</th>
                <th>№</th>
                {classes.map((classItem) => (
                  <th key={classItem.classId}>{classItem.name}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {groupSlotsByDay(slots).map(({ day, daySlots }) => daySlots.map((slot, slotIndex) => (
                <tr key={`${day}-${slot.scheduleId}`}>
                  {slotIndex === 0 && <td rowSpan={daySlots.length} className="day-cell">{dayName(day)}</td>}
                  <td>{slot.startTime} - {slot.endTime}</td>
                  <td>{slot.lessonNumber}</td>
                  {classes.map((classItem) => {
                    const lesson = lessonMap.get(`${classItem.classId}_${slot.scheduleId}`) ?? null;
                    const selected = selectedCell?.classItem.classId === classItem.classId
                      && selectedCell?.slot.scheduleId === slot.scheduleId;
                    return (
                      <td
                        className={`schedule-cell ${lesson ? "has-lesson" : "empty"} ${selected ? "selected" : ""}`}
                        key={`${classItem.classId}-${slot.scheduleId}`}
                        onClick={() => editable && selectCell(classItem, slot, lesson)}
                      >
                        {lesson ? (
                          <>
                            <strong>{lesson.subjectName}</strong>
                            <span>{lesson.teacherName}</span>
                            {lesson.homework && <small>ДЗ: {lesson.homework}</small>}
                          </>
                        ) : "Свободно"}
                      </td>
                    );
                  })}
                </tr>
              )))}
            </tbody>
          </table>
        </div>
        {editable && (
          <form className="schedule-side-editor" onSubmit={saveLesson}>
            <h3>{selectedCell?.lesson ? "Редактирование урока" : "Новый урок"}</h3>
            <p>{selectedCell ? `${selectedCell.classItem.name}, ${dayName(selectedCell.slot.dayOfWeek)}, ${selectedCell.slot.lessonNumber} урок` : "Выберите ячейку в сетке"}</p>
            <label className="field">
              <span>Предмет</span>
              <select value={lessonForm.subjectId} onChange={(event) => {
                const subject = subjects.find((item) => Number(item.subjectId) === Number(event.target.value));
                setLessonForm({
                  ...lessonForm,
                  subjectId: event.target.value,
                  teacherId: lessonForm.teacherId || subject?.teacherId || ""
                });
              }}>
                <option value="">Выберите предмет</option>
                {subjects.map((subject) => (
                  <option key={subject.subjectId} value={subject.subjectId}>{subject.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Преподаватель</span>
              <select value={lessonForm.teacherId || selectedSubject?.teacherId || ""} onChange={(event) => setLessonForm({ ...lessonForm, teacherId: event.target.value })}>
                <option value="">Выберите преподавателя</option>
                {teachers.map((teacher) => (
                  <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
                ))}
              </select>
            </label>
            <Field label="Домашнее задание / примечание" value={lessonForm.homework} onChange={(value) => setLessonForm({ ...lessonForm, homework: value })} />
            <button className="primary-button" disabled={!selectedCell}>Сохранить урок</button>
            {selectedCell?.lesson && (
              <button className="danger-button" type="button" onClick={deleteScheduleLesson}>Удалить урок</button>
            )}
          </form>
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
          lesson.topic || "—",
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
          lesson.topic || "—",
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

function calculateAverage(values) {
  const numericValues = values.map(Number).filter(Number.isFinite);
  return numericValues.length
    ? numericValues.reduce((sum, value) => sum + value, 0) / numericValues.length
    : 0;
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
    return "Н - пропуск";
  }

  if (numeric === 2) {
    return "Б - болезнь";
  }

  if (numeric === 3) {
    return "Ув - уважительная причина";
  }

  if (numeric === 4) {
    return "Неуд - неуважительная причина";
  }

  return "Не отмечено";
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

function getMonday(date) {
  const result = new Date(date);
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
  const date = new Date(weekStart);
  date.setDate(date.getDate() + days);
  return toIsoDate(getMonday(date));
}

function getWeekCaption(weekStart) {
  const start = new Date(weekStart);
  const end = new Date(start);
  end.setDate(end.getDate() + 4);
  return `${formatDate(start)} - ${formatDate(end)}`;
}

function getLessonDateForSlot(weekStart, slot) {
  const date = new Date(weekStart);
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

function dayName(day) {
  return ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"][Number(day)] ?? String(day);
}

createRoot(document.getElementById("root")).render(<App />);
