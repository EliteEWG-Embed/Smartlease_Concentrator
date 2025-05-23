using System.Timers;
using Microsoft.Data.Sqlite;
using SmartleaseUploader;

using Timer = System.Timers.Timer;

namespace SmartleaseUploader;

public class Uploader
{
    private Timer _nightTimer;
    private Timer _bilanTimer;
    private Timer _purgeTimer;
    private AzureClient _azureClient = new AzureClient(
        Environment.GetEnvironmentVariable("AZURE_IOT_CONNECTION_STRING")
            ?? throw new Exception("AZURE_IOT_CONNECTION_STRING missing")
    );

    public async Task StartAsync()
    {
        _nightTimer = CreateDailyTimer(08, 30, async () => await CalculateAndSendNightReports());
        _bilanTimer = CreateIntervalTimer(12, async () => await SendSensorBilan());
        _purgeTimer = CreateDailyTimer(3, 0, async () => await PurgeOldData());

        Console.WriteLine("Uploader started. Press Ctrl+C to exit.");
        await Task.Delay(-1);
    }

    private Timer CreateIntervalTimer(int minutes, Func<Task> action)
    {
        var timer = new Timer(minutes * 60 * 1000);
        timer.Elapsed += async (_, _) => await action();
        timer.AutoReset = true;
        timer.Start();
        return timer;
    }

    private Timer CreateDailyTimer(int hour, int minute, Func<Task> action)
    {
        var now = DateTime.Now;
        var nextRun = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        if (now > nextRun)
            nextRun = nextRun.AddDays(1);
        var delay = (nextRun - now).TotalMilliseconds;

        var timer = new Timer(delay);
        timer.Elapsed += async (_, _) =>
        {
            await action();
            timer.Interval = 24 * 60 * 60 * 1000;
        };
        timer.AutoReset = true;
        timer.Start();
        return timer;
    }

    private async Task CalculateAndSendNightReports()
    {
        Console.WriteLine("[NIGHT] Calculating and recording night reports...");

        using var conn = new SqliteConnection("Data Source=/database/concentrator.db");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT sensor_id, COUNT(*) AS frames, SUM(motion) as total_motion, MAX(orientation) as orientation
        FROM Frames
        WHERE time >= datetime('now', '-1 day', 'start of day', '+14 hours')
          AND time <  datetime('now', 'start of day', '+10 hours')
          AND motion > 0
        GROUP BY sensor_id
        HAVING frames >= 12 AND total_motion > 1000;
    ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var sensorId = reader.GetString(0);
            var orientation = reader.GetInt32(3);
            var detected = 1; // Nuitée détectée

            // Insère dans la table Night
            NightRepository.InsertNight(sensorId, orientation, detected);
            Console.WriteLine($"[NIGHT] Detected night for {sensorId} (orientation={orientation})");
        }

        // Tente d’envoyer les nuitées en attente
        await SendUnsentNights();
    }


    private async Task SendSensorBilan()
    {
        Console.WriteLine("[BILAN] Sending sensor + repeater status...");

        using var conn = new SqliteConnection("Data Source=/database/concentrator.db");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
        SELECT sensor_id, COUNT(DISTINCT sensor_id || '-' || counter) as apparitions
        FROM Frames
        WHERE time >= datetime('now', '-45 minutes')
        GROUP BY sensor_id;
    ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var sensorId = reader.GetString(0);
            var apparitions = reader.GetInt32(1);

            var payload = new
            {
                type = "bilan",
                timestamp = DateTime.UtcNow,
                sensor = sensorId,
                apparitions = apparitions,
            };

            await _azureClient.SendJsonAsync(payload);
        }
    }

    private async Task PurgeOldData()
    {
        Console.WriteLine("[PURGE] Cleaning old data...");
        try
        {
            using var conn = new SqliteConnection("Data Source=/database/concentrator.db");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Frames WHERE time < datetime('now', '-15 days');";
            var rows = cmd.ExecuteNonQuery();
            Console.WriteLine($"[PURGE] {rows} rows deleted.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PURGE ERROR] {ex.Message}");
        }
    }

    private async Task SendUnsentNights()
    {
        var unsent = NightRepository.GetUnsentNights();
        foreach (var night in unsent)
        {
            var payload = new
            {
                type = "night",
                timestamp = night.Time,
                sensor = night.SensorId,
                orientation = night.Orientation,
                detected = night.Detected
            };

            try
            {
                await _azureClient.SendJsonAsync(payload);
                NightRepository.MarkAsSent(night.Id);
                Console.WriteLine($"[NIGHT SENT] ID={night.Id} Sensor={night.SensorId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NIGHT ERROR] {ex.Message}");
            }
        }
    }
}
