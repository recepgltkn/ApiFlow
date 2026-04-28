const API_BASE_URL = 'http://localhost:5085';

const state = {
  profiles: [],
  operations: [],
  selectedProfileId: null
};

const elements = {
  alertHost: document.querySelector('#alertHost'),
  profileCount: document.querySelector('#profileCount'),
  profileList: document.querySelector('#profileList'),
  profileForm: document.querySelector('#profileForm'),
  formTitle: document.querySelector('#formTitle'),
  profileId: document.querySelector('#profileId'),
  name: document.querySelector('#name'),
  baseUrl: document.querySelector('#baseUrl'),
  loginPath: document.querySelector('#loginPath'),
  username: document.querySelector('#username'),
  language: document.querySelector('#language'),
  password: document.querySelector('#password'),
  apiKey: document.querySelector('#apiKey'),
  firmaKodu: document.querySelector('#firmaKodu'),
  donemKodu: document.querySelector('#donemKodu'),
  disconnectSameUser: document.querySelector('#disconnectSameUser'),
  deleteButton: document.querySelector('#deleteButton'),
  loginButton: document.querySelector('#loginButton'),
  newProfileButton: document.querySelector('#newProfileButton'),
  refreshButton: document.querySelector('#refreshButton'),
  operation: document.querySelector('#operation'),
  limit: document.querySelector('#limit'),
  offset: document.querySelector('#offset'),
  filters: document.querySelector('#filters'),
  forceLogin: document.querySelector('#forceLogin'),
  saveToSqlServer: document.querySelector('#saveToSqlServer'),
  runOperationButton: document.querySelector('#runOperationButton'),
  resultBox: document.querySelector('#resultBox')
};

document.addEventListener('DOMContentLoaded', init);

function init() {
  elements.profileForm.addEventListener('submit', saveProfile);
  elements.newProfileButton.addEventListener('click', clearForm);
  elements.refreshButton.addEventListener('click', loadAll);
  elements.deleteButton.addEventListener('click', deleteProfile);
  elements.loginButton.addEventListener('click', loginProfile);
  elements.runOperationButton.addEventListener('click', runOperation);
  loadAll();
}

async function loadAll() {
  try {
    const [profiles, operations] = await Promise.all([
      api('/api/profiles'),
      api('/api/dia/operations')
    ]);

    state.profiles = profiles;
    state.operations = operations;
    renderProfiles();
    renderOperations();

    if (state.selectedProfileId) {
      const selected = state.profiles.find(profile => profile.id === state.selectedProfileId);
      selected ? selectProfile(selected.id) : clearForm();
    } else if (state.profiles.length > 0) {
      selectProfile(state.profiles[0].id);
    }
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

function renderProfiles() {
  elements.profileCount.textContent = state.profiles.length;
  elements.profileList.innerHTML = '';

  if (state.profiles.length === 0) {
    elements.profileList.innerHTML = '<div class="text-secondary">Kayıtlı profil yok.</div>';
    return;
  }

  for (const profile of state.profiles) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `profile-item${profile.id === state.selectedProfileId ? ' active' : ''}`;
    button.innerHTML = `
      <div class="profile-title">
        <strong>${escapeHtml(profile.name)}</strong>
        <span class="badge text-bg-light">#${profile.id}</span>
      </div>
      <div class="profile-meta">
        <span>${escapeHtml(profile.username)}</span>
        <span>Firma ${profile.firmaKodu}</span>
        <span>Dönem ${profile.donemKodu}</span>
      </div>
    `;
    button.addEventListener('click', () => selectProfile(profile.id));
    elements.profileList.appendChild(button);
  }
}

function renderOperations() {
  elements.operation.innerHTML = '';
  for (const operation of state.operations) {
    const option = document.createElement('option');
    option.value = operation.key;
    option.textContent = `${operation.key} (${operation.diaMethod})`;
    elements.operation.appendChild(option);
  }
}

function selectProfile(id) {
  const profile = state.profiles.find(item => item.id === id);
  if (!profile) {
    clearForm();
    return;
  }

  state.selectedProfileId = profile.id;
  elements.profileId.value = profile.id;
  elements.name.value = profile.name;
  elements.baseUrl.value = profile.baseUrl;
  elements.loginPath.value = profile.loginPath;
  elements.username.value = profile.username;
  elements.language.value = profile.language;
  elements.password.value = profile.password;
  elements.apiKey.value = profile.apiKey;
  elements.firmaKodu.value = profile.firmaKodu;
  elements.donemKodu.value = profile.donemKodu;
  elements.disconnectSameUser.checked = profile.disconnectSameUser;
  elements.formTitle.textContent = `Profil: ${profile.name}`;
  elements.deleteButton.disabled = false;
  elements.loginButton.disabled = false;
  elements.runOperationButton.disabled = false;
  renderProfiles();
}

function clearForm() {
  state.selectedProfileId = null;
  elements.profileForm.reset();
  elements.profileId.value = '';
  elements.baseUrl.value = '';
  elements.loginPath.value = '';
  elements.language.value = 'tr';
  elements.disconnectSameUser.checked = true;
  elements.formTitle.textContent = 'Yeni Profil';
  elements.deleteButton.disabled = true;
  elements.loginButton.disabled = true;
  elements.runOperationButton.disabled = true;
  renderProfiles();
}

async function saveProfile(event) {
  event.preventDefault();

  const id = elements.profileId.value;
  const payload = {
    name: elements.name.value.trim(),
    username: elements.username.value.trim(),
    firmaKodu: Number(elements.firmaKodu.value),
    donemKodu: Number(elements.donemKodu.value),
    loginPath: elements.loginPath.value.trim(),
    baseUrl: elements.baseUrl.value.trim(),
    language: elements.language.value.trim() || 'tr',
    disconnectSameUser: elements.disconnectSameUser.checked
  };

  payload.password = elements.password.value;
  payload.apiKey = elements.apiKey.value;

  if (!payload.password || !payload.apiKey) {
    showAlert('Parola ve API key zorunlu.', 'warning');
    return;
  }

  try {
    const saved = await api(id ? `/api/profiles/${id}` : '/api/profiles', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(payload)
    });
    showAlert('Profil kaydedildi.', 'success');
    state.selectedProfileId = saved.id;
    await loadAll();
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function deleteProfile() {
  const id = elements.profileId.value;
  if (!id || !confirm('Profil silinsin mi?')) {
    return;
  }

  try {
    await api(`/api/profiles/${id}`, { method: 'DELETE' });
    showAlert('Profil silindi.', 'success');
    clearForm();
    await loadAll();
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function loginProfile() {
  const id = elements.profileId.value;
  if (!id) {
    return;
  }

  elements.resultBox.textContent = 'Login deneniyor...';
  try {
    const result = await api(`/api/dia/profiles/${id}/login`, { method: 'POST' });
    elements.resultBox.textContent = JSON.stringify(maskSession(result), null, 2);
    showAlert('Login testi başarılı.', 'success');
    await loadAll();
  } catch (error) {
    elements.resultBox.textContent = error.message;
    showAlert(error.message, 'danger');
  }
}

async function runOperation() {
  const id = elements.profileId.value;
  if (!id) {
    return;
  }

  let filters = null;
  if (elements.filters.value.trim()) {
    try {
      filters = JSON.parse(elements.filters.value);
    } catch {
      showAlert('Filters JSON geçerli değil.', 'warning');
      return;
    }
  }

  const payload = {
    forceLogin: elements.forceLogin.checked,
    saveToSqlServer: elements.saveToSqlServer.checked,
    limit: Number(elements.limit.value || 20),
    offset: Number(elements.offset.value || 0),
    filters
  };

  elements.resultBox.textContent = 'Operasyon çalışıyor...';
  try {
    const result = await api(`/api/dia/profiles/${id}/operations/${elements.operation.value}`, {
      method: 'POST',
      body: JSON.stringify(payload)
    });
    elements.resultBox.textContent = JSON.stringify(result, null, 2);
  } catch (error) {
    elements.resultBox.textContent = error.message;
    showAlert(error.message, 'danger');
  }
}

async function api(url, options = {}) {
  let response;
  try {
    response = await fetch(`${API_BASE_URL}${url}`, {
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...options.headers
      },
      ...options
    });
  } catch (error) {
    throw new Error(`API bağlantısı kurulamadı: ${error.message}`);
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  const data = parseResponse(text);
  if (!response.ok) {
    throw new Error(data?.message || data?.title || text || `HTTP ${response.status}`);
  }

  return data;
}

function parseResponse(text) {
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text);
  } catch {
    return { message: text };
  }
}

function showAlert(message, type) {
  elements.alertHost.innerHTML = `
    <div class="alert alert-${type} alert-dismissible fade show" role="alert">
      ${escapeHtml(message)}
      <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Kapat"></button>
    </div>
  `;
  setTimeout(() => {
    elements.alertHost.innerHTML = '';
  }, 5000);
}

function maskSession(result) {
  return {
    ...result,
    sessionId: result.sessionId ? `${result.sessionId.slice(0, 6)}...` : result.sessionId
  };
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
