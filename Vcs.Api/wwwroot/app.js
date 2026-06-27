const userStorageKey = "gitgoy.web.username";

const state = {
    userName: localStorage.getItem(userStorageKey) || "",
    repositories: [],
    selectedRepository: null,
    selectedBranch: "",
    currentPath: ""
};

const elements = {
    loginScreen: document.getElementById("loginScreen"),
    loginForm: document.getElementById("loginForm"),
    usernameInput: document.getElementById("usernameInput"),
    passwordInput: document.getElementById("passwordInput"),
    loginError: document.getElementById("loginError"),
    currentUserLabel: document.getElementById("currentUserLabel"),
    logoutButton: document.getElementById("logoutButton"),
    repoSearch: document.getElementById("repoSearch"),
    repoList: document.getElementById("repoList"),
    repoTitle: document.getElementById("repoTitle"),
    repoDescription: document.getElementById("repoDescription"),
    branchSelect: document.getElementById("branchSelect"),
    breadcrumbs: document.getElementById("breadcrumbs"),
    backButton: document.getElementById("backButton"),
    fileList: document.getElementById("fileList"),
    emptyState: document.getElementById("emptyState")
};

async function api(path, options = {}) {
    const response = await fetch(path, {
        ...options,
        headers: {
            ...(options.headers || {}),
            "X-User-Name": state.userName
        }
    });

    if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`);
    }

    return response.json();
}

async function signIn(username, password) {
    const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password })
    });

    if (!response.ok) {
        throw new Error(`Login failed: ${response.status}`);
    }

    const user = await response.json();
    state.userName = user.username || user.userName || user.Username || username;
    localStorage.setItem(userStorageKey, state.userName);
}

async function loadRepositories() {
    setEmptyState("Loading repositories...");
    state.repositories = await api("/api/projects/my");
    renderRepositories();

    if (state.repositories.length === 0) {
        setEmptyState(`No repositories for ${state.userName}.`);
        return;
    }

    await selectRepository(state.repositories[0].id);
}

function renderRepositories() {
    const query = elements.repoSearch.value.trim().toLowerCase();
    const repositories = query
        ? state.repositories.filter(repo => repo.name.toLowerCase().includes(query))
        : state.repositories;

    elements.repoList.innerHTML = "";
    for (const repository of repositories) {
        const item = document.createElement("button");
        item.type = "button";
        item.className = repository.id === state.selectedRepository?.id
            ? "repo-item active"
            : "repo-item";
        item.innerHTML = `
            <strong>${escapeHtml(repository.name)}</strong>
            <span>${escapeHtml(repository.description || repository.visibility)}</span>
        `;
        item.addEventListener("click", () => selectRepository(repository.id));
        elements.repoList.appendChild(item);
    }
}

async function selectRepository(repositoryId) {
    const repository = state.repositories.find(item => item.id === repositoryId);
    if (!repository) {
        return;
    }

    state.selectedRepository = repository;
    state.currentPath = "";
    elements.repoTitle.textContent = repository.name;
    elements.repoDescription.textContent = repository.description || `${repository.visibility} repository`;
    renderRepositories();

    const branches = await api(`/api/projects/${repository.id}/branches`);
    renderBranches(branches);
    state.selectedBranch = branches.find(branch => branch.isDefault)?.name || branches[0]?.name || "main";
    elements.branchSelect.value = state.selectedBranch;
    await loadFiles();
}

function renderBranches(branches) {
    elements.branchSelect.innerHTML = "";
    elements.branchSelect.disabled = branches.length === 0;

    for (const branch of branches) {
        const option = document.createElement("option");
        option.value = branch.name;
        option.textContent = branch.isDefault ? `${branch.name} (default)` : branch.name;
        elements.branchSelect.appendChild(option);
    }
}

async function loadFiles() {
    if (!state.selectedRepository) {
        setEmptyState("Select a repository to view files.");
        return;
    }

    setEmptyState("Loading files...");
    const query = new URLSearchParams({ branch: state.selectedBranch });
    if (state.currentPath) {
        query.set("path", state.currentPath);
    }

    const files = await api(`/api/projects/${state.selectedRepository.id}/files?${query}`);
    renderPath();
    renderFiles(files);
}

function renderPath() {
    elements.backButton.disabled = !state.currentPath;
    elements.breadcrumbs.textContent = state.currentPath ? `/${state.currentPath}` : "/";
}

function renderFiles(files) {
    elements.fileList.innerHTML = "";

    if (files.length === 0) {
        setEmptyState("This folder is empty.");
        return;
    }

    elements.emptyState.classList.add("hidden");
    for (const file of files) {
        const row = document.createElement(file.isDirectory ? "button" : "div");
        row.className = file.isDirectory ? "file-row directory" : "file-row";
        if (file.isDirectory) {
            row.type = "button";
            row.addEventListener("click", () => {
                state.currentPath = file.path;
                loadFiles();
            });
        }

        row.innerHTML = `
            <div class="file-name">
                <span>${file.isDirectory ? "[dir]" : "[file]"}</span>
                <span>${escapeHtml(file.name)}</span>
            </div>
            <div class="file-type">${file.isDirectory ? "Directory" : "File"}</div>
        `;
        elements.fileList.appendChild(row);
    }
}

function setEmptyState(message) {
    elements.fileList.innerHTML = "";
    elements.emptyState.textContent = message;
    elements.emptyState.classList.remove("hidden");
    renderPath();
}

function goBack() {
    if (!state.currentPath) {
        return;
    }

    const parts = state.currentPath.split("/");
    parts.pop();
    state.currentPath = parts.join("/");
    loadFiles();
}

function showLogin(message = "") {
    elements.loginScreen.classList.remove("hidden");
    elements.loginError.textContent = message;
    elements.loginError.classList.toggle("hidden", !message);
}

function showApp() {
    elements.loginScreen.classList.add("hidden");
    elements.currentUserLabel.textContent = state.userName;
}

function resetApp() {
    state.repositories = [];
    state.selectedRepository = null;
    state.selectedBranch = "";
    state.currentPath = "";
    elements.repoSearch.value = "";
    elements.repoTitle.textContent = "Select a repository";
    elements.repoDescription.textContent = "";
    elements.branchSelect.innerHTML = "";
    elements.branchSelect.disabled = true;
    elements.repoList.innerHTML = "";
    setEmptyState("Select a repository to view files.");
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

elements.loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    const username = elements.usernameInput.value.trim();
    const password = elements.passwordInput.value;

    if (!username) {
        showLogin("Enter a login.");
        return;
    }

    try {
        elements.loginError.classList.add("hidden");
        await signIn(username, password);
        showApp();
        await loadRepositories();
    } catch (error) {
        console.error(error);
        showLogin("Could not sign in. Check that the API is running.");
    }
});

elements.logoutButton.addEventListener("click", () => {
    localStorage.removeItem(userStorageKey);
    state.userName = "";
    resetApp();
    showLogin();
});

elements.repoSearch.addEventListener("input", renderRepositories);
elements.branchSelect.addEventListener("change", () => {
    state.selectedBranch = elements.branchSelect.value;
    state.currentPath = "";
    loadFiles();
});
elements.backButton.addEventListener("click", goBack);

if (state.userName) {
    showApp();
    loadRepositories().catch(error => {
        console.error(error);
        localStorage.removeItem(userStorageKey);
        state.userName = "";
        resetApp();
        showLogin("Could not load this account. Sign in again.");
    });
} else {
    resetApp();
    showLogin();
}
