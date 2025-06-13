using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

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

        public static void Initialize()
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
                @"
        CREATE TABLE IF NOT EXISTS Night (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            time TEXT DEFAULT CURRENT_TIMESTAMP,
            sensor_id TEXT,
            orientation INTEGER,
            detected INTEGER,
            sent INTEGER DEFAULT 0
        );
    ";
            cmd.ExecuteNonQuery();
        }

        public static void InsertNight(string sensorId, int orientation, int detected)
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            string todayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText =
                @"
        SELECT COUNT(*) 
        FROM Night 
        WHERE sensor_id = @sensor_id 
          AND DATE(time) = @date;
    ";
            checkCmd.Parameters.AddWithValue("@sensor_id", sensorId);
            checkCmd.Parameters.AddWithValue("@date", todayDate);

            long count = (long)checkCmd.ExecuteScalar();

            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText =
                @"
        INSERT INTO Night (time, sensor_id, orientation, detected, sent)
        VALUES (@time, @sensor_id, @orientation, @detected, 0);
    ";
            insertCmd.Parameters.AddWithValue(
                "@time",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            );
            insertCmd.Parameters.AddWithValue("@sensor_id", sensorId);
            insertCmd.Parameters.AddWithValue("@orientation", orientation);
            insertCmd.Parameters.AddWithValue("@detected", detected);
            insertCmd.ExecuteNonQuery();

            Console.WriteLine(
                $"[NIGHT INSERT] Inserted night for sensor {sensorId} on {todayDate}"
            );
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
                results.Add(
                    new NightEntry
                    {
                        Id = reader.GetInt32(0),
                        Time = reader.GetString(1),
                        SensorId = reader.GetString(2),
                        Orientation = reader.GetInt32(3),
                        Detected = reader.GetInt32(4),
                        Sent = reader.GetInt32(5),
                    }
                );
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
