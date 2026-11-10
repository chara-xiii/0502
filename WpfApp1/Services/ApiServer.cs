using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WpfApp1.Data;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public static class ApiServer
    {
        private static HttpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static int _requestCount;

        public static event Action<string> RequestLogged;

        public static bool IsRunning
        {
            get { return _running; }
        }

        public static int Port
        {
            get
            {
                int port;
                if (!int.TryParse(ConfigurationManager.AppSettings["ApiPort"], out port)) port = 5080;
                return port;
            }
        }

        public static string BaseUrl
        {
            get { return "http://localhost:" + Port; }
        }

        public static int RequestCount
        {
            get { return _requestCount; }
        }

        public static string Start()
        {
            if (_running) return null;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(BaseUrl + "/");
                _listener.Start();
                _running = true;
                _thread = new Thread(ListenLoop) { IsBackground = true, Name = "PostOfficeApi" };
                _thread.Start();
                Log("API запущен: " + BaseUrl + "/swagger");
                return null;
            }
            catch (Exception ex)
            {
                _running = false;
                return "Не удалось запустить API: " + ex.Message;
            }
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch
            {
                // остановка фонового прослушивания
            }
            _listener = null;
            Log("API остановлен.");
        }

        private static void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch
                {
                    if (!_running) break;
                    continue;
                }

                ThreadPool.QueueUserWorkItem(state => SafeHandle((HttpListenerContext)state), context);
            }
        }

        private static void SafeHandle(HttpListenerContext context)
        {
            var started = DateTime.Now;
            var status = 200;
            try
            {
                status = Handle(context);
            }
            catch (Exception ex)
            {
                status = 500;
                TryWriteJson(context, 500, new { error = "Внутренняя ошибка сервера", detail = ex.Message, code = "INTERNAL_ERROR" });
            }
            finally
            {
                Interlocked.Increment(ref _requestCount);
                var elapsed = (int)(DateTime.Now - started).TotalMilliseconds;
                Log(string.Format("{0} {1} → {2} ({3} мс)", context.Request.HttpMethod, context.Request.Url.AbsolutePath, status, elapsed));
                try { context.Response.Close(); } catch { }
            }
        }

        private static int Handle(HttpListenerContext context)
        {
            AddCors(context);
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return 204;
            }

            var path = (context.Request.Url.AbsolutePath ?? "/").TrimEnd('/');
            if (path == "") path = "/";

            if (path == "/")
            {
                Redirect(context, "/swagger");
                return 302;
            }
            if (path == "/swagger" || path == "/swagger/index.html")
            {
                WriteHtml(context, 200, SwaggerPage.Html.Replace("{{PORT}}", Port.ToString()));
                return 200;
            }
            if (path == "/swagger/v1/swagger.json")
            {
                WriteJson(context, 200, OpenApiDocument());
                return 200;
            }
            if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            {
                return ServeImage(context, path.Substring("/assets/".Length));
            }
            if (path == "/api/health")
            {
                WriteJson(context, 200, new { status = "ok", service = "PostOffice API", time = DateTime.Now.ToString("s"), version = "1.0" });
                return 200;
            }
            if (path == "/api/auth/login" && context.Request.HttpMethod == "POST")
            {
                return Login(context);
            }

            var user = Authenticate(context);
            if (user == null)
            {
                WriteJson(context, 401, new { error = "Требуется авторизация. Передайте Bearer-токен.", code = "UNAUTHORIZED" });
                return 401;
            }

            if (path == "/api/tariffs" && context.Request.HttpMethod == "GET")
                return GetTariffs(context);

            if (path == "/api/clients" && context.Request.HttpMethod == "GET")
                return GetClients(context);

            if (path == "/api/products" && context.Request.HttpMethod == "GET")
                return GetProducts(context);

            if (path == "/api/shipments" && context.Request.HttpMethod == "GET")
                return GetShipments(context);

            if (path == "/api/shipments" && context.Request.HttpMethod == "POST")
                return CreateShipment(context, user);

            if (path.StartsWith("/api/shipments/", StringComparison.OrdinalIgnoreCase))
            {
                var tail = Uri.UnescapeDataString(path.Substring("/api/shipments/".Length));
                if (tail.EndsWith("/status", StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "PUT")
                {
                    return ChangeStatus(context, user, tail.Substring(0, tail.Length - "/status".Length));
                }
                if (context.Request.HttpMethod == "GET")
                {
                    return GetShipment(context, tail);
                }
            }

            if (path == "/api/sales" && context.Request.HttpMethod == "POST")
                return CreateSale(context, user);

            if (path == "/api/reports/shipments" && context.Request.HttpMethod == "GET")
                return ShipmentReport(context);

            if (path == "/api/reports/sales" && context.Request.HttpMethod == "GET")
                return SalesReport(context);

            if (path == "/api/users" && context.Request.HttpMethod == "GET")
            {
                if (user.Role != "Admin")
                {
                    WriteJson(context, 403, new { error = "Недостаточно прав.", code = "FORBIDDEN" });
                    return 403;
                }
                var users = UsersRepository.GetAll().ConvertAll(u => new
                {
                    id = u.UserId,
                    fullName = u.FullName,
                    position = u.Position,
                    login = u.Login,
                    role = u.Role,
                    isActive = u.IsActive
                });
                WriteJson(context, 200, users);
                return 200;
            }

            WriteJson(context, 404, new { error = "Метод API не найден.", code = "NOT_FOUND" });
            return 404;
        }

        private static JwtPayload Authenticate(HttpListenerContext context)
        {
            var header = context.Request.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(header)) return null;
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
            return JwtService.Validate(header.Substring(7).Trim());
        }

        private static int Login(HttpListenerContext context)
        {
            var body = ReadBody(context);
            var data = Deserialize(body);
            if (data == null)
            {
                WriteJson(context, 400, new { error = "Некорректный JSON.", code = "BAD_REQUEST" });
                return 400;
            }

            var login = GetString(data, "login");
            var password = GetString(data, "password");
            var user = AuthService.Login(login, password);
            if (user == null)
            {
                WriteJson(context, 401, new { error = "Неверный логин или пароль.", code = "INVALID_CREDENTIALS" });
                return 401;
            }

            var token = JwtService.CreateToken(user);
            WriteJson(context, 200, new
            {
                token,
                tokenType = "Bearer",
                expiresIn = 8 * 3600,
                user = new { id = user.UserId, fullName = user.FullName, role = user.Role, roleName = user.RoleDisplay }
            });
            return 200;
        }

        private static int GetTariffs(HttpListenerContext context)
        {
            var tariffs = TariffsRepository.GetAll(true).ConvertAll(t => new
            {
                id = t.TariffId,
                typeId = t.TypeId,
                typeName = t.TypeName,
                maxWeight = t.MaxWeight,
                price = t.Price,
                isActive = t.IsActive
            });
            WriteJson(context, 200, tariffs);
            return 200;
        }

        private static int GetClients(HttpListenerContext context)
        {
            var clients = ClientsRepository.GetAll(GetQuery(context, "search")).ConvertAll(c => new
            {
                id = c.ClientId,
                fullName = c.FullName,
                phone = c.Phone,
                address = c.Address,
                email = c.Email
            });
            WriteJson(context, 200, clients);
            return 200;
        }

        private static int GetProducts(HttpListenerContext context)
        {
            var products = ProductsRepository.GetAll(GetQuery(context, "search"), GetQuery(context, "category")).ConvertAll(p => new
            {
                id = p.ProductId,
                name = p.Name,
                category = p.Category,
                price = p.Price,
                stock = p.Stock,
                description = p.Description,
                imageUrl = string.IsNullOrWhiteSpace(p.ImageFile) ? null : "/assets/" + p.ImageFile
            });
            WriteJson(context, 200, products);
            return 200;
        }

        private static int GetShipments(HttpListenerContext context)
        {
            var search = GetQuery(context, "search");
            var status = GetQuery(context, "status");
            var from = ParseDate(GetQuery(context, "from"));
            var to = ParseDate(GetQuery(context, "to"));
            var shipments = ShipmentsRepository.GetAll(search, status, from, to).ConvertAll(ToDto);
            WriteJson(context, 200, shipments);
            return 200;
        }

        private static int GetShipment(HttpListenerContext context, string number)
        {
            var shipment = ShipmentsRepository.GetByNumber(number);
            if (shipment == null)
            {
                WriteJson(context, 404, new { error = "Отправление не найдено.", code = "SHIPMENT_NOT_FOUND" });
                return 404;
            }

            var history = ShipmentsRepository.GetHistory(shipment.ShipmentId).ConvertAll(h => new
            {
                status = h.Status,
                changedAt = h.ChangedAt.ToString("s"),
                employee = h.EmployeeName,
                comment = h.Comment
            });

            WriteJson(context, 200, new
            {
                shipment = ToDto(shipment),
                history
            });
            return 200;
        }

        private static int CreateShipment(HttpListenerContext context, JwtPayload user)
        {
            if (user.Role != "Admin" && user.Role != "Operator")
            {
                WriteJson(context, 403, new { error = "Недостаточно прав.", code = "FORBIDDEN" });
                return 403;
            }

            var data = Deserialize(ReadBody(context));
            if (data == null)
            {
                WriteJson(context, 400, new { error = "Некорректный JSON.", code = "BAD_REQUEST" });
                return 400;
            }

            var typeId = GetInt(data, "typeId");
            var senderId = GetInt(data, "senderId");
            var recipientId = GetInt(data, "recipientId");
            var weight = GetDecimal(data, "weight");
            var comment = GetString(data, "comment");

            if (TypesRepository.GetById(typeId) == null)
            {
                WriteJson(context, 400, new { error = "Тип отправления не найден.", code = "TYPE_NOT_FOUND" });
                return 400;
            }
            if (ClientsRepository.GetById(senderId) == null || ClientsRepository.GetById(recipientId) == null)
            {
                WriteJson(context, 400, new { error = "Отправитель или получатель не найден.", code = "CLIENT_NOT_FOUND" });
                return 400;
            }
            if (senderId == recipientId)
            {
                WriteJson(context, 400, new { error = "Отправитель и получатель должны различаться.", code = "SAME_CLIENT" });
                return 400;
            }
            var weightError = ValidationService.ValidateWeight(weight);
            if (weightError != null)
            {
                WriteJson(context, 400, new { error = weightError, code = "INVALID_WEIGHT" });
                return 400;
            }

            var tariff = TariffsRepository.FindForWeight(typeId, weight);
            if (tariff == null)
            {
                WriteJson(context, 400, new { error = "Для указанного веса нет действующего тарифа.", code = "TARIFF_NOT_FOUND" });
                return 400;
            }

            var shipment = new Shipment
            {
                TypeId = typeId,
                SenderId = senderId,
                RecipientId = recipientId,
                TariffId = tariff.TariffId,
                Weight = weight,
                Cost = tariff.Price,
                EmployeeId = user.UserId,
                Comment = comment
            };

            var number = ShipmentsRepository.Create(shipment, comment);
            var created = ShipmentsRepository.GetByNumber(number);
            WriteJson(context, 201, ToDto(created));
            return 201;
        }

        private static int ChangeStatus(HttpListenerContext context, JwtPayload user, string number)
        {
            if (user.Role != "Admin" && user.Role != "Operator")
            {
                WriteJson(context, 403, new { error = "Недостаточно прав.", code = "FORBIDDEN" });
                return 403;
            }

            var shipment = ShipmentsRepository.GetByNumber(number);
            if (shipment == null)
            {
                WriteJson(context, 404, new { error = "Отправление не найдено.", code = "SHIPMENT_NOT_FOUND" });
                return 404;
            }

            var data = Deserialize(ReadBody(context));
            var status = data == null ? null : GetString(data, "status");
            if (Array.IndexOf(ShipmentsRepository.Statuses, status) < 0)
            {
                WriteJson(context, 400, new { error = "Недопустимый статус отправления.", code = "INVALID_STATUS" });
                return 400;
            }

            var comment = data == null ? null : GetString(data, "comment");
            ShipmentsRepository.UpdateStatus(shipment.ShipmentId, status, user.UserId, comment);
            WriteJson(context, 200, new { number, status, message = "Статус обновлён." });
            return 200;
        }

        private static int CreateSale(HttpListenerContext context, JwtPayload user)
        {
            if (user.Role != "Admin" && user.Role != "Operator")
            {
                WriteJson(context, 403, new { error = "Недостаточно прав.", code = "FORBIDDEN" });
                return 403;
            }

            var data = Deserialize(ReadBody(context));
            if (data == null)
            {
                WriteJson(context, 400, new { error = "Некорректный JSON.", code = "BAD_REQUEST" });
                return 400;
            }

            var productId = GetInt(data, "productId");
            var quantity = GetInt(data, "quantity");
            var clientId = GetNullableInt(data, "clientId");
            if (quantity <= 0)
            {
                WriteJson(context, 400, new { error = "Количество должно быть больше нуля.", code = "INVALID_QUANTITY" });
                return 400;
            }
            if (ProductsRepository.GetById(productId) == null)
            {
                WriteJson(context, 404, new { error = "Товар не найден.", code = "PRODUCT_NOT_FOUND" });
                return 404;
            }

            try
            {
                var total = ProductsRepository.Sell(productId, clientId, quantity, user.UserId);
                WriteJson(context, 201, new { productId, quantity, total, soldAt = DateTime.Now.ToString("s") });
                return 201;
            }
            catch (Exception ex)
            {
                WriteJson(context, 409, new { error = ex.Message, code = "SALE_CONFLICT" });
                return 409;
            }
        }

        private static int ShipmentReport(HttpListenerContext context)
        {
            var from = ParseDate(GetQuery(context, "from")) ?? DateTime.Now.Date.AddDays(-30);
            var to = ParseDate(GetQuery(context, "to")) ?? DateTime.Now.Date;
            var report = ReportsRepository.BuildShipmentReport(from, to);
            WriteJson(context, 200, new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd"),
                count = report.Count,
                total = report.Total,
                byType = report.Rows.ConvertAll(r => new { group = r.Group, count = r.Count, total = r.Total }),
                byDay = report.Days.ConvertAll(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count, total = d.Total })
            });
            return 200;
        }

        private static int SalesReport(HttpListenerContext context)
        {
            var from = ParseDate(GetQuery(context, "from")) ?? DateTime.Now.Date.AddDays(-30);
            var to = ParseDate(GetQuery(context, "to")) ?? DateTime.Now.Date;
            var report = ReportsRepository.BuildSalesReport(from, to);
            WriteJson(context, 200, new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd"),
                count = report.Count,
                total = report.Total,
                byProduct = report.Rows.ConvertAll(r => new { group = r.Group, count = r.Count, total = r.Total }),
                byDay = report.Days.ConvertAll(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count, total = d.Total })
            });
            return 200;
        }

        private static object ToDto(ShipmentView s)
        {
            return new
            {
                id = s.ShipmentId,
                number = s.Number,
                typeId = s.TypeId,
                type = s.TypeName,
                senderId = s.SenderId,
                sender = s.SenderName,
                recipientId = s.RecipientId,
                recipient = s.RecipientName,
                weight = s.Weight,
                cost = s.Cost,
                status = s.Status,
                createdAt = s.CreatedAt.ToString("s"),
                employee = s.EmployeeName,
                comment = s.Comment
            };
        }

        private static int ServeImage(HttpListenerContext context, string fileName)
        {
            fileName = Uri.UnescapeDataString(fileName);
            var safeName = Path.GetFileName(fileName);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", safeName);
            if (!File.Exists(path))
            {
                WriteJson(context, 404, new { error = "Изображение не найдено.", code = "IMAGE_NOT_FOUND" });
                return 404;
            }

            var bytes = File.ReadAllBytes(path);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/png";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            return 200;
        }

        private static void AddCors(HttpListenerContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        }

        private static string ReadBody(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static Dictionary<string, object> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                return serializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int? GetNullableInt(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return null;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static decimal GetDecimal(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return 0;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static string GetQuery(HttpListenerContext context, string key)
        {
            return context.Request.QueryString[key] ?? "";
        }

        private static DateTime? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            DateTime value;
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                return value;
            if (DateTime.TryParse(text, out value)) return value;
            return null;
        }

        private static void WriteJson(HttpListenerContext context, int status, object data)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var json = serializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static void TryWriteJson(HttpListenerContext context, int status, object data)
        {
            try { WriteJson(context, status, data); } catch { }
        }

        private static void WriteHtml(HttpListenerContext context, int status, string html)
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = status;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static void Redirect(HttpListenerContext context, string location)
        {
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = location;
            context.Response.Close();
        }

        private static void Log(string message)
        {
            var handler = RequestLogged;
            if (handler != null) handler(message);
        }

        private static object OpenApiDocument()
        {
            var paths = new Dictionary<string, object>
            {
                {
                    "/api/auth/login", new Dictionary<string, object>
                    {
                        { "post", new { summary = "Авторизация и получение JWT", tags = new[] { "Auth" }, requestBody = new { required = true, content = new Dictionary<string, object> { { "application/json", new { schema = new { type = "object", properties = new { login = new { type = "string" }, password = new { type = "string" } } } } } } }, responses = new Dictionary<string, object> { { "200", new { description = "Токен выдан" } }, { "401", new { description = "Неверные учётные данные" } } } } }
                    }
                },
                {
                    "/api/shipments", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Список отправлений", tags = new[] { "Shipments" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new object[] { new { name = "search", @in = "query", schema = new { type = "string" } }, new { name = "status", @in = "query", schema = new { type = "string" } }, new { name = "from", @in = "query", schema = new { type = "string", format = "date" } }, new { name = "to", @in = "query", schema = new { type = "string", format = "date" } } }, responses = new Dictionary<string, object> { { "200", new { description = "Список отправлений" } }, { "401", new { description = "Не авторизован" } } } } },
                        { "post", new { summary = "Регистрация отправления", tags = new[] { "Shipments" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, requestBody = new { required = true, content = new Dictionary<string, object> { { "application/json", new { schema = new { type = "object", required = new[] { "typeId", "senderId", "recipientId", "weight" }, properties = new { typeId = new { type = "integer" }, senderId = new { type = "integer" }, recipientId = new { type = "integer" }, weight = new { type = "number" }, comment = new { type = "string" } } } } } } }, responses = new Dictionary<string, object> { { "201", new { description = "Отправление создано" } }, { "400", new { description = "Ошибка валидации" } }, { "401", new { description = "Не авторизован" } } } } }
                    }
                },
                {
                    "/api/shipments/{number}", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Карточка отправления и история статусов", tags = new[] { "Shipments" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new[] { new { name = "number", @in = "path", required = true, schema = new { type = "string" } } }, responses = new Dictionary<string, object> { { "200", new { description = "Карточка отправления" } }, { "404", new { description = "Не найдено" } } } } }
                    }
                },
                {
                    "/api/shipments/{number}/status", new Dictionary<string, object>
                    {
                        { "put", new { summary = "Изменение статуса", tags = new[] { "Shipments" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new[] { new { name = "number", @in = "path", required = true, schema = new { type = "string" } } }, requestBody = new { required = true, content = new Dictionary<string, object> { { "application/json", new { schema = new { type = "object", required = new[] { "status" }, properties = new { status = new { type = "string" }, comment = new { type = "string" } } } } } } }, responses = new Dictionary<string, object> { { "200", new { description = "Статус обновлён" } }, { "400", new { description = "Недопустимый статус" } } } } }
                    }
                },
                {
                    "/api/tariffs", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Действующие тарифы", tags = new[] { "Catalog" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, responses = new Dictionary<string, object> { { "200", new { description = "Список тарифов" } } } } }
                    }
                },
                {
                    "/api/products", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Каталог почтовых товаров", tags = new[] { "Catalog" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new[] { new { name = "search", @in = "query", schema = new { type = "string" } }, new { name = "category", @in = "query", schema = new { type = "string" } } }, responses = new Dictionary<string, object> { { "200", new { description = "Каталог товаров" } } } } }
                    }
                },
                {
                    "/api/sales", new Dictionary<string, object>
                    {
                        { "post", new { summary = "Продажа товара", tags = new[] { "Sales" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, requestBody = new { required = true, content = new Dictionary<string, object> { { "application/json", new { schema = new { type = "object", required = new[] { "productId", "quantity" }, properties = new { productId = new { type = "integer" }, quantity = new { type = "integer" }, clientId = new { type = "integer", nullable = true } } } } } } }, responses = new Dictionary<string, object> { { "201", new { description = "Продажа оформлена" } }, { "409", new { description = "Недостаточно товара" } } } } }
                    }
                },
                {
                    "/api/reports/shipments", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Отчёт по отправлениям за период", tags = new[] { "Reports" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new[] { new { name = "from", @in = "query", schema = new { type = "string", format = "date" } }, new { name = "to", @in = "query", schema = new { type = "string", format = "date" } } }, responses = new Dictionary<string, object> { { "200", new { description = "Отчёт" } } } } }
                    }
                },
                {
                    "/api/reports/sales", new Dictionary<string, object>
                    {
                        { "get", new { summary = "Отчёт по продажам товаров", tags = new[] { "Reports" }, security = new[] { new Dictionary<string, string[]> { { "bearerAuth", new string[0] } } }, parameters = new[] { new { name = "from", @in = "query", schema = new { type = "string", format = "date" } }, new { name = "to", @in = "query", schema = new { type = "string", format = "date" } } }, responses = new Dictionary<string, object> { { "200", new { description = "Отчёт" } } } } }
                    }
                }
            };

            return new
            {
                openapi = "3.0.3",
                info = new { title = "PostOffice API", version = "1.0.0", description = "REST API информационно-справочной системы «Почтовое отделение»" },
                servers = new[] { new { url = BaseUrl, description = "Локальный сервер приложения" } },
                components = new { securitySchemes = new { bearerAuth = new { type = "http", scheme = "bearer", bearerFormat = "JWT" } } },
                paths
            };
        }
    }
}
