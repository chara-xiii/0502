using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class UsersRepository
    {
        private const string SelectColumns =
            "UserId, FullName, Position, Phone, Login, PasswordHash, Role, IsActive";

        public static List<User> GetAll()
        {
            var result = new List<User>();
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand("SELECT " + SelectColumns + " FROM dbo.[0502_Users] ORDER BY FullName;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) result.Add(Map(reader));
            }
            return result;
        }

        public static User GetByLogin(string login)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand("SELECT " + SelectColumns + " FROM dbo.[0502_Users] WHERE Login = @login;", connection))
            {
                command.Parameters.AddWithValue("@login", login);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static User GetById(int id)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand("SELECT " + SelectColumns + " FROM dbo.[0502_Users] WHERE UserId = @id;", connection))
            {
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static bool LoginExists(string login, int exceptId)
        {
            var value = Db.Scalar("SELECT COUNT(*) FROM dbo.[0502_Users] WHERE Login = @login AND UserId <> @id;",
                Db.P("@login", login), Db.P("@id", exceptId));
            return Convert.ToInt32(value) > 0;
        }

        public static int Insert(User user, string password)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.[0502_Users] (FullName, Position, Phone, Login, PasswordHash, Role, IsActive)
                  VALUES (@name, @position, @phone, @login, @hash, @role, @active);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                command.Parameters.AddWithValue("@name", user.FullName);
                command.Parameters.AddWithValue("@position", user.Position);
                command.Parameters.AddWithValue("@phone", (object)user.Phone ?? DBNull.Value);
                command.Parameters.AddWithValue("@login", user.Login);
                command.Parameters.AddWithValue("@hash", Services.PasswordHasher.Hash(password));
                command.Parameters.AddWithValue("@role", user.Role);
                command.Parameters.AddWithValue("@active", user.IsActive);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public static void Update(User user, string newPassword)
        {
            using (var connection = Db.OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.[0502_Users] SET FullName = @name, Position = @position, Phone = @phone,
                         Login = @login, Role = @role, IsActive = @active
                  WHERE UserId = @id;", connection))
            {
                command.Parameters.AddWithValue("@name", user.FullName);
                command.Parameters.AddWithValue("@position", user.Position);
                command.Parameters.AddWithValue("@phone", (object)user.Phone ?? DBNull.Value);
                command.Parameters.AddWithValue("@login", user.Login);
                command.Parameters.AddWithValue("@role", user.Role);
                command.Parameters.AddWithValue("@active", user.IsActive);
                command.Parameters.AddWithValue("@id", user.UserId);
                command.ExecuteNonQuery();
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                Db.Execute("UPDATE dbo.[0502_Users] SET PasswordHash = @hash WHERE UserId = @id;",
                    Db.P("@hash", Services.PasswordHasher.Hash(newPassword)), Db.P("@id", user.UserId));
            }
        }

        public static void SetActive(int userId, bool active)
        {
            Db.Execute("UPDATE dbo.[0502_Users] SET IsActive = @active WHERE UserId = @id;",
                Db.P("@active", active), Db.P("@id", userId));
        }

        private static User Map(SqlDataReader reader)
        {
            return new User
            {
                UserId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Position = reader.GetString(2),
                Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Login = reader.GetString(4),
                PasswordHash = reader.GetString(5),
                Role = reader.GetString(6),
                IsActive = reader.GetBoolean(7)
            };
        }
    }
}
