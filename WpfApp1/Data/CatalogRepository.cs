using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class TypesRepository
    {
        public static List<ShipmentType> GetAll()
        {
            var result = new List<ShipmentType>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                "SELECT TypeId, Name, Description, ImageFile FROM dbo.[0502_ShipmentTypes] ORDER BY TypeId;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) result.Add(Map(reader));
            }
            return result;
        }

        public static ShipmentType GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                "SELECT TypeId, Name, Description, ImageFile FROM dbo.[0502_ShipmentTypes] WHERE TypeId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        private static ShipmentType Map(SqlDataReader reader)
        {
            return new ShipmentType
            {
                TypeId = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ImageFile = reader.IsDBNull(3) ? "" : reader.GetString(3)
            };
        }
    }

    public static class TariffsRepository
    {
        public static List<Tariff> GetAll(bool activeOnly = false)
        {
            var result = new List<Tariff>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT t.TariffId, t.TypeId, st.Name, t.MaxWeight, t.Price, t.IsActive
                  FROM dbo.[0502_Tariffs] t
                  JOIN dbo.[0502_ShipmentTypes] st ON st.TypeId = t.TypeId
                  WHERE (@active = 0 OR t.IsActive = 1)
                  ORDER BY st.Name, t.MaxWeight;", connection))
            {
                command.Parameters.AddWithValue("@active", activeOnly ? 1 : 0);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Tariff
                        {
                            TariffId = reader.GetInt32(0),
                            TypeId = reader.GetInt32(1),
                            TypeName = reader.GetString(2),
                            MaxWeight = reader.GetDecimal(3),
                            Price = reader.GetDecimal(4),
                            IsActive = reader.GetBoolean(5)
                        });
                    }
                }
            }
            return result;
        }

        public static Tariff GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT t.TariffId, t.TypeId, st.Name, t.MaxWeight, t.Price, t.IsActive
                  FROM dbo.[0502_Tariffs] t
                  JOIN dbo.[0502_ShipmentTypes] st ON st.TypeId = t.TypeId
                  WHERE t.TariffId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new Tariff
                    {
                        TariffId = reader.GetInt32(0),
                        TypeId = reader.GetInt32(1),
                        TypeName = reader.GetString(2),
                        MaxWeight = reader.GetDecimal(3),
                        Price = reader.GetDecimal(4),
                        IsActive = reader.GetBoolean(5)
                    };
                }
            }
        }

        public static Tariff FindForWeight(int typeId, decimal weight)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT TOP 1 t.TariffId, t.TypeId, st.Name, t.MaxWeight, t.Price, t.IsActive
                  FROM dbo.[0502_Tariffs] t
                  JOIN dbo.[0502_ShipmentTypes] st ON st.TypeId = t.TypeId
                  WHERE t.TypeId = @type AND t.IsActive = 1 AND t.MaxWeight >= @weight
                  ORDER BY t.MaxWeight, t.Price;", connection))
            {
                command.Parameters.AddWithValue("@type", typeId);
                command.Parameters.AddWithValue("@weight", weight);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new Tariff
                    {
                        TariffId = reader.GetInt32(0),
                        TypeId = reader.GetInt32(1),
                        TypeName = reader.GetString(2),
                        MaxWeight = reader.GetDecimal(3),
                        Price = reader.GetDecimal(4),
                        IsActive = reader.GetBoolean(5)
                    };
                }
            }
        }

        public static int Insert(Tariff tariff)
        {
            return Convert.ToInt32(Db.Scalar(
                @"INSERT INTO dbo.[0502_Tariffs] (TypeId, MaxWeight, Price, IsActive)
                  VALUES (@type, @max, @price, @active);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                Db.P("@type", tariff.TypeId), Db.P("@max", tariff.MaxWeight),
                Db.P("@price", tariff.Price), Db.P("@active", tariff.IsActive)));
        }

        public static void Update(Tariff tariff)
        {
            Db.Execute(
                @"UPDATE dbo.[0502_Tariffs] SET TypeId = @type, MaxWeight = @max, Price = @price, IsActive = @active
                  WHERE TariffId = @id;",
                Db.P("@type", tariff.TypeId), Db.P("@max", tariff.MaxWeight),
                Db.P("@price", tariff.Price), Db.P("@active", tariff.IsActive),
                Db.P("@id", tariff.TariffId));
        }

        public static string Delete(int tariffId)
        {
            var used = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE TariffId = @id;", Db.P("@id", tariffId)));
            if (used > 0) return "Тариф уже применён в отправлениях. Его можно только отключить.";
            Db.Execute("DELETE FROM dbo.[0502_Tariffs] WHERE TariffId = @id;", Db.P("@id", tariffId));
            return null;
        }

        public static void Toggle(int tariffId, bool active)
        {
            Db.Execute("UPDATE dbo.[0502_Tariffs] SET IsActive = @active WHERE TariffId = @id;",
                Db.P("@active", active), Db.P("@id", tariffId));
        }
    }

    public static class ProductsRepository
    {
        public static List<Product> GetAll(string search = null, string category = null)
        {
            var result = new List<Product>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT ProductId, Name, Category, Price, Stock, ImageFile, Description, IsActive
                  FROM dbo.[0502_Products]
                  WHERE (@search = N'' OR Name LIKE @like OR Description LIKE @like)
                    AND (@category = N'' OR Category = @category)
                  ORDER BY Category, Name;", connection))
            {
                command.Parameters.AddWithValue("@search", search ?? "");
                command.Parameters.AddWithValue("@like", "%" + (search ?? "").Trim() + "%");
                command.Parameters.AddWithValue("@category", category ?? "");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }
            return result;
        }

        public static List<Product> GetTop(int limit)
        {
            var result = new List<Product>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT TOP (@limit) ProductId, Name, Category, Price, Stock, ImageFile, Description, IsActive
                  FROM dbo.[0502_Products] WHERE IsActive = 1
                  ORDER BY Stock DESC, Price DESC;", connection))
            {
                command.Parameters.AddWithValue("@limit", limit);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }
            return result;
        }

        public static Product GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT ProductId, Name, Category, Price, Stock, ImageFile, Description, IsActive
                  FROM dbo.[0502_Products] WHERE ProductId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static List<string> GetCategories()
        {
            var result = new List<string>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand("SELECT DISTINCT Category FROM dbo.[0502_Products] ORDER BY Category;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) result.Add(reader.GetString(0));
            }
            return result;
        }

        public static int Insert(Product product)
        {
            return Convert.ToInt32(Db.Scalar(
                @"INSERT INTO dbo.[0502_Products] (Name, Category, Price, Stock, ImageFile, Description, IsActive)
                  VALUES (@name, @category, @price, @stock, @image, @descr, @active);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                Db.P("@name", product.Name.Trim()), Db.P("@category", product.Category.Trim()),
                Db.P("@price", product.Price), Db.P("@stock", product.Stock),
                Db.P("@image", product.ImageFile), Db.P("@descr", product.Description),
                Db.P("@active", product.IsActive)));
        }

        public static void Update(Product product)
        {
            Db.Execute(
                @"UPDATE dbo.[0502_Products] SET Name = @name, Category = @category, Price = @price,
                         Stock = @stock, ImageFile = @image, Description = @descr, IsActive = @active
                  WHERE ProductId = @id;",
                Db.P("@name", product.Name.Trim()), Db.P("@category", product.Category.Trim()),
                Db.P("@price", product.Price), Db.P("@stock", product.Stock),
                Db.P("@image", product.ImageFile), Db.P("@descr", product.Description),
                Db.P("@active", product.IsActive), Db.P("@id", product.ProductId));
        }

        public static string Delete(int productId)
        {
            var used = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Sales] WHERE ProductId = @id;", Db.P("@id", productId)));
            if (used > 0) return "Товар уже продавался. Удаление недоступно — снимите его с продажи.";
            Db.Execute("DELETE FROM dbo.[0502_Products] WHERE ProductId = @id;", Db.P("@id", productId));
            return null;
        }

        public static void SetActive(int productId, bool active)
        {
            Db.Execute("UPDATE dbo.[0502_Products] SET IsActive = @active WHERE ProductId = @id;",
                Db.P("@active", active), Db.P("@id", productId));
        }

        public static decimal Sell(int productId, int? clientId, int quantity, int employeeId)
        {
            if (quantity <= 0) throw new ArgumentException("Количество должно быть больше нуля.");

            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    decimal price;
                    int stock;
                    using (var command = new SqlCommand(
                        "SELECT Price, Stock FROM dbo.[0502_Products] WHERE ProductId = @id AND IsActive = 1;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@id", productId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read()) throw new InvalidOperationException("Товар не найден или снят с продажи.");
                            price = reader.GetDecimal(0);
                            stock = reader.GetInt32(1);
                        }
                    }

                    if (stock < quantity)
                        throw new InvalidOperationException("Недостаточно товара на складе. Доступно: " + stock + " шт.");

                    var total = price * quantity;
                    using (var command = new SqlCommand(
                        @"UPDATE dbo.[0502_Products] SET Stock = Stock - @qty WHERE ProductId = @id;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@qty", quantity);
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }

                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.[0502_Sales] (ProductId, ClientId, Quantity, Total, SoldAt, EmployeeId)
                          VALUES (@product, @client, @qty, @total, SYSDATETIME(), @employee);", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@product", productId);
                        command.Parameters.AddWithValue("@client", clientId.HasValue ? (object)clientId.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@qty", quantity);
                        command.Parameters.AddWithValue("@total", total);
                        command.Parameters.AddWithValue("@employee", employeeId);
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return total;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static List<Sale> GetSales(DateTime? from, DateTime? to, string search = null)
        {
            var result = new List<Sale>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT s.SaleId, s.ProductId, s.ClientId, p.Name, ISNULL(c.FullName, N'Розничная продажа'),
                         s.Quantity, s.Total, s.SoldAt, u.FullName
                  FROM dbo.[0502_Sales] s
                  JOIN dbo.[0502_Products] p ON p.ProductId = s.ProductId
                  LEFT JOIN dbo.[0502_Clients] c ON c.ClientId = s.ClientId
                  JOIN dbo.[0502_Users] u ON u.UserId = s.EmployeeId
                  WHERE (@from IS NULL OR s.SoldAt >= @from)
                    AND (@to IS NULL OR s.SoldAt < DATEADD(DAY, 1, @to))
                    AND (@search = N'' OR p.Name LIKE @like OR ISNULL(c.FullName, N'') LIKE @like)
                  ORDER BY s.SoldAt DESC;", connection))
            {
                command.Parameters.AddWithValue("@from", (object)from ?? DBNull.Value);
                command.Parameters.AddWithValue("@to", (object)to ?? DBNull.Value);
                command.Parameters.AddWithValue("@search", search ?? "");
                command.Parameters.AddWithValue("@like", "%" + (search ?? "").Trim() + "%");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Sale
                        {
                            SaleId = reader.GetInt32(0),
                            ProductId = reader.GetInt32(1),
                            ClientId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                            ProductName = reader.GetString(3),
                            ClientName = reader.GetString(4),
                            Quantity = reader.GetInt32(5),
                            Total = reader.GetDecimal(6),
                            SoldAt = reader.GetDateTime(7),
                            EmployeeName = reader.GetString(8)
                        });
                    }
                }
            }
            return result;
        }

        public static decimal SalesRevenueThisMonth()
        {
            return Convert.ToDecimal(Db.Scalar(
                @"SELECT ISNULL(SUM(Total), 0) FROM dbo.[0502_Sales]
                  WHERE SoldAt >= DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1);"));
        }

        public static int TotalStock()
        {
            return Convert.ToInt32(Db.Scalar("SELECT ISNULL(SUM(Stock), 0) FROM dbo.[0502_Products] WHERE IsActive = 1;"));
        }

        private static Product Map(SqlDataReader reader)
        {
            return new Product
            {
                ProductId = reader.GetInt32(0),
                Name = reader.GetString(1),
                Category = reader.GetString(2),
                Price = reader.GetDecimal(3),
                Stock = reader.GetInt32(4),
                ImageFile = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                IsActive = reader.GetBoolean(7)
            };
        }
    }
}
