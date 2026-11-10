using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class ClientsRepository
    {
        private const string SelectColumns =
            "ClientId, FullName, Phone, Address, Email, CreatedAt";

        public static List<Client> GetAll(string search = null)
        {
            var result = new List<Client>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                "SELECT " + SelectColumns + " FROM dbo.[0502_Clients] " +
                "WHERE (@search = N'' OR FullName LIKE @like OR Phone LIKE @like OR Address LIKE @like) " +
                "ORDER BY FullName;", connection))
            {
                command.Parameters.AddWithValue("@search", search ?? "");
                command.Parameters.AddWithValue("@like", "%" + (search ?? "").Trim() + "%");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }
            return result;
        }

        public static Client GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand("SELECT " + SelectColumns + " FROM dbo.[0502_Clients] WHERE ClientId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static int Insert(Client client)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.[0502_Clients] (FullName, Phone, Address, Email, CreatedAt)
                  VALUES (@name, @phone, @address, @email, SYSDATETIME());
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                command.Parameters.AddWithValue("@name", client.FullName.Trim());
                command.Parameters.AddWithValue("@phone", client.Phone.Trim());
                command.Parameters.AddWithValue("@address", client.Address.Trim());
                command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(client.Email) ? (object)DBNull.Value : client.Email.Trim());
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public static void Update(Client client)
        {
            Db.Execute(
                @"UPDATE dbo.[0502_Clients] SET FullName = @name, Phone = @phone, Address = @address, Email = @email
                  WHERE ClientId = @id;",
                Db.P("@name", client.FullName.Trim()), Db.P("@phone", client.Phone.Trim()),
                Db.P("@address", client.Address.Trim()),
                Db.P("@email", string.IsNullOrWhiteSpace(client.Email) ? null : client.Email.Trim()),
                Db.P("@id", client.ClientId));
        }

        public static string Delete(int clientId)
        {
            var used = Convert.ToInt32(Db.Scalar(
                "SELECT COUNT(*) FROM dbo.[0502_Shipments] WHERE SenderId = @id OR RecipientId = @id;",
                Db.P("@id", clientId)));
            if (used > 0)
                return "Клиент связан с отправлениями и не может быть удалён.";

            var sales = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Sales] WHERE ClientId = @id;", Db.P("@id", clientId)));
            if (sales > 0)
                return "Клиент связан с продажами товаров и не может быть удалён.";

            Db.Execute("DELETE FROM dbo.[0502_Clients] WHERE ClientId = @id;", Db.P("@id", clientId));
            return null;
        }

        public static int Count()
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Clients];"));
        }

        private static Client Map(SqlDataReader reader)
        {
            return new Client
            {
                ClientId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Phone = reader.GetString(2),
                Address = reader.GetString(3),
                Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            };
        }
    }
}
