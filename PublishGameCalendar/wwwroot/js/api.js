const TOKEN_KEY = 'gc_token';

function getToken() { return localStorage.getItem(TOKEN_KEY); }
function setToken(t) { localStorage.setItem(TOKEN_KEY, t); }
function clearToken() { localStorage.removeItem(TOKEN_KEY); }

function decodeToken(token) {
    try {
        return JSON.parse(atob(token.split('.')[1]));
    } catch { return null; }
}

function currentUser() {
    const t = getToken();
    if (!t) return null;
    const p = decodeToken(t);
    if (!p || (p.exp && p.exp * 1000 < Date.now())) { clearToken(); return null; }
    return p;
}

function requireAuth(role) {
    const u = currentUser();
    if (!u) { window.location.href = 'login.html'; return false; }
    if (role && u['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] !== role) {
        window.location.href = 'index.html'; return false;
    }
    return true;
}

function userRole() {
    const u = currentUser();
    return u ? u['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] : null;
}

function userEmail() {
    const u = currentUser();
    return u ? u.email : null;
}

async function apiFetch(path, options = {}) {
    const token = getToken();
    const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
    if (token) headers['Authorization'] = `Bearer ${token}`;
    const res = await fetch(path, { ...options, headers });
    return res;
}

function logout() {
    clearToken();
    window.location.href = 'login.html';
}
