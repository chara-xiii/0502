using System;

namespace WpfApp1.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Phone { get; set; }
        public string Login { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }

        public string RoleDisplay
        {
            get { return Role == "Admin" ? "Администратор" : "Оператор"; }
        }

        public string ActiveDisplay
        {
            get { return IsActive ? "Активен" : "Заблокирован"; }
        }
    }

    public class Client
    {
        public int ClientId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CreatedAtText
        {
            get { return CreatedAt.ToString("dd.MM.yyyy"); }
        }
    }

    public class ShipmentType
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageFile { get; set; }
    }

    public class Tariff
    {
        public int TariffId { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public decimal MaxWeight { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }

        public string MaxWeightText
        {
            get { return MaxWeight.ToString("0.###") + " кг"; }
        }

        public string PriceText
        {
            get { return Price.ToString("N2") + " ₽"; }
        }

        public string ActiveDisplay
        {
            get { return IsActive ? "Действует" : "Не действует"; }
        }
    }

    public class Shipment
    {
        public int ShipmentId { get; set; }
        public string Number { get; set; }
        public int TypeId { get; set; }
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
        public int TariffId { get; set; }
        public decimal Weight { get; set; }
        public decimal Cost { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EmployeeId { get; set; }
        public string Comment { get; set; }
    }

    public class ShipmentView
    {
        public int ShipmentId { get; set; }
        public string Number { get; set; }
        public string TypeName { get; set; }
        public string TypeImage { get; set; }
        public string SenderName { get; set; }
        public string SenderPhone { get; set; }
        public string SenderAddress { get; set; }
        public string RecipientName { get; set; }
        public string RecipientPhone { get; set; }
        public string RecipientAddress { get; set; }
        public decimal Weight { get; set; }
        public decimal Cost { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EmployeeName { get; set; }
        public string Comment { get; set; }
        public int TypeId { get; set; }
        public int SenderId { get; set; }
        public int RecipientId { get; set; }

        public string CreatedAtText
        {
            get { return CreatedAt.ToString("dd.MM.yyyy HH:mm"); }
        }

        public string DateText
        {
            get { return CreatedAt.ToString("dd.MM.yyyy"); }
        }

        public string WeightText
        {
            get { return Weight.ToString("0.###") + " кг"; }
        }

        public string CostText
        {
            get { return Cost.ToString("N2") + " ₽"; }
        }

        public string ShortComment
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Comment)) return "—";
                return Comment.Length > 60 ? Comment.Substring(0, 60) + "…" : Comment;
            }
        }
    }

    public class StatusHistoryItem
    {
        public int HistoryId { get; set; }
        public string Status { get; set; }
        public DateTime ChangedAt { get; set; }
        public string EmployeeName { get; set; }
        public string Comment { get; set; }

        public string ChangedAtText
        {
            get { return ChangedAt.ToString("dd.MM.yyyy HH:mm"); }
        }
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string ImageFile { get; set; }
        public bool IsActive { get; set; }

        public string PriceText
        {
            get { return Price.ToString("N2") + " ₽"; }
        }

        public string StockText
        {
            get { return Stock > 0 ? "В наличии: " + Stock + " шт." : "Нет в наличии"; }
        }

        public bool CanSell
        {
            get { return IsActive && Stock > 0; }
        }
    }

    public class Sale
    {
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public int? ClientId { get; set; }
        public string ProductName { get; set; }
        public string ClientName { get; set; }
        public int Quantity { get; set; }
        public decimal Total { get; set; }
        public DateTime SoldAt { get; set; }
        public string EmployeeName { get; set; }

        public string SoldAtText
        {
            get { return SoldAt.ToString("dd.MM.yyyy HH:mm"); }
        }

        public string TotalText
        {
            get { return Total.ToString("N2") + " ₽"; }
        }
    }

    public class ReportRow
    {
        public string Group { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }

        public string TotalText
        {
            get { return Total.ToString("N2") + " ₽"; }
        }
    }

    public class DayStat
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }
        public double BarHeight { get; set; }

        public string Label
        {
            get { return Date.ToString("dd.MM"); }
        }
    }

    public class ReportResult
    {
        public int Count { get; set; }
        public decimal Total { get; set; }
        public System.Collections.Generic.List<ReportRow> Rows { get; set; }
        public System.Collections.Generic.List<DayStat> Days { get; set; }

        public string TotalText
        {
            get { return Total.ToString("N2") + " ₽"; }
        }
    }

    public class SelfTestResult
    {
        public string Name { get; set; }
        public string Expected { get; set; }
        public string Actual { get; set; }
        public bool Passed { get; set; }

        public string StatusText
        {
            get { return Passed ? "Пройден" : "Не пройден"; }
        }
    }
}
