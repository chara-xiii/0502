using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class ShipmentsRepository
    {
        public static readonly string[] Statuses =
        {
            "Принято", "В обработке", "Отправлено", "Доставлено", "Возврат"
        };

        private const string SelectSql = @"
SELECT s.ShipmentId, s.Number, t.Name AS TypeName, t.ImageFile,
       s.SenderId, sender.FullName AS SenderName, sender.Phone AS SenderPhone, sender.Address AS SenderAddress,
       s.RecipientId, recipient.FullName AS RecipientName, recipient.Phone AS RecipientPhone, recipient.Address AS RecipientAddress,
       s.Weight, s.Cost, s.Status, s.CreatedAt, u.FullName AS EmployeeName, s.Comment, s.TypeId
FROM dbo.[0502_Shipments] s
JOIN dbo.[0502_ShipmentTypes] t ON t.TypeId = s.TypeId
JOIN dbo.[0502_Clients] sender ON sender.ClientId = s.SenderId
JOIN dbo.[0502_Clients] recipient ON recipient.ClientId = s.RecipientId
JOIN dbo.[0502_Users] u ON u.UserId = s.EmployeeId
";

        public static List<ShipmentView> GetAll(string search, string status, DateTime? from, DateTime? to)
        {
            var result = new List<ShipmentView>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                SelectSql + @"
WHERE (@search = N'' OR s.Number LIKE @like OR sender.FullName LIKE @like OR recipient.FullName LIKE @like)
  AND (@status = N'' OR s.Status = @status)
  AND (@from IS NULL OR s.CreatedAt >= @from)
  AND (@to IS NULL OR s.CreatedAt < DATEADD(DAY, 1, @to))
ORDER BY s.CreatedAt DESC;", connection))
            {
                command.Parameters.AddWithValue("@search", search ?? "");
                command.Parameters.AddWithValue("@like", "%" + (search ?? "").Trim() + "%");
                command.Parameters.AddWithValue("@status", status ?? "");
                command.Parameters.AddWithValue("@from", (object)from ?? DBNull.Value);
                command.Parameters.AddWithValue("@to", (object)to ?? DBNull.Value);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }
            return result;
        }

        public static ShipmentView GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(SelectSql + " WHERE s.ShipmentId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static ShipmentView GetByNumber(string number)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(SelectSql + " WHERE s.Number = @number;", connection))
            {
                command.Parameters.AddWithValue("@number", number);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static List<ShipmentView> GetRecent(int limit)
        {
            var result = new List<ShipmentView>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(SelectSql + " ORDER BY s.CreatedAt DESC OFFSET 0 ROWS FETCH NEXT @limit ROWS ONLY;", connection))
            {
                command.Parameters.AddWithValue("@limit", limit);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }
            return result;
        }

        public static string Create(Shipment shipment, string initialComment)
        {
            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var number = GenerateNumber(connection, transaction);
                    int shipmentId;
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.[0502_Shipments]
                            (Number, TypeId, SenderId, RecipientId, TariffId, Weight, Cost, Status, CreatedAt, EmployeeId, Comment)
                          VALUES
                            (@number, @type, @sender, @recipient, @tariff, @weight, @cost, N'Принято', SYSDATETIME(), @employee, @comment);
                          SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@number", number);
                        command.Parameters.AddWithValue("@type", shipment.TypeId);
                        command.Parameters.AddWithValue("@sender", shipment.SenderId);
                        command.Parameters.AddWithValue("@recipient", shipment.RecipientId);
                        command.Parameters.AddWithValue("@tariff", shipment.TariffId);
                        command.Parameters.AddWithValue("@weight", shipment.Weight);
                        command.Parameters.AddWithValue("@cost", shipment.Cost);
                        command.Parameters.AddWithValue("@employee", shipment.EmployeeId);
                        command.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(initialComment) ? (object)DBNull.Value : initialComment.Trim());
                        shipmentId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.[0502_StatusHistory] (ShipmentId, Status, ChangedAt, EmployeeId, Comment)
                          VALUES (@id, N'Принято', SYSDATETIME(), @employee, N'Отправление зарегистрировано');", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@id", shipmentId);
                        command.Parameters.AddWithValue("@employee", shipment.EmployeeId);
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return number;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static void Update(Shipment shipment)
        {
            Db.Execute(
                @"UPDATE dbo.[0502_Shipments]
                  SET TypeId = @type, SenderId = @sender, RecipientId = @recipient,
                      TariffId = @tariff, Weight = @weight, Cost = @cost, Comment = @comment
                  WHERE ShipmentId = @id;",
                Db.P("@type", shipment.TypeId), Db.P("@sender", shipment.SenderId),
                Db.P("@recipient", shipment.RecipientId), Db.P("@tariff", shipment.TariffId),
                Db.P("@weight", shipment.Weight), Db.P("@cost", shipment.Cost),
                Db.P("@comment", string.IsNullOrWhiteSpace(shipment.Comment) ? null : shipment.Comment.Trim()),
                Db.P("@id", shipment.ShipmentId));
        }

        public static void UpdateStatus(int shipmentId, string status, int employeeId, string comment)
        {
            using (var connection = Db.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = new SqlCommand(
                        "UPDATE dbo.[0502_Shipments] SET Status = @status WHERE ShipmentId = @id;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@status", status);
                        command.Parameters.AddWithValue("@id", shipmentId);
                        command.ExecuteNonQuery();
                    }

                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.[0502_StatusHistory] (ShipmentId, Status, ChangedAt, EmployeeId, Comment)
                          VALUES (@id, @status, SYSDATETIME(), @employee, @comment);", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@id", shipmentId);
                        command.Parameters.AddWithValue("@status", status);
                        command.Parameters.AddWithValue("@employee", employeeId);
                        command.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? (object)DBNull.Value : comment.Trim());
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static void Delete(int shipmentId)
        {
            Db.Execute("DELETE FROM dbo.[0502_Shipments] WHERE ShipmentId = @id;", Db.P("@id", shipmentId));
        }

        public static List<StatusHistoryItem> GetHistory(int shipmentId)
        {
            var result = new List<StatusHistoryItem>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT h.HistoryId, h.Status, h.ChangedAt, u.FullName, h.Comment
                  FROM dbo.[0502_StatusHistory] h
                  JOIN dbo.[0502_Users] u ON u.UserId = h.EmployeeId
                  WHERE h.ShipmentId = @id
                  ORDER BY h.ChangedAt;", connection))
            {
                command.Parameters.AddWithValue("@id", shipmentId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new StatusHistoryItem
                        {
                            HistoryId = reader.GetInt32(0),
                            Status = reader.GetString(1),
                            ChangedAt = reader.GetDateTime(2),
                            EmployeeName = reader.GetString(3),
                            Comment = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        });
                    }
                }
            }
            return result;
        }

        public static int CountTotal()
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Shipments];"));
        }

        public static int CountToday()
        {
            return Convert.ToInt32(Db.Scalar(
                "SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE CAST(CreatedAt AS DATE) = CAST(SYSDATETIME() AS DATE);"));
        }

        public static int CountByStatus(string status)
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE Status = @status;", Db.P("@status", status)));
        }

        private static string GenerateNumber(SqlConnection connection, SqlTransaction transaction)
        {
            var prefix = "ПО" + DateTime.Now.ToString("yyMMdd") + "-";
            using (var command = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE Number LIKE @prefix + '%';", connection, transaction))
            {
                command.Parameters.AddWithValue("@prefix", prefix);
                var sequence = Convert.ToInt32(command.ExecuteScalar()) + 1;
                while (true)
                {
                    var candidate = prefix + sequence.ToString("D4");
                    using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE Number = @number;", connection, transaction))
                    {
                        check.Parameters.AddWithValue("@number", candidate);
                        if (Convert.ToInt32(check.ExecuteScalar()) == 0) return candidate;
                    }
                    sequence++;
                }
            }
        }

        private static ShipmentView Map(SqlDataReader reader)
        {
            return new ShipmentView
            {
                ShipmentId = reader.GetInt32(0),
                Number = reader.GetString(1),
                TypeName = reader.GetString(2),
                TypeImage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                SenderId = reader.GetInt32(4),
                SenderName = reader.GetString(5),
                SenderPhone = reader.GetString(6),
                SenderAddress = reader.GetString(7),
                RecipientId = reader.GetInt32(8),
                RecipientName = reader.GetString(9),
                RecipientPhone = reader.GetString(10),
                RecipientAddress = reader.GetString(11),
                Weight = reader.GetDecimal(12),
                Cost = reader.GetDecimal(13),
                Status = reader.GetString(14),
                CreatedAt = reader.GetDateTime(15),
                EmployeeName = reader.GetString(16),
                Comment = reader.IsDBNull(17) ? "" : reader.GetString(17),
                TypeId = reader.GetInt32(18)
            };
        }
    }
}
