const API_BASE_URL = window.APIFLOW_API_BASE_URL ?? window.location.origin;

const state = {
  profiles: [],
  sqlProfiles: [],
  operations: [],
  selectedProfileId: null,
  selectedEndpointProfileId: null,
  selectedSqlProfileId: null
};

const elements = {
  alertHost: document.querySelector('#alertHost'),
  jsonInputs: document.querySelectorAll('.json-input'),
  viewButtons: document.querySelectorAll('[data-view-target]'),
  views: document.querySelectorAll('.app-view'),
  profileCount: document.querySelector('#profileCount'),
  profileList: document.querySelector('#profileList'),
  profileForm: document.querySelector('#profileForm'),
  formTitle: document.querySelector('#formTitle'),
  profileId: document.querySelector('#profileId'),
  name: document.querySelector('#name'),
  baseUrl: document.querySelector('#baseUrl'),
  loginPath: document.querySelector('#loginPath'),
  loginBodyTemplate: document.querySelector('#loginBodyTemplate'),
  sessionIdJsonPath: document.querySelector('#sessionIdJsonPath'),
  defaultHeadersJson: document.querySelector('#defaultHeadersJson'),
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
  swaggerLink: document.querySelector('#swaggerLink'),
  sqlProfileCount: document.querySelector('#sqlProfileCount'),
  sqlProfileList: document.querySelector('#sqlProfileList'),
  sqlProfileForm: document.querySelector('#sqlProfileForm'),
  sqlProfileFormTitle: document.querySelector('#sqlProfileFormTitle'),
  sqlProfileId: document.querySelector('#sqlProfileId'),
  sqlProfileSelect: document.querySelector('#sqlProfileSelect'),
  sqlProfileKey: document.querySelector('#sqlProfileKey'),
  sqlProfileName: document.querySelector('#sqlProfileName'),
  sqlHost: document.querySelector('#sqlHost'),
  sqlPort: document.querySelector('#sqlPort'),
  sqlInstanceName: document.querySelector('#sqlInstanceName'),
  sqlDatabaseName: document.querySelector('#sqlDatabaseName'),
  sqlSchemaName: document.querySelector('#sqlSchemaName'),
  sqlApplicationName: document.querySelector('#sqlApplicationName'),
  sqlUsername: document.querySelector('#sqlUsername'),
  sqlPassword: document.querySelector('#sqlPassword'),
  sqlTrustServerCertificate: document.querySelector('#sqlTrustServerCertificate'),
  sqlEncrypt: document.querySelector('#sqlEncrypt'),
  newSqlProfileButton: document.querySelector('#newSqlProfileButton'),
  deleteSqlProfileButton: document.querySelector('#deleteSqlProfileButton'),
  endpointProfileFilter: document.querySelector('#endpointProfileFilter'),
  operationCount: document.querySelector('#operationCount'),
  operationList: document.querySelector('#operationList'),
  operationFormTitle: document.querySelector('#operationFormTitle'),
  operationForm: document.querySelector('#operationForm'),
  operationOriginalKey: document.querySelector('#operationOriginalKey'),
  operationProfileId: document.querySelector('#operationProfileId'),
  operationKey: document.querySelector('#operationKey'),
  operationMethod: document.querySelector('#operationMethod'),
  operationPath: document.querySelector('#operationPath'),
  operationHeadersJson: document.querySelector('#operationHeadersJson'),
  operationResultJsonPath: document.querySelector('#operationResultJsonPath'),
  operationTargetTableName: document.querySelector('#operationTargetTableName'),
  operationCreateTableIfMissing: document.querySelector('#operationCreateTableIfMissing'),
  operationAddMissingColumns: document.querySelector('#operationAddMissingColumns'),
  operationClearTableBeforeImport: document.querySelector('#operationClearTableBeforeImport'),
  operationBodyTemplate: document.querySelector('#operationBodyTemplate'),
  newOperationButton: document.querySelector('#newOperationButton'),
  deleteOperationButton: document.querySelector('#deleteOperationButton'),
  testProfileId: document.querySelector('#testProfileId'),
  operation: document.querySelector('#operation'),
  limit: document.querySelector('#limit'),
  offset: document.querySelector('#offset'),
  filters: document.querySelector('#filters'),
  params: document.querySelector('#params'),
  forceLogin: document.querySelector('#forceLogin'),
  saveToSqlServer: document.querySelector('#saveToSqlServer'),
  testSqlProfileId: document.querySelector('#testSqlProfileId'),
  runOperationButton: document.querySelector('#runOperationButton'),
  resultBox: document.querySelector('#resultBox')
};

document.addEventListener('DOMContentLoaded', init);

function init() {
  elements.swaggerLink.href = `${API_BASE_URL}/swagger`;
  for (const button of elements.viewButtons) {
    button.addEventListener('click', () => showView(button.dataset.viewTarget));
  }
  enhanceJsonInputs();
  elements.profileForm.addEventListener('submit', saveProfile);
  elements.newProfileButton.addEventListener('click', clearForm);
  elements.refreshButton.addEventListener('click', loadAll);
  elements.deleteButton.addEventListener('click', deleteProfile);
  elements.loginButton.addEventListener('click', loginProfile);
  elements.sqlProfileForm.addEventListener('submit', saveSqlProfile);
  elements.newSqlProfileButton.addEventListener('click', clearSqlProfileForm);
  elements.deleteSqlProfileButton.addEventListener('click', deleteSqlProfile);
  elements.sqlProfileSelect.addEventListener('change', () => {
    const id = Number(elements.sqlProfileSelect.value);
    id ? selectSqlProfile(id) : clearSqlProfileForm();
  });
  elements.endpointProfileFilter.addEventListener('change', () => {
    state.selectedEndpointProfileId = Number(elements.endpointProfileFilter.value);
    clearOperationForm();
    renderOperations();
  });
  elements.operationForm.addEventListener('submit', saveOperation);
  elements.newOperationButton.addEventListener('click', clearOperationForm);
  elements.deleteOperationButton.addEventListener('click', deleteOperation);
  elements.testProfileId.addEventListener('change', () => {
    state.selectedProfileId = Number(elements.testProfileId.value);
    const profile = state.profiles.find(item => item.id === state.selectedProfileId);
    if (profile) {
      fillProfileForm(profile);
      renderProfiles();
      renderOperations();
    }
  });
  elements.testSqlProfileId.addEventListener('change', () => {
    state.selectedSqlProfileId = Number(elements.testSqlProfileId.value) || null;
  });
  elements.runOperationButton.addEventListener('click', runOperation);
  loadAll();
}

function enhanceJsonInputs() {
  for (const input of elements.jsonInputs) {
    const wrapper = document.createElement('div');
    wrapper.className = 'json-field';
    input.parentNode.insertBefore(wrapper, input);
    wrapper.appendChild(input);

    const toolbar = document.createElement('div');
    toolbar.className = 'json-toolbar';
    toolbar.innerHTML = `
      <span class="json-status text-secondary">JSON</span>
      <button class="btn btn-outline-secondary btn-sm" type="button">Formatla</button>
    `;
    wrapper.appendChild(toolbar);

    const status = toolbar.querySelector('.json-status');
    const formatButton = toolbar.querySelector('button');
    formatButton.addEventListener('click', () => formatJsonInput(input, status));
    input.addEventListener('blur', () => validateJsonInput(input, status));
    input.addEventListener('input', () => {
      input.classList.remove('is-invalid', 'is-valid');
      status.textContent = 'JSON';
      status.className = 'json-status text-secondary';
    });
  }
}

function formatJsonInput(input, status) {
  const value = input.value.trim();
  if (!value) {
    validateJsonInput(input, status);
    return;
  }

  if (value.includes('{{')) {
    validateJsonInput(input, status);
    status.textContent = 'Şablon geçerli';
    return;
  }

  try {
    input.value = JSON.stringify(JSON.parse(value), null, 2);
    input.classList.remove('is-invalid');
    input.classList.add('is-valid');
    status.textContent = 'Geçerli';
    status.className = 'json-status text-success';
  } catch (error) {
    input.classList.remove('is-valid');
    input.classList.add('is-invalid');
    status.textContent = `Hatalı: ${error.message}`;
    status.className = 'json-status text-danger';
  }
}

function validateJsonInput(input, status) {
  const value = input.value.trim();
  if (!value) {
    input.classList.remove('is-invalid', 'is-valid');
    status.textContent = 'Boş';
    status.className = 'json-status text-secondary';
    return true;
  }

  try {
    JSON.parse(normalizeJsonTemplate(value));
    input.classList.remove('is-invalid');
    input.classList.add('is-valid');
    status.textContent = 'Geçerli';
    status.className = 'json-status text-success';
    return true;
  } catch (error) {
    input.classList.remove('is-valid');
    input.classList.add('is-invalid');
    status.textContent = `Hatalı: ${error.message}`;
    status.className = 'json-status text-danger';
    return false;
  }
}

function normalizeJsonTemplate(value) {
  return value.replace(/{{\s*[\w.]+\s*}}/g, '"__template__"');
}

async function loadAll() {
  try {
    const [profiles, operations, sqlProfiles] = await Promise.all([
      api('/api/profiles'),
      api('/api/integrations/operations'),
      api('/api/sql-profiles')
    ]);

    state.profiles = profiles;
    state.operations = operations;
    state.sqlProfiles = sqlProfiles;
    if (!state.selectedEndpointProfileId && profiles.length > 0) {
      state.selectedEndpointProfileId = state.selectedProfileId || profiles[0].id;
    }

    renderProfiles();
    renderSqlProfiles();
    renderProfileOptions();
    renderOperations();
    const visibleOperations = getVisibleOperations();
    if (!elements.operationOriginalKey.value && visibleOperations.length > 0) {
      selectOperation(visibleOperations[0].key, visibleOperations[0].profileId);
    }

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

function renderSqlProfiles() {
  elements.sqlProfileCount.textContent = state.sqlProfiles.length;
  elements.sqlProfileList.innerHTML = '';
  elements.testSqlProfileId.innerHTML = '';
  elements.sqlProfileSelect.innerHTML = '';

  const newOption = document.createElement('option');
  newOption.value = '';
  newOption.textContent = 'Yeni profil';
  elements.sqlProfileSelect.appendChild(newOption);

  if (state.sqlProfiles.length === 0) {
    elements.sqlProfileList.innerHTML = '<div class="text-secondary">Kayıtlı SQL profili yok.</div>';
    const option = document.createElement('option');
    option.value = '';
    option.textContent = 'SQL profili yok';
    elements.testSqlProfileId.appendChild(option);
    return;
  }

  for (const profile of state.sqlProfiles) {
    const selectOption = document.createElement('option');
    selectOption.value = profile.id;
    selectOption.textContent = `${profile.name} | ${profile.key}`;
    elements.sqlProfileSelect.appendChild(selectOption);

    const option = document.createElement('option');
    option.value = profile.id;
    option.textContent = `${profile.name} (${profile.databaseName})`;
    elements.testSqlProfileId.appendChild(option);

    const button = document.createElement('button');
    button.type = 'button';
    button.className = `profile-item${profile.id === state.selectedSqlProfileId ? ' active' : ''}`;
    button.innerHTML = `
      <div class="profile-title">
        <strong>${escapeHtml(profile.name)}</strong>
        <span class="badge text-bg-light">#${profile.id}</span>
      </div>
      <div class="profile-meta">
        <span>${escapeHtml(profile.key)}</span>
        <span>${escapeHtml(profile.host)}</span>
        <span>${escapeHtml(profile.databaseName)}</span>
      </div>
    `;
    button.addEventListener('click', () => selectSqlProfile(profile.id));
    elements.sqlProfileList.appendChild(button);
  }

  if (!state.selectedSqlProfileId) {
    state.selectedSqlProfileId = state.sqlProfiles[0].id;
  }

  elements.testSqlProfileId.value = state.selectedSqlProfileId;
  elements.sqlProfileSelect.value = state.selectedSqlProfileId || '';
}

function selectSqlProfile(id) {
  const profile = state.sqlProfiles.find(item => item.id === id);
  if (!profile) {
    clearSqlProfileForm();
    return;
  }

  state.selectedSqlProfileId = profile.id;
  elements.sqlProfileId.value = profile.id;
  elements.sqlProfileSelect.value = profile.id;
  elements.sqlProfileKey.value = profile.key;
  elements.sqlProfileName.value = profile.name;
  elements.sqlHost.value = profile.host;
  elements.sqlPort.value = profile.port || '';
  elements.sqlInstanceName.value = profile.instanceName || '';
  elements.sqlDatabaseName.value = profile.databaseName;
  elements.sqlSchemaName.value = profile.schemaName;
  elements.sqlApplicationName.value = profile.applicationName || '';
  elements.sqlUsername.value = profile.username;
  elements.sqlPassword.value = profile.password;
  elements.sqlTrustServerCertificate.checked = profile.trustServerCertificate;
  elements.sqlEncrypt.checked = profile.encrypt;
  elements.sqlProfileFormTitle.textContent = `SQL Profili: ${profile.name}`;
  elements.deleteSqlProfileButton.disabled = false;
  elements.testSqlProfileId.value = profile.id;
  renderSqlProfiles();
}

function clearSqlProfileForm() {
  state.selectedSqlProfileId = null;
  elements.sqlProfileForm.reset();
  elements.sqlProfileId.value = '';
  elements.sqlProfileSelect.value = '';
  elements.sqlProfileKey.value = '';
  elements.sqlSchemaName.value = 'dbo';
  elements.sqlApplicationName.value = '';
  elements.sqlTrustServerCertificate.checked = true;
  elements.sqlEncrypt.checked = false;
  elements.sqlProfileFormTitle.textContent = 'Yeni SQL Profili';
  elements.deleteSqlProfileButton.disabled = true;
  renderSqlProfiles();
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
  elements.operationList.innerHTML = '';
  const visibleOperations = getVisibleOperations();
  const testOperations = getTestOperations();
  elements.operationCount.textContent = visibleOperations.length;

  for (const operation of testOperations) {
    const option = document.createElement('option');
    option.value = operation.key;
    option.textContent = `${operation.key} (${operation.httpMethod})`;
    elements.operation.appendChild(option);
  }

  if (visibleOperations.length === 0) {
    elements.operationList.innerHTML = '<div class="text-secondary">Kayıtlı endpoint yok.</div>';
  }

  for (const operation of visibleOperations) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `operation-item${operation.key === elements.operationOriginalKey.value ? ' active' : ''}`;
    button.innerHTML = `
      <div class="operation-title">
        <strong>${escapeHtml(operation.key)}</strong>
        <span class="badge text-bg-light">${escapeHtml(operation.httpMethod)}</span>
      </div>
      <div class="operation-meta">
        <span>${escapeHtml(getProfileName(operation.profileId))}</span>
        <span>${escapeHtml(operation.path)}</span>
      </div>
    `;
    button.addEventListener('click', () => {
      selectOperation(operation.key, operation.profileId);
      showView('endpointsView');
    });
    elements.operationList.appendChild(button);
  }

  elements.operation.onchange = () => selectOperation(elements.operation.value, state.selectedProfileId);
}

function renderProfileOptions() {
  const selectedValue = elements.operationProfileId.value || String(state.selectedProfileId || '');
  const selectedFilterValue = String(state.selectedEndpointProfileId || state.selectedProfileId || '');
  const selectedTestValue = String(state.selectedProfileId || '');
  elements.operationProfileId.innerHTML = '';
  elements.endpointProfileFilter.innerHTML = '';
  elements.testProfileId.innerHTML = '';

  for (const profile of state.profiles) {
    const option = document.createElement('option');
    option.value = profile.id;
    option.textContent = profile.name;
    elements.operationProfileId.appendChild(option);

    const filterOption = document.createElement('option');
    filterOption.value = profile.id;
    filterOption.textContent = profile.name;
    elements.endpointProfileFilter.appendChild(filterOption);

    const testOption = document.createElement('option');
    testOption.value = profile.id;
    testOption.textContent = profile.name;
    elements.testProfileId.appendChild(testOption);
  }

  if (selectedValue) {
    elements.operationProfileId.value = selectedValue;
  }

  if (selectedFilterValue) {
    elements.endpointProfileFilter.value = selectedFilterValue;
  }

  if (selectedTestValue) {
    elements.testProfileId.value = selectedTestValue;
  }
}

function getVisibleOperations() {
  const profileId = state.selectedEndpointProfileId || state.selectedProfileId;
  if (!profileId) {
    return state.operations;
  }

  return state.operations.filter(operation => operation.profileId === profileId);
}

function getTestOperations() {
  if (!state.selectedProfileId) {
    return [];
  }

  return state.operations.filter(operation => operation.profileId === state.selectedProfileId);
}

function getProfileName(profileId) {
  return state.profiles.find(profile => profile.id === profileId)?.name || `Profil #${profileId}`;
}

function selectOperation(key, profileId = state.selectedEndpointProfileId) {
  const operation = state.operations.find(item =>
    item.key === key && (!profileId || item.profileId === profileId));
  if (!operation) {
    clearOperationForm();
    return;
  }

  elements.operationOriginalKey.value = operation.key;
  elements.operationProfileId.value = operation.profileId;
  elements.operationKey.value = operation.key;
  elements.operationMethod.value = operation.httpMethod;
  elements.operationPath.value = operation.path;
  elements.operationHeadersJson.value = operation.headersJson || '';
  elements.operationResultJsonPath.value = operation.resultJsonPath || '';
  elements.operationTargetTableName.value = operation.targetTableName || '';
  elements.operationCreateTableIfMissing.checked = operation.createTableIfMissing !== false;
  elements.operationAddMissingColumns.checked = operation.addMissingColumns !== false;
  elements.operationClearTableBeforeImport.checked = operation.clearTableBeforeImport === true;
  elements.operationBodyTemplate.value = operation.requestBodyTemplate || '';
  elements.operationFormTitle.textContent = `Endpoint: ${operation.key}`;
  elements.deleteOperationButton.disabled = false;
  elements.operation.value = operation.key;
  renderOperations();
}

function clearOperationForm() {
  elements.operationForm.reset();
  elements.operationOriginalKey.value = '';
  elements.operationProfileId.value = state.selectedEndpointProfileId || state.selectedProfileId || state.profiles[0]?.id || '';
  elements.operationMethod.value = 'POST';
  elements.operationCreateTableIfMissing.checked = true;
  elements.operationAddMissingColumns.checked = true;
  elements.operationClearTableBeforeImport.checked = false;
  elements.operationFormTitle.textContent = 'Yeni Endpoint';
  elements.deleteOperationButton.disabled = true;
  renderOperations();
}

function selectProfile(id) {
  const profile = state.profiles.find(item => item.id === id);
  if (!profile) {
    clearForm();
    return;
  }

  state.selectedProfileId = profile.id;
  fillProfileForm(profile);
  renderProfiles();
  renderOperations();
}

function fillProfileForm(profile) {
  elements.profileId.value = profile.id;
  elements.name.value = profile.name;
  elements.baseUrl.value = profile.baseUrl;
  elements.loginPath.value = profile.loginPath || '';
  elements.loginBodyTemplate.value = profile.loginBodyTemplate || '';
  elements.sessionIdJsonPath.value = profile.sessionIdJsonPath || '';
  elements.defaultHeadersJson.value = profile.defaultHeadersJson || '';
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
  elements.operationProfileId.value = profile.id;
  elements.testProfileId.value = profile.id;
  if (!state.selectedEndpointProfileId) {
    state.selectedEndpointProfileId = profile.id;
  }
}

function showView(viewId) {
  for (const view of elements.views) {
    view.classList.toggle('active', view.id === viewId);
  }

  for (const button of elements.viewButtons) {
    button.classList.toggle('active', button.dataset.viewTarget === viewId);
  }
}

function clearForm() {
  state.selectedProfileId = null;
  elements.profileForm.reset();
  elements.profileId.value = '';
  elements.baseUrl.value = '';
  elements.loginPath.value = '';
  elements.loginBodyTemplate.value = '';
  elements.sessionIdJsonPath.value = '';
  elements.defaultHeadersJson.value = '';
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
    loginBodyTemplate: elements.loginBodyTemplate.value.trim(),
    sessionIdJsonPath: elements.sessionIdJsonPath.value.trim(),
    defaultHeadersJson: elements.defaultHeadersJson.value.trim(),
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
    const result = await api(`/api/integrations/profiles/${id}/login`, { method: 'POST' });
    elements.resultBox.textContent = JSON.stringify(maskSession(result), null, 2);
    showAlert('Login testi başarılı.', 'success');
    await loadAll();
  } catch (error) {
    elements.resultBox.textContent = error.message;
    showAlert(error.message, 'danger');
  }
}

async function runOperation() {
  const id = elements.testProfileId.value || elements.profileId.value;
  if (!id) {
    return;
  }

  if (!elements.operation.value) {
    showAlert('Bu profil için endpoint bulunamadı.', 'warning');
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

  let params = null;
  if (elements.params.value.trim()) {
    try {
      params = JSON.parse(elements.params.value);
    } catch {
      showAlert('Params JSON geçerli değil.', 'warning');
      return;
    }
  }

  const payload = {
    forceLogin: elements.forceLogin.checked,
    saveToSqlServer: elements.saveToSqlServer.checked,
    sqlProfileId: elements.saveToSqlServer.checked ? Number(elements.testSqlProfileId.value) : null,
    limit: Number(elements.limit.value || 20),
    offset: Number(elements.offset.value || 0),
    filters,
    params
  };

  if (payload.saveToSqlServer && !payload.sqlProfileId) {
    showAlert('MSSQL için SQL profili seçilmeli.', 'warning');
    return;
  }

  const originalButtonHtml = elements.runOperationButton.innerHTML;
  elements.runOperationButton.disabled = true;
  elements.runOperationButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Çalışıyor';

  const startedAt = Date.now();
  const timer = setInterval(() => {
    const elapsedSeconds = Math.floor((Date.now() - startedAt) / 1000);
    elements.resultBox.textContent = `Operasyon çalışıyor... ${elapsedSeconds} sn`;
  }, 1000);

  elements.resultBox.textContent = 'Operasyon çalışıyor... 0 sn';
  try {
    const result = await api(`/api/integrations/profiles/${id}/operations/${elements.operation.value}`, {
      method: 'POST',
      body: JSON.stringify(payload),
      timeoutMs: 60000
    });
    elements.resultBox.textContent = JSON.stringify(result, null, 2);
  } catch (error) {
    elements.resultBox.textContent = error.message;
    showAlert(error.message, 'danger');
  } finally {
    clearInterval(timer);
    elements.runOperationButton.disabled = false;
    elements.runOperationButton.innerHTML = originalButtonHtml;
  }
}

async function saveOperation(event) {
  event.preventDefault();

  const originalKey = elements.operationOriginalKey.value;
  const payload = {
    profileId: Number(elements.operationProfileId.value),
    key: elements.operationKey.value.trim(),
    httpMethod: elements.operationMethod.value,
    path: elements.operationPath.value.trim(),
    headersJson: elements.operationHeadersJson.value.trim(),
    resultJsonPath: elements.operationResultJsonPath.value.trim(),
    requestBodyTemplate: elements.operationBodyTemplate.value.trim(),
    targetTableName: elements.operationTargetTableName.value.trim(),
    createTableIfMissing: elements.operationCreateTableIfMissing.checked,
    addMissingColumns: elements.operationAddMissingColumns.checked,
    clearTableBeforeImport: elements.operationClearTableBeforeImport.checked
  };

  try {
    const saved = await api(originalKey ? `/api/integrations/operations/${encodeURIComponent(originalKey)}` : '/api/integrations/operations', {
      method: originalKey ? 'PUT' : 'POST',
      body: JSON.stringify(payload)
    });
    showAlert('Endpoint kaydedildi.', 'success');
    await loadAll();
    selectOperation(saved.key);
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function saveSqlProfile(event) {
  event.preventDefault();

  const id = elements.sqlProfileId.value;
  const payload = {
    key: elements.sqlProfileKey.value.trim(),
    name: elements.sqlProfileName.value.trim(),
    host: elements.sqlHost.value.trim(),
    port: elements.sqlPort.value ? Number(elements.sqlPort.value) : null,
    instanceName: elements.sqlInstanceName.value.trim(),
    databaseName: elements.sqlDatabaseName.value.trim(),
    username: elements.sqlUsername.value.trim(),
    password: elements.sqlPassword.value,
    applicationName: elements.sqlApplicationName.value.trim(),
    trustServerCertificate: elements.sqlTrustServerCertificate.checked,
    encrypt: elements.sqlEncrypt.checked,
    schemaName: elements.sqlSchemaName.value.trim() || 'dbo'
  };

  try {
    const saved = await api(id ? `/api/sql-profiles/${id}` : '/api/sql-profiles', {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(payload)
    });
    showAlert('SQL profili kaydedildi.', 'success');
    state.selectedSqlProfileId = saved.id;
    await loadAll();
    selectSqlProfile(saved.id);
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function deleteSqlProfile() {
  const id = elements.sqlProfileId.value;
  if (!id || !confirm('SQL profili silinsin mi?')) {
    return;
  }

  try {
    await api(`/api/sql-profiles/${id}`, { method: 'DELETE' });
    showAlert('SQL profili silindi.', 'success');
    clearSqlProfileForm();
    await loadAll();
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function deleteOperation() {
  const key = elements.operationOriginalKey.value;
  if (!key || !confirm('Endpoint silinsin mi?')) {
    return;
  }

  try {
    await api(`/api/integrations/operations/${encodeURIComponent(key)}`, { method: 'DELETE' });
    showAlert('Endpoint silindi.', 'success');
    clearOperationForm();
    await loadAll();
  } catch (error) {
    showAlert(error.message, 'danger');
  }
}

async function api(url, options = {}) {
  const { timeoutMs = 0, ...fetchOptions } = options;
  const controller = timeoutMs > 0 ? new AbortController() : null;
  const timeout = controller
    ? setTimeout(() => controller.abort(), timeoutMs)
    : null;

  let response;
  try {
    response = await fetch(`${API_BASE_URL}${url}`, {
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...fetchOptions.headers
      },
      ...fetchOptions,
      signal: controller?.signal
    });
  } catch (error) {
    if (error.name === 'AbortError') {
      throw new Error('Operasyon 60 saniye içinde tamamlanmadı. Limit değerini düşürün veya endpoint filtresi ekleyin.');
    }

    throw new Error(`API bağlantısı kurulamadı: ${error.message}`);
  } finally {
    if (timeout) {
      clearTimeout(timeout);
    }
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
