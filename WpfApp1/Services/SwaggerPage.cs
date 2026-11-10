namespace WpfApp1.Services
{
    public static class SwaggerPage
    {
        public const string Html = @"<!DOCTYPE html>
<html lang=""ru"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>PostOffice API — Swagger</title>
<style>
  :root { --blue:#0b3c7a; --blue2:#1656a5; --red:#e63946; --bg:#f3f6fb; --text:#1b2430; --muted:#6b7a90; }
  * { box-sizing:border-box; }
  body { margin:0; font-family:'Segoe UI',Arial,sans-serif; background:var(--bg); color:var(--text); }
  header { background:linear-gradient(120deg,#082b58,#1656a5); color:#fff; padding:28px 40px 24px; }
  header h1 { margin:0 0 6px; font-size:28px; letter-spacing:.2px; }
  header p { margin:0; color:#cfe0f5; font-size:14px; }
  .wrap { max-width:1180px; margin:22px auto 50px; padding:0 24px; }
  .panel { background:#fff; border-radius:14px; box-shadow:0 8px 24px rgba(16,42,77,.08); padding:20px 22px; margin-bottom:18px; }
  .row { display:flex; gap:12px; align-items:center; flex-wrap:wrap; }
  input, textarea { border:1px solid #d7e1ee; border-radius:9px; padding:10px 12px; font-family:inherit; font-size:14px; outline:none; background:#fbfdff; }
  input:focus, textarea:focus { border-color:var(--blue2); box-shadow:0 0 0 3px rgba(22,86,165,.12); }
  input { min-width:220px; }
  textarea { width:100%; min-height:74px; font-family:Consolas,monospace; font-size:12.5px; }
  button { border:0; border-radius:9px; padding:10px 16px; font-family:inherit; font-weight:600; cursor:pointer; background:var(--blue); color:#fff; }
  button:hover { background:var(--blue2); }
  button.light { background:#e8f0fb; color:var(--blue); }
  .method { display:inline-block; min-width:58px; text-align:center; border-radius:7px; padding:5px 8px; font-size:12px; font-weight:700; color:#fff; }
  .GET { background:#1e9e6a; } .POST { background:#e08a1e; } .PUT { background:#1656a5; } .DELETE { background:#e63946; }
  .ep { border:1px solid #e5edf7; border-radius:11px; padding:14px 16px; margin:12px 0; }
  .ep h3 { margin:0 0 4px; font-size:15px; }
  .ep small { color:var(--muted); }
  .ep .body { margin-top:10px; display:none; }
  .ep.open .body { display:block; }
  pre { background:#0f1b2c; color:#c8e1ff; border-radius:10px; padding:14px; overflow:auto; font-size:12.5px; line-height:1.5; margin:10px 0 0; max-height:340px; }
  .tag { font-size:11px; text-transform:uppercase; letter-spacing:.8px; color:var(--muted); margin:18px 0 4px; }
  .ok { color:#1e9e6a; font-weight:700; }
  .note { background:#eef5ff; border-left:4px solid var(--blue2); border-radius:8px; padding:12px 14px; color:#29405f; font-size:13.5px; }
  code { background:#eef3fa; border-radius:5px; padding:2px 6px; }
</style>
</head>
<body>
<header>
  <h1>PostOffice API</h1>
  <p>REST API информационно-справочной системы «Почтовое отделение» · OpenAPI 3.0.3 · JWT Bearer</p>
  <p style=""margin-top:8px"">Сервер: <b>http://localhost:{{PORT}}</b> · Документация: <b>/swagger/v1/swagger.json</b></p>
</header>
<div class=""wrap"">
  <div class=""panel"">
    <div class=""row"">
      <div style=""flex:1"">
        <div class=""tag"">Авторизация</div>
        <div class=""row"">
          <input id=""login"" value=""admin"" placeholder=""Логин"">
          <input id=""password"" type=""password"" placeholder=""Пароль"">
          <button onclick=""doLogin()"">Получить JWT</button>
        </div>
        <div style=""margin-top:10px;color:#6b7a90;font-size:13px"">Укажите данные своей учётной записи.</div>
      </div>
    </div>
    <div class=""tag"">Bearer-токен</div>
    <div class=""row"">
      <input id=""token"" style=""flex:1"" placeholder=""Вставьте JWT или получите его кнопкой выше"">
      <button class=""light"" onclick=""clearToken()"">Очистить</button>
    </div>
  </div>

  <div class=""panel"">
    <div class=""tag"">Auth</div>
    <div class=""ep"" data-method=""POST"" data-path=""/api/auth/login"">
      <span class=""method POST"">POST</span> <b>/api/auth/login</b> — авторизация, выдача JWT
      <div class=""body""><textarea>{ ""login"": ""admin"", ""password"": """" }</textarea><button onclick=""call(this)"">Выполнить</button></div>
    </div>

    <div class=""tag"">Shipments</div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/shipments"">
      <span class=""method GET"">GET</span> <b>/api/shipments</b> — список отправлений
      <div class=""body""><input class=""query"" value=""?search=&status=&from=&to="" style=""width:100%""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/shipments/{number}"">
      <span class=""method GET"">GET</span> <b>/api/shipments/{number}</b> — карточка и история статусов
      <div class=""body""><input class=""query"" value=""ПО261031-1001"" style=""width:100%""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""POST"" data-path=""/api/shipments"">
      <span class=""method POST"">POST</span> <b>/api/shipments</b> — регистрация отправления
      <div class=""body""><textarea>{ ""typeId"": 2, ""senderId"": 1, ""recipientId"": 2, ""weight"": 1.5, ""comment"": ""Тестовое отправление через API"" }</textarea><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""PUT"" data-path=""/api/shipments/{number}/status"">
      <span class=""method PUT"">PUT</span> <b>/api/shipments/{number}/status</b> — изменение статуса
      <div class=""body""><input class=""query"" value=""ПО261031-1001"" style=""width:100%""><textarea>{ ""status"": ""Отправлено"", ""comment"": ""Передано в сортировочный центр"" }</textarea><button onclick=""call(this)"">Выполнить</button></div>
    </div>

    <div class=""tag"">Catalog</div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/tariffs"">
      <span class=""method GET"">GET</span> <b>/api/tariffs</b> — действующие тарифы
      <div class=""body""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/products"">
      <span class=""method GET"">GET</span> <b>/api/products</b> — каталог почтовых товаров
      <div class=""body""><input class=""query"" value=""?search=&category="" style=""width:100%""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""POST"" data-path=""/api/sales"">
      <span class=""method POST"">POST</span> <b>/api/sales</b> — продажа товара
      <div class=""body""><textarea>{ ""productId"": 1, ""quantity"": 2, ""clientId"": 1 }</textarea><button onclick=""call(this)"">Выполнить</button></div>
    </div>

    <div class=""tag"">Reports</div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/reports/shipments"">
      <span class=""method GET"">GET</span> <b>/api/reports/shipments</b> — отчёт по отправлениям
      <div class=""body""><input class=""query"" value=""?from=2026-10-01&to=2026-10-31"" style=""width:100%""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
    <div class=""ep"" data-method=""GET"" data-path=""/api/reports/sales"">
      <span class=""method GET"">GET</span> <b>/api/reports/sales</b> — отчёт по продажам
      <div class=""body""><input class=""query"" value=""?from=2026-10-01&to=2026-10-31"" style=""width:100%""><button onclick=""call(this)"">Выполнить</button></div>
    </div>
  </div>

  <div class=""panel"">
    <div class=""tag"">Ответ сервера</div>
    <pre id=""out"">Выберите метод и нажмите «Выполнить».</pre>
    <div class=""note"">Схема OpenAPI доступна по адресу <code>/swagger/v1/swagger.json</code> и может быть открыта в Swagger UI, Postman или Insomnia.</div>
  </div>
</div>
<script>
  function token() { return document.getElementById('token').value.trim(); }
  function clearToken() { document.getElementById('token').value = ''; }
  async function doLogin() {
    const login = document.getElementById('login').value;
    const password = document.getElementById('password').value;
    const res = await fetch('/api/auth/login', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({login,password}) });
    const data = await res.json();
    document.getElementById('out').textContent = JSON.stringify(data, null, 2);
    if (data.token) document.getElementById('token').value = data.token;
  }
  async function call(btn) {
    const ep = btn.closest('.ep');
    let path = ep.dataset.path;
    const q = ep.querySelector('.query');
    if (q) path += q.value;
    const body = ep.querySelector('textarea');
    const headers = { 'Content-Type':'application/json' };
    if (token()) headers['Authorization'] = 'Bearer ' + token();
    try {
      const res = await fetch(path, { method: ep.dataset.method, headers, body: body ? body.value : undefined });
      const text = await res.text();
      let pretty = text;
      try { pretty = JSON.stringify(JSON.parse(text), null, 2); } catch(e) {}
      document.getElementById('out').textContent = 'HTTP ' + res.status + '\n\n' + pretty;
    } catch (e) {
      document.getElementById('out').textContent = 'Ошибка запроса: ' + e;
    }
  }
  document.querySelectorAll('.ep').forEach(ep => ep.addEventListener('click', e => {
    if (e.target.tagName !== 'BUTTON' && e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') ep.classList.toggle('open');
  }));
</script>
</body>
</html>";
    }
}
