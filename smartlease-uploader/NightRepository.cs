using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartleaseUploader
{
    public class NightEntry
    {
        public int Id { get; set; }
        public string Time { get; set; } = string.Empty;
        public string SensorId { get; set; } = string.Empty;
        public int Orientation { get; set; }
        public int Detected { get; set; }
        public int Sent { get; set; }
    }

    public class NightRepository
    {
        private const string DbPath = "/database/concentrator.db";

        public static void InsertNight(string sensorId, int orientation, int detected)
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Night (time, sensor_id, orientation, detected, sent)
                VALUES (@time, @sensor_id, @orientation, @detected, 0);
            ";
            cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@sensor_id", sensorId);
            cmd.Parameters.AddWithValue("@orientation", orientation);
            cmd.Parameters.AddWithValue("@detected", detected);
            cmd.ExecuteNonQuery();
        }

        public static List<NightEntry> GetUnsentNights()
        {
            var results = new List<NightEntry>();
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Night WHERE sent = 0;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new NightEntry
                {
                    Id = reader.GetInt32(0),
                    Time = reader.GetString(1),
                    SensorId = reader.GetString(2),
                    Orientation = reader.GetInt32(3),
                    Detected = reader.GetInt32(4),
                    Sent = reader.GetInt32(5)
                });
            }
            return results;
        }

        public static void MarkAsSent(int id)
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Night SET sent = 1 WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}