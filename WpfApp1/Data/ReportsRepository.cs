using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using WpfApp1.Models;

namespace WpfApp1.Data
{
    public static class ReportsRepository
    {
        public static ReportResult BuildShipmentReport(DateTime from, DateTime to)
        {
            var result = new ReportResult { Rows = new List<ReportRow>(), Days = new List<DayStat>() };

            using (var connection = Db.OpenConnection())
            {
                using (var command = new SqlCommand(
                    @"SELECT COUNT(*), ISNULL(SUM(Cost), 0)
                      FROM dbo.[0502_Shipments]
                      WHERE CreatedAt >= @from AND CreatedAt < DATEADD(DAY, 1, @to);", connection))
                {
                    command.Parameters.AddWithValue("@from", from.Date);
                    command.Parameters.AddWithValue("@to", to.Date);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result.Count = reader.GetInt32(0);
                            result.Total = reader.GetDecimal(1);
                        }
                    }
                }

                using (var command = new SqlCommand(
                    @"SELECT t.Name, COUNT(*), ISNULL(SUM(s.Cost), 0)
                      FROM dbo.[0502_Shipments] s
                      JOIN dbo.[0502_ShipmentTypes] t ON t.TypeId = s.TypeId
                      WHERE s.CreatedAt >= @from AND s.CreatedAt < DATEADD(DAY, 1, @to)
                      GROUP BY t.Name
                      ORDER BY COUNT(*) DESC;", connection))
                {
                    command.Parameters.AddWithValue("@from", from.Date);
                    command.Parameters.AddWithValue("@to", to.Date);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Rows.Add(new ReportRow
                            {
                                Group = reader.GetString(0),
                                Count = reader.GetInt32(1),
                                Total = reader.GetDecimal(2)
                            });
                        }
                    }
                }

                result.Days = BuildShipmentDays(connection, from, to);
            }

            return result;
        }

        public static ReportResult BuildSalesReport(DateTime from, DateTime to)
        {
            var result = new ReportResult { Rows = new List<ReportRow>(), Days = new List<DayStat>() };

            using (var connection = Db.OpenConnection())
            {
                using (var command = new SqlCommand(
                    @"SELECT ISNULL(SUM(Quantity), 0), ISNULL(SUM(Total), 0)
                      FROM dbo.[0502_Sales]
                      WHERE SoldAt >= @from AND SoldAt < DATEADD(DAY, 1, @to);", connection))
                {
                    command.Parameters.AddWithValue("@from", from.Date);
                    command.Parameters.AddWithValue("@to", to.Date);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result.Count = reader.GetInt32(0);
                            result.Total = reader.GetDecimal(1);
                        }
                    }
                }

                using (var command = new SqlCommand(
                    @"SELECT p.Name, SUM(s.Quantity), SUM(s.Total)
                      FROM dbo.[0502_Sales] s
                      JOIN dbo.[0502_Products] p ON p.ProductId = s.ProductId
                      WHERE s.SoldAt >= @from AND s.SoldAt < DATEADD(DAY, 1, @to)
                      GROUP BY p.Name
                      ORDER BY SUM(s.Total) DESC;", connection))
                {
                    command.Parameters.AddWithValue("@from", from.Date);
                    command.Parameters.AddWithValue("@to", to.Date);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Rows.Add(new ReportRow
                            {
                                Group = reader.GetString(0),
                                Count = reader.GetInt32(1),
                                Total = reader.GetDecimal(2)
                            });
                        }
                    }
                }

                result.Days = BuildSalesDays(connection, from, to);
            }

            return result;
        }

        public static List<DayStat> BuildLastWeekStats()
        {
            var from = DateTime.Now.Date.AddDays(-6);
            using (var connection = Db.OpenConnection())
            {
                return BuildShipmentDays(connection, from, DateTime.Now.Date);
            }
        }

        public static decimal RevenueThisMonth()
        {
            var shipmentRevenue = Convert.ToDecimal(Db.Scalar(
                @"SELECT ISNULL(SUM(Cost), 0) FROM dbo.[0502_Shipments]
                  WHERE CreatedAt >= DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1);"));
            return shipmentRevenue + ProductsRepository.SalesRevenueThisMonth();
        }

        public static int CountByStatus(string status)
        {
            return ShipmentsRepository.CountByStatus(status);
        }

        private static List<DayStat> BuildShipmentDays(SqlConnection connection, DateTime from, DateTime to)
        {
            var counts = new Dictionary<DateTime, int>();
            var sums = new Dictionary<DateTime, decimal>();
            using (var command = new SqlCommand(
                @"SELECT CAST(CreatedAt AS DATE), COUNT(*), ISNULL(SUM(Cost), 0)
                  FROM dbo.[0502_Shipments]
                  WHERE CreatedAt >= @from AND CreatedAt < DATEADD(DAY, 1, @to)
                  GROUP BY CAST(CreatedAt AS DATE);", connection))
            {
                command.Parameters.AddWithValue("@from", from.Date);
                command.Parameters.AddWithValue("@to", to.Date);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var date = reader.GetDateTime(0).Date;
                        counts[date] = reader.GetInt32(1);
                        sums[date] = reader.GetDecimal(2);
                    }
                }
            }

            return MakeDays(from, to, counts, sums);
        }

        private static List<DayStat> BuildSalesDays(SqlConnection connection, DateTime from, DateTime to)
        {
            var counts = new Dictionary<DateTime, int>();
            var sums = new Dictionary<DateTime, decimal>();
            using (var command = new SqlCommand(
                @"SELECT CAST(SoldAt AS DATE), ISNULL(SUM(Quantity), 0), ISNULL(SUM(Total), 0)
                  FROM dbo.[0502_Sales]
                  WHERE SoldAt >= @from AND SoldAt < DATEADD(DAY, 1, @to)
                  GROUP BY CAST(SoldAt AS DATE);", connection))
            {
                command.Parameters.AddWithValue("@from", from.Date);
                command.Parameters.AddWithValue("@to", to.Date);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var date = reader.GetDateTime(0).Date;
                        counts[date] = reader.GetInt32(1);
                        sums[date] = reader.GetDecimal(2);
                    }
                }
            }

            return MakeDays(from, to, counts, sums);
        }

        private static List<DayStat> MakeDays(DateTime from, DateTime to, Dictionary<DateTime, int> counts, Dictionary<DateTime, decimal> sums)
        {
            var days = new List<DayStat>();
            var start = from.Date;
            var end = to.Date;
            if ((end - start).TotalDays > 31) start = end.AddDays(-31);

            var max = 0;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                int count;
                counts.TryGetValue(date, out count);
                if (count > max) max = count;
            }

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                int count;
                decimal sum;
                counts.TryGetValue(date, out count);
                sums.TryGetValue(date, out sum);
                days.Add(new DayStat
                {
                    Date = date,
                    Count = count,
                    Total = sum,
                    BarHeight = max == 0 ? 6 : Math.Max(6, 110.0 * count / max)
                });
            }

            return days;
        }
    }
}
