using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class Db
    {
        private static string _server;
        private static string _database;
        private static string _user;
        private static string _password;

        public static string Server
        {
            get
            {
                if (_server == null)
                    _server = Environment.GetEnvironmentVariable("POSTOFFICE_SQL_SERVER") ?? ConfigurationManager.AppSettings["DbServer"] ?? @".\SQLEXPRESS";
                return _server;
            }
        }

        public static string DatabaseName
        {
            get
            {
                if (_database == null)
                    _database = Environment.GetEnvironmentVariable("POSTOFFICE_SQL_DATABASE") ?? ConfigurationManager.AppSettings["DbName"] ?? "PostOfficeDB";
                return _database;
            }
        }

        public static string DbUser
        {
            get
            {
                if (_user == null)
                    _user = Environment.GetEnvironmentVariable("POSTOFFICE_SQL_USER") ?? ConfigurationManager.AppSettings["DbUser"] ?? "";
                return _user;
            }
        }

        public static string DbPassword
        {
            get
            {
                if (_password == null)
                    _password = Environment.GetEnvironmentVariable("POSTOFFICE_SQL_PASSWORD") ?? ConfigurationManager.AppSettings["DbPassword"] ?? "";
                return _password;
            }
        }

        public static string ConnectionString
        {
            get
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = Server,
                    InitialCatalog = DatabaseName,
                    IntegratedSecurity = string.IsNullOrWhiteSpace(DbUser),
                    MultipleActiveResultSets = true,
                    ConnectTimeout = 30
                };
                if (!builder.IntegratedSecurity)
                {
                    if (string.IsNullOrWhiteSpace(DbPassword))
                        throw new InvalidOperationException("Укажите POSTOFFICE_SQL_PASSWORD для выбранного пользователя SQL Server.");
                    builder.UserID = DbUser;
                    builder.Password = DbPassword;
                }
                return builder.ConnectionString;
            }
        }

        public static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public static void EnsureCreated()
        {
            // База данных создаётся отдельно администратором SQL Server, поэтому этап
            // CREATE DATABASE пропускается. Создаются только отсутствующие таблицы
            // с префиксом 0502_.
            using (var connection = OpenConnection())
            {
                ExecuteBatch(connection, SchemaSql);
            }

            SeedIfEmpty();
        }

        private static void ExecuteBatch(SqlConnection connection, string sql)
        {
            // Разделяем пакет по маркеру GO, чтобы каждую инструкцию выполнять отдельно.
            var parts = sql.Split(new[] { "\r\nGO\r\n", "\nGO\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                using (var command = new SqlCommand(part, connection))
                {
                    command.CommandTimeout = 60;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteScalar();
            }
        }

        public static int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private static void SeedIfEmpty()
        {
            var userCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_Users];"));
            if (userCount == 0)
            {
                SeedUsers();
            }

            var clientCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_Clients];"));
            if (clientCount == 0)
            {
                SeedClients();
            }

            var typeCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_ShipmentTypes];"));
            if (typeCount == 0)
            {
                SeedTypesAndTariffs();
            }

            var productCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_Products];"));
            if (productCount == 0)
            {
                SeedProducts();
            }

            var shipmentCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_Shipments];"));
            if (shipmentCount == 0)
            {
                SeedShipments();
            }

            var saleCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.[0502_Sales];"));
            if (saleCount == 0)
            {
                SeedSales();
            }
        }

        private static void SeedUsers()
        {
            var adminPassword = Services.AuthService.DemoPassword("admin");
            var operatorPassword = Services.AuthService.DemoPassword("operator");
            if (adminPassword.Length < 12 || operatorPassword.Length < 12 || adminPassword == operatorPassword)
                throw new InvalidOperationException("Для первого запуска задайте разные POSTOFFICE_DEMO_ADMIN_PASSWORD и POSTOFFICE_DEMO_OPERATOR_PASSWORD, не короче 12 символов.");

            var users = new[]
            {
                new { Name = "Демонстрационный сотрудник admin", Position = "Начальник отделения", Phone = "", Login = "admin", Password = adminPassword, Role = "Admin" },
                new { Name = "Демонстрационный сотрудник operator", Position = "Оператор связи", Phone = "", Login = "operator", Password = operatorPassword, Role = "Operator" },
                new { Name = "Демонстрационный сотрудник operator2", Position = "Оператор связи", Phone = "", Login = "operator2", Password = operatorPassword, Role = "Operator" },
                new { Name = "Демонстрационный сотрудник operator3", Position = "Оператор связи", Phone = "", Login = "operator3", Password = operatorPassword, Role = "Operator" }
            };

            foreach (var user in users)
            {
                Execute(
                    @"INSERT INTO dbo.[0502_Users] (FullName, Position, Phone, Login, PasswordHash, Role, IsActive)
                      VALUES (@name, @position, @phone, @login, @hash, @role, 1);",
                    P("@name", user.Name), P("@position", user.Position), P("@phone", user.Phone),
                    P("@login", user.Login), P("@hash", Services.PasswordHasher.Hash(user.Password)), P("@role", user.Role));
            }
        }

        private static void SeedClients()
        {
            var clients = new[]
            {
                new { Name = "Демонстрационный клиент 01", Phone = "+70000000001", Address = "Учебный адрес 1", Email = "client1@example.invalid" },
                new { Name = "Демонстрационный клиент 02", Phone = "+70000000002", Address = "Учебный адрес 2", Email = "client2@example.invalid" },
                new { Name = "Демонстрационный клиент 03", Phone = "+70000000003", Address = "Учебный адрес 3", Email = "client3@example.invalid" },
                new { Name = "Демонстрационный клиент 04", Phone = "+70000000004", Address = "Учебный адрес 4", Email = "client4@example.invalid" },
                new { Name = "Демонстрационный клиент 05", Phone = "+70000000005", Address = "Учебный адрес 5", Email = "client5@example.invalid" },
                new { Name = "Демонстрационный клиент 06", Phone = "+70000000006", Address = "Учебный адрес 6", Email = "client6@example.invalid" },
                new { Name = "Демонстрационный клиент 07", Phone = "+70000000007", Address = "Учебный адрес 7", Email = "client7@example.invalid" },
                new { Name = "Демонстрационный клиент 08", Phone = "+70000000008", Address = "Учебный адрес 8", Email = "client8@example.invalid" },
                new { Name = "Демонстрационный клиент 09", Phone = "+70000000009", Address = "Учебный адрес 9", Email = "client9@example.invalid" },
                new { Name = "Демонстрационный клиент 10", Phone = "+70000000010", Address = "Учебный адрес 10", Email = "client10@example.invalid" }
            };

            foreach (var client in clients)
            {
                Execute(
                    @"INSERT INTO dbo.[0502_Clients] (FullName, Phone, Address, Email, CreatedAt)
                      VALUES (@name, @phone, @address, @email, DATEADD(DAY, -ABS(CHECKSUM(NEWID())) % 120, GETDATE()));",
                    P("@name", client.Name), P("@phone", client.Phone),
                    P("@address", client.Address), P("@email", client.Email));
            }
        }

        private static void SeedTypesAndTariffs()
        {
            var types = new[]
            {
                new { Name = "Письмо", Description = "Простое или заказное письмо в конверте", Image = "letter.png" },
                new { Name = "Посылка", Description = "Коробочное отправление с объявленной ценностью", Image = "parcel.png" },
                new { Name = "Бандероль", Description = "Малогабаритное отправление с вложением", Image = "banderole.png" },
                new { Name = "Открытка", Description = "Почтовая открытка для личных поздравлений", Image = "postcard.png" }
            };

            foreach (var type in types)
            {
                Execute(
                    @"INSERT INTO dbo.[0502_ShipmentTypes] (Name, Description, ImageFile) VALUES (@name, @descr, @image);",
                    P("@name", type.Name), P("@descr", type.Description), P("@image", type.Image));
            }

            var tariffs = new[]
            {
                new { Type = "Письмо", Max = 0.020m, Price = 35.00m },
                new { Type = "Письмо", Max = 0.100m, Price = 60.00m },
                new { Type = "Посылка", Max = 2.000m, Price = 210.00m },
                new { Type = "Посылка", Max = 5.000m, Price = 340.00m },
                new { Type = "Посылка", Max = 10.000m, Price = 520.00m },
                new { Type = "Бандероль", Max = 0.500m, Price = 110.00m },
                new { Type = "Бандероль", Max = 2.000m, Price = 170.00m },
                new { Type = "Открытка", Max = 0.020m, Price = 25.00m }
            };

            foreach (var tariff in tariffs)
            {
                Execute(
                    @"INSERT INTO dbo.[0502_Tariffs] (TypeId, MaxWeight, Price, IsActive)
                      SELECT TypeId, @max, @price, 1 FROM dbo.[0502_ShipmentTypes] WHERE Name = @type;",
                    P("@max", tariff.Max), P("@price", tariff.Price), P("@type", tariff.Type));
            }
        }

        private static void SeedProducts()
        {
            var products = new[]
            {
                new { Name = "Конверт С5 «Ярославль»", Category = "Конверты", Price = 12.00m, Stock = 240, Image = "envelope_pack.png", Description = "Белый конверт с видами Ярославля, 229×162 мм." },
                new { Name = "Конверт С4 «Почта России»", Category = "Конверты", Price = 18.00m, Stock = 180, Image = "envelope_big.png", Description = "Конверт большого формата для документов, 324×229 мм." },
                new { Name = "Марка «Город золотой»", Category = "Марки", Price = 65.00m, Stock = 320, Image = "stamp_sheet.png", Description = "Художественная марка с видом Ярославля. В упаковке 4 шт." },
                new { Name = "Открытка «Ярославль. Стрелка»", Category = "Открытки", Price = 45.00m, Stock = 150, Image = "postcard_art.png", Description = "Цветная открытка 10×15 см с авторской фотографией." },
                new { Name = "Открытка «С добрым утром!»", Category = "Открытки", Price = 39.00m, Stock = 95, Image = "postcard_greeting.png", Description = "Поздравительная открытка с классическим дизайном." },
                new { Name = "Коробка «Посылка» 20×15×10", Category = "Упаковка", Price = 55.00m, Stock = 120, Image = "box_small.png", Description = "Гофрокороб для посылок малого размера." },
                new { Name = "Коробка «Посылка» 40×30×20", Category = "Упаковка", Price = 95.00m, Stock = 70, Image = "box_big.png", Description = "Прочный гофрокороб для средних отправлений." },
                new { Name = "Лента упаковочная «Ярославль»", Category = "Упаковка", Price = 80.00m, Stock = 85, Image = "tape.png", Description = "Клейкая лента 50 м с фирменным рисунком." },
                new { Name = "Ручка «Почта России»", Category = "Сувениры", Price = 90.00m, Stock = 60, Image = "pen.png", Description = "Синяя шариковая ручка с фирменным логотипом." },
                new { Name = "Магнит «Ярославль»", Category = "Сувениры", Price = 120.00m, Stock = 45, Image = "magnet.png", Description = "Сувенирный магнит с видом Ярославля." }
            };

            foreach (var product in products)
            {
                Execute(
                    @"INSERT INTO dbo.[0502_Products] (Name, Category, Price, Stock, ImageFile, Description, IsActive)
                      VALUES (@name, @category, @price, @stock, @image, @descr, 1);",
                    P("@name", product.Name), P("@category", product.Category), P("@price", product.Price),
                    P("@stock", product.Stock), P("@image", product.Image), P("@descr", product.Description));
            }
        }

        private static void SeedShipments()
        {
            var users = QueryIdList("SELECT UserId FROM dbo.[0502_Users] ORDER BY UserId;");
            var clients = QueryIdList("SELECT ClientId FROM dbo.[0502_Clients] ORDER BY ClientId;");
            var types = new List<ShipmentType>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("SELECT TypeId, Name, ImageFile, Description FROM dbo.[0502_ShipmentTypes] ORDER BY TypeId;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    types.Add(new ShipmentType
                    {
                        TypeId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ImageFile = reader.GetString(2)
                    });
                }
            }

            var random = new Random(20261031);
            var statuses = new[] { "Принято", "В обработке", "Отправлено", "Доставлено", "Доставлено", "Доставлено" };
            var comments = new[]
            {
                "Хрупкое вложение, требуется аккуратная перевозка.",
                "Отправление упаковано оператором отделения.",
                "Документы для юридического лица.",
                "Поздравительная открытка к празднику.",
                null,
                null,
                "Доставка до востребования.",
                null
            };

            for (var i = 0; i < 24; i++)
            {
                var type = types[random.Next(types.Count)];
                var sender = clients[random.Next(clients.Count)];
                var recipient = clients[random.Next(clients.Count)];
                while (recipient == sender) recipient = clients[random.Next(clients.Count)];

                decimal maxWeight;
                decimal price;
                using (var connection = OpenConnection())
                using (var command = new SqlCommand(
                    "SELECT TOP 1 TariffId, MaxWeight, Price FROM dbo.[0502_Tariffs] WHERE TypeId = @type AND IsActive = 1 ORDER BY MaxWeight;", connection))
                {
                    command.Parameters.AddWithValue("@type", type.TypeId);
                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        maxWeight = reader.GetDecimal(1);
                        price = reader.GetDecimal(2);
                    }
                }

                var weight = Math.Round((decimal)(random.NextDouble() * (double)maxWeight * 0.8 + 0.005), 3);
                var date = DateTime.Now.Date.AddDays(-random.Next(0, 14)).AddHours(random.Next(9, 19)).AddMinutes(random.Next(0, 60));
                var status = statuses[random.Next(statuses.Length)];
                var number = string.Format("ПО{0}-{1:D4}", date.ToString("yyMMdd"), 1000 + i);
                var employee = users[random.Next(users.Count)];
                var comment = comments[random.Next(comments.Length)];

                int shipmentId;
                using (var connection = OpenConnection())
                using (var command = new SqlCommand(
                    @"INSERT INTO dbo.[0502_Shipments] (Number, TypeId, SenderId, RecipientId, TariffId, Weight, Cost, Status, CreatedAt, EmployeeId, Comment)
                      VALUES (@number, @type, @sender, @recipient, @tariff, @weight, @cost, @status, @created, @employee, @comment);
                      SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
                {
                    command.Parameters.AddWithValue("@number", number);
                    command.Parameters.AddWithValue("@type", type.TypeId);
                    command.Parameters.AddWithValue("@sender", sender);
                    command.Parameters.AddWithValue("@recipient", recipient);
                    command.Parameters.AddWithValue("@tariff", GetTariffId(type.TypeId, maxWeight));
                    command.Parameters.AddWithValue("@weight", weight);
                    command.Parameters.AddWithValue("@cost", price);
                    command.Parameters.AddWithValue("@status", status);
                    command.Parameters.AddWithValue("@created", date);
                    command.Parameters.AddWithValue("@employee", employee);
                    command.Parameters.AddWithValue("@comment", (object)comment ?? DBNull.Value);
                    shipmentId = Convert.ToInt32(command.ExecuteScalar());
                }

                Execute(
                    @"INSERT INTO dbo.[0502_StatusHistory] (ShipmentId, Status, ChangedAt, EmployeeId, Comment)
                      VALUES (@id, N'Принято', @date, @employee, N'Отправление зарегистрировано');",
                    P("@id", shipmentId), P("@date", date), P("@employee", employee));

                if (status != "Принято")
                {
                    var steps = new List<string> { "В обработке", "Отправлено", "Доставлено" };
                    var targetIndex = steps.IndexOf(status);
                    for (var s = 0; s <= targetIndex; s++)
                    {
                        Execute(
                            @"INSERT INTO dbo.[0502_StatusHistory] (ShipmentId, Status, ChangedAt, EmployeeId, Comment)
                              VALUES (@id, @status, @date, @employee, @comment);",
                            P("@id", shipmentId), P("@status", steps[s]),
                            P("@date", date.AddHours(2 + s * 3)),
                            P("@employee", users[random.Next(users.Count)]),
                            P("@comment", "Статус изменён оператором"));
                    }
                }
            }
        }

        private static int GetTariffId(int typeId, decimal maxWeight)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT TOP 1 TariffId FROM dbo.[0502_Tariffs] WHERE TypeId = @type AND MaxWeight = @max;", connection))
            {
                command.Parameters.AddWithValue("@type", typeId);
                command.Parameters.AddWithValue("@max", maxWeight);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void SeedSales()
        {
            var products = new List<Product>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("SELECT ProductId, Name, Price, Stock FROM dbo.[0502_Products] ORDER BY ProductId;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    products.Add(new Product
                    {
                        ProductId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Price = reader.GetDecimal(2),
                        Stock = reader.GetInt32(3)
                    });
                }
            }

            var clients = QueryIdList("SELECT ClientId FROM dbo.[0502_Clients] ORDER BY ClientId;");
            var users = QueryIdList("SELECT UserId FROM dbo.[0502_Users] ORDER BY UserId;");
            var random = new Random(777);

            for (var i = 0; i < 18; i++)
            {
                var product = products[random.Next(products.Count)];
                var quantity = random.Next(1, 4);
                var total = product.Price * quantity;
                var date = DateTime.Now.Date.AddDays(-random.Next(0, 14)).AddHours(random.Next(9, 19)).AddMinutes(random.Next(0, 60));
                var clientId = random.Next(4) == 0 ? (int?)null : clients[random.Next(clients.Count)];

                Execute(
                    @"INSERT INTO dbo.[0502_Sales] (ProductId, ClientId, Quantity, Total, SoldAt, EmployeeId)
                      VALUES (@product, @client, @qty, @total, @date, @employee);",
                    P("@product", product.ProductId), P("@client", clientId),
                    P("@qty", quantity), P("@total", total), P("@date", date),
                    P("@employee", users[random.Next(users.Count)]));
            }
        }

        private static List<int> QueryIdList(string sql)
        {
            var result = new List<int>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) result.Add(reader.GetInt32(0));
            }
            return result;
        }

        public static DataTable QueryTable(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            using (var adapter = new SqlDataAdapter(command))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        private const string SchemaSql = @"
IF OBJECT_ID('dbo.[0502_Users]', 'U') IS NULL
CREATE TABLE dbo.[0502_Users]
(
    UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Users PRIMARY KEY,
    FullName NVARCHAR(120) NOT NULL,
    Position NVARCHAR(80) NOT NULL,
    Phone NVARCHAR(20) NULL,
    Login NVARCHAR(50) NOT NULL CONSTRAINT UQ_0502_Users_Login UNIQUE,
    PasswordHash NVARCHAR(300) NOT NULL,
    Role NVARCHAR(20) NOT NULL CONSTRAINT CK_0502_Users_Role CHECK (Role IN ('Admin', 'Operator')),
    IsActive BIT NOT NULL CONSTRAINT DF_0502_Users_Active DEFAULT(1)
);
GO
IF OBJECT_ID('dbo.[0502_Clients]', 'U') IS NULL
CREATE TABLE dbo.[0502_Clients]
(
    ClientId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Clients PRIMARY KEY,
    FullName NVARCHAR(150) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    Address NVARCHAR(250) NOT NULL,
    Email NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_0502_Clients_Created DEFAULT(SYSDATETIME())
);
GO
IF OBJECT_ID('dbo.[0502_ShipmentTypes]', 'U') IS NULL
CREATE TABLE dbo.[0502_ShipmentTypes]
(
    TypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_ShipmentTypes PRIMARY KEY,
    Name NVARCHAR(60) NOT NULL,
    Description NVARCHAR(250) NULL,
    ImageFile NVARCHAR(100) NULL
);
GO
IF OBJECT_ID('dbo.[0502_Tariffs]', 'U') IS NULL
CREATE TABLE dbo.[0502_Tariffs]
(
    TariffId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Tariffs PRIMARY KEY,
    TypeId INT NOT NULL CONSTRAINT FK_0502_Tariffs_Types REFERENCES dbo.[0502_ShipmentTypes](TypeId),
    MaxWeight DECIMAL(8,3) NOT NULL CONSTRAINT CK_0502_Tariffs_Weight CHECK (MaxWeight > 0),
    Price DECIMAL(10,2) NOT NULL CONSTRAINT CK_0502_Tariffs_Price CHECK (Price >= 0),
    IsActive BIT NOT NULL CONSTRAINT DF_0502_Tariffs_Active DEFAULT(1)
);
GO
IF OBJECT_ID('dbo.[0502_Shipments]', 'U') IS NULL
CREATE TABLE dbo.[0502_Shipments]
(
    ShipmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Shipments PRIMARY KEY,
    Number NVARCHAR(30) NOT NULL CONSTRAINT UQ_0502_Shipments_Number UNIQUE,
    TypeId INT NOT NULL CONSTRAINT FK_0502_Shipments_Types REFERENCES dbo.[0502_ShipmentTypes](TypeId),
    SenderId INT NOT NULL CONSTRAINT FK_0502_Shipments_Sender REFERENCES dbo.[0502_Clients](ClientId),
    RecipientId INT NOT NULL CONSTRAINT FK_0502_Shipments_Recipient REFERENCES dbo.[0502_Clients](ClientId),
    TariffId INT NOT NULL CONSTRAINT FK_0502_Shipments_Tariff REFERENCES dbo.[0502_Tariffs](TariffId),
    Weight DECIMAL(8,3) NOT NULL CONSTRAINT CK_0502_Shipments_Weight CHECK (Weight > 0),
    Cost DECIMAL(10,2) NOT NULL CONSTRAINT CK_0502_Shipments_Cost CHECK (Cost >= 0),
    Status NVARCHAR(30) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_0502_Shipments_Created DEFAULT(SYSDATETIME()),
    EmployeeId INT NOT NULL CONSTRAINT FK_0502_Shipments_Employee REFERENCES dbo.[0502_Users](UserId),
    Comment NVARCHAR(300) NULL
);
GO
IF OBJECT_ID('dbo.[0502_StatusHistory]', 'U') IS NULL
CREATE TABLE dbo.[0502_StatusHistory]
(
    HistoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_StatusHistory PRIMARY KEY,
    ShipmentId INT NOT NULL CONSTRAINT FK_0502_StatusHistory_Shipment REFERENCES dbo.[0502_Shipments](ShipmentId) ON DELETE CASCADE,
    Status NVARCHAR(30) NOT NULL,
    ChangedAt DATETIME2 NOT NULL,
    EmployeeId INT NOT NULL CONSTRAINT FK_0502_StatusHistory_Employee REFERENCES dbo.[0502_Users](UserId),
    Comment NVARCHAR(300) NULL
);
GO
IF OBJECT_ID('dbo.[0502_Products]', 'U') IS NULL
CREATE TABLE dbo.[0502_Products]
(
    ProductId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Products PRIMARY KEY,
    Name NVARCHAR(120) NOT NULL,
    Category NVARCHAR(60) NOT NULL,
    Price DECIMAL(10,2) NOT NULL CONSTRAINT CK_0502_Products_Price CHECK (Price >= 0),
    Stock INT NOT NULL CONSTRAINT CK_0502_Products_Stock CHECK (Stock >= 0),
    ImageFile NVARCHAR(150) NULL,
    Description NVARCHAR(400) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_0502_Products_Active DEFAULT(1)
);
GO
IF OBJECT_ID('dbo.[0502_Sales]', 'U') IS NULL
CREATE TABLE dbo.[0502_Sales]
(
    SaleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_0502_Sales PRIMARY KEY,
    ProductId INT NOT NULL CONSTRAINT FK_0502_Sales_Product REFERENCES dbo.[0502_Products](ProductId),
    ClientId INT NULL CONSTRAINT FK_0502_Sales_Client REFERENCES dbo.[0502_Clients](ClientId),
    Quantity INT NOT NULL CONSTRAINT CK_0502_Sales_Qty CHECK (Quantity > 0),
    Total DECIMAL(10,2) NOT NULL CONSTRAINT CK_0502_Sales_Total CHECK (Total >= 0),
    SoldAt DATETIME2 NOT NULL CONSTRAINT DF_0502_Sales_Date DEFAULT(SYSDATETIME()),
    EmployeeId INT NOT NULL CONSTRAINT FK_0502_Sales_Employee REFERENCES dbo.[0502_Users](UserId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_0502_Shipments_CreatedAt')
CREATE INDEX IX_0502_Shipments_CreatedAt ON dbo.[0502_Shipments](CreatedAt);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_0502_StatusHistory_Shipment')
CREATE INDEX IX_0502_StatusHistory_Shipment ON dbo.[0502_StatusHistory](ShipmentId);
GO
";
    }
}
