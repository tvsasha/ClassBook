function resolveApiBase() {
    const { hostname, port, origin } = window.location;
    const isLocalHost = hostname === "localhost" || hostname === "127.0.0.1";
    const liveServerPorts = new Set(["5500", "5501", "5502", "5503", "5504", "5505"]);

    if (isLocalHost && liveServerPorts.has(port)) {
        return "http://localhost:5156/api";
    }

    return `${origin}/api`;
}

const apiUrl = resolveApiBase();

let currentUser = null;

// DOM
const authBlock = document.getElementById("authBlock");
const userControls = document.getElementById("userControls");
const usersList = document.getElementById("usersList");
const editCard = document.getElementById("editCard");
const overlay = document.getElementById("overlay");
const createBtn = document.getElementById("createBtn");
const editTitle = document.getElementById("editTitle");
const editError = document.getElementById("editError");

// ===== ВХОД =====
document.getElementById("loginBtn").addEventListener("click", async () => {
    const login = document.getElementById("loginInput").value.trim();
    const password = document.getElementById("passwordInput").value.trim();

    if (!login || !password) {
        document.getElementById("loginError").textContent = "Заполните логин и пароль";
        return;
    }

    try {
        const res = await fetch(`${apiUrl}/auth/login`, {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ login, password })
        });

        if (!res.ok) {
            const data = await res.json();
            document.getElementById("loginError").textContent = data || "Ошибка входа";
            return;
        }

        currentUser = await res.json();
        authBlock.style.display = "none";
        userControls.style.display = "flex";

        if ((currentUser.role || currentUser.roleName) === "Администратор") {
            createBtn.style.display = "block";
        }

        loadUsers();
    } catch (err) {
        document.getElementById("loginError").textContent = "Ошибка сети";
    }
});

// ===== ВЫХОД =====
document.getElementById("logoutBtn").addEventListener("click", async () => {
    try {
        // Отправляем запрос на выход к серверу
        const res = await fetch(`${apiUrl}/auth/logout`, {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" }
        });
        
        // Даже если сервер вернул ошибку, продолжаем локальный выход
        console.log("Выход выполнен", res.status);
    } catch (err) {
        console.warn("Ошибка при выходе с сервера:", err);
    }
    
    // Локальный выход
    currentUser = null;
    usersList.innerHTML = "";
    authBlock.style.display = "block";
    userControls.style.display = "none";
    createBtn.style.display = "none";
    
    // Очищаем поля ввода
    document.getElementById("loginInput").value = "";
    document.getElementById("passwordInput").value = "";
    document.getElementById("loginError").textContent = "";
    
    // Показываем сообщение об успешном выходе
    alert("Вы успешно вышли из системы");
});

// ===== ЗАГРУЗКА ПОЛЬЗОВАТЕЛЕЙ =====
async function loadUsers() {
    try {
        const res = await fetch(`${apiUrl}/users`, { credentials: "include" });
        if (!res.ok) throw new Error("Не удалось загрузить пользователей");

        const users = await res.json();
        usersList.innerHTML = "";

        users.forEach(u => {
            const li = document.createElement("li");
            li.className = "user-card";
            li.innerHTML = `<strong>${u.login}</strong> (${u.roleName || u.role || "Неизвестная роль"})`;
            if ((currentUser.role || currentUser.roleName) === "Администратор") {
                li.onclick = () => openEdit(u);
            }
            usersList.appendChild(li);
        });
    } catch (err) {
        console.error(err);
        usersList.innerHTML = "<p style='color:red'>Ошибка загрузки пользователей</p>";
    }
}

// ===== СОЗДАНИЕ / РЕДАКТИРОВАНИЕ =====
createBtn.addEventListener("click", () => openEdit(null));

async function openEdit(user = null) {
    editError.textContent = "";
    selectedUser = user;

    editCard.style.display = "block";
    overlay.style.display = "block";

    document.getElementById("editLogin").value = user ? user.login : "";
    document.getElementById("editPassword").value = "";

    // Загрузка ролей
    await loadRoles("editRole");

    if (user) {
        document.getElementById("editRole").value = user.roleId;
        editTitle.textContent = "Редактирование пользователя";
        document.getElementById("editPassword").placeholder = "Новый пароль (оставьте пустым, если не менять)";
    } else {
        editTitle.textContent = "Новый пользователь";
        document.getElementById("editPassword").placeholder = "Пароль";
    }
}

// ===== ЗАГРУЗКА РОЛЕЙ =====
async function loadRoles(selectId) {
    const select = document.getElementById(selectId);
    select.innerHTML = '<option value="">Загрузка...</option>';

    try {
        const res = await fetch(`${apiUrl}/roles`, { credentials: "include" });
        if (!res.ok) throw new Error();

        const roles = await res.json();
        select.innerHTML = "";

        roles.forEach(r => {
            const option = document.createElement("option");
            option.value = r.id; // предполагаем, что в ответе { id, name }
            option.textContent = r.name;
            select.appendChild(option);
        });
    } catch (e) {
        select.innerHTML = '<option value="">Ошибка загрузки ролей</option>';
    }
}

// ===== СОХРАНЕНИЕ =====
document.getElementById("saveUserBtn").addEventListener("click", async () => {
    const login = document.getElementById("editLogin").value.trim();
    const password = document.getElementById("editPassword").value.trim();
    const roleId = document.getElementById("editRole").value;

    if (!login || !roleId) {
        editError.textContent = "Логин и роль обязательны";
        return;
    }

    // Если редактируем — пароль необязателен
    if (!selectedUser && !password) {
        editError.textContent = "Пароль обязателен для нового пользователя";
        return;
    }

    const body = { login, fullName: login, roleId: Number(roleId) };
    if (password) body.password = password;

    const url = selectedUser ? `${apiUrl}/users/${selectedUser.id}` : `${apiUrl}/users`;
    const method = selectedUser ? "PUT" : "POST";

    try {
        const res = await fetch(url, {
            method,
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });

        if (!res.ok) {
            const msg = await res.text();
            editError.textContent = msg || "Ошибка сохранения";
            return;
        }

        closeEdit();
        loadUsers();
    } catch (err) {
        editError.textContent = "Ошибка сети";
    }
});

// ===== ЗАКРЫТИЕ =====
function closeEdit() {
    editCard.style.display = "none";
    overlay.style.display = "none";
    editError.textContent = "";
}

document.getElementById("cancelBtn").addEventListener("click", closeEdit);
