using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Timers;
using Microsoft.Azure.Devices.Client;
using Microsoft.Data.Sqlite;
using SmartleaseUploader;
using Timer = System.Timers.Timer;

namespace SmartleaseUploader;

public class Uploader
{
    private Timer _nightTimer;
    private Timer _bilanTimer;
    private Timer _purgeTimer;
    private static readonly TimeSpan FrameGap = TimeSpan.FromSeconds(150); // 2,5 min
    private AzureClient _azureClient = new AzureClient(
        Environment.GetEnvironmentVariable("AZURE_IOT_CONNECTION_STRING")
            ?? throw new Exception("AZURE_IOT_CONNECTION_STRING missing")
    );

    public async Task StartAsync()
    {
        NightRepository.Initialize();

        _nightTimer = CreateIntervalTimer(60, async () => await CalculateAndSendNightReports());
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
        Console.WriteLine("[NIGHT] Calculating and recording night reports…");

        await using var conn = new SqliteConnection("Data Source=/database/concentrator.db");
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
        SELECT sensor_id,
               time,
               motion,
               orientation
        FROM Frames
        WHERE time >= datetime('now', '-1 day', 'start of day', '+14 hours')
          AND time <  datetime('now', 'start of day', '+10 hours')
          AND motion > 0
        ORDER BY sensor_id, time;";

        var stats =
            new Dictionary<
                string,
                (DateTime lastKept, int frameCount, int motionSum, int lastOrientation)
            >();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var sensorId = reader.GetString(0);
            var frameTime = reader.GetDateTime(1);
            var motion = reader.GetInt32(2);
            var orientation = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

            if (!stats.TryGetValue(sensorId, out var s))
            {
                stats[sensorId] = (frameTime, 1, motion, orientation);
                continue;
            }

            if (frameTime - s.lastKept >= FrameGap)
            {
                stats[sensorId] = (frameTime, s.frameCount + 1, s.motionSum + motion, orientation);
            }
        }

        foreach (var (sensorId, data) in stats)
        {
            var (lastTime, frames, motionSum, lastOrientation) = data;

            if (frames >= 12 && motionSum > 1000)
            {
                NightRepository.InsertNight(sensorId, lastOrientation, detected: 1);
            }
        }

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
        WHERE time >= datetime('now', '-60 minutes')
        GROUP BY sensor_id;
    ";

        using var reader = cmd.ExecuteReader();

        var batch = new List<object>();
        int maxPayloadSize = 4000;
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

            string nextPayloadSerialized = JsonSerializer.Serialize(payload);
            int estimatedSize = Encoding.UTF8.GetByteCount(
                JsonSerializer.Serialize(batch.Concat(new[] { payload }))
            );

            if (estimatedSize > maxPayloadSize && batch.Count > 0)
            {
                await _azureClient.SendJsonAsync(batch);
                batch.Clear();
            }

            batch.Add(payload);
        }

        if (batch.Count > 0)
        {
            await _azureClient.SendJsonAsync(batch);
        }

        Console.WriteLine($"[BILAN] Sent grouped bilans to Azure");
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

        var batch = new List<object>();
        int currentSize = 0;
        const int maxPayloadSize = 4000;

        foreach (var night in unsent)
        {
            var payload = new
            {
                type = "night",
                timestamp = night.Time,
                sensor = night.SensorId,
                orientation = night.Orientation,
                detected = night.Detected,
            };

            // Estimated size of the new payload
            string jsonCandidate = JsonSerializer.Serialize(payload);
            int sizeCandidate = Encoding.UTF8.GetByteCount(
                JsonSerializer.Serialize(batch.Concat(new[] { payload }))
            );

            if (sizeCandidate > maxPayloadSize && batch.Count > 0)
            {
                await _azureClient.SendJsonAsync(batch);
                batch.Clear();
            }

            batch.Add(payload);
        }

        if (batch.Count > 0)
        {
            await _azureClient.SendJsonAsync(batch);
        }

        foreach (var night in unsent)
        {
            NightRepository.MarkAsSent(night.Id);
            //Console.WriteLine($"[NIGHT SENT] ID={night.Id} Sensor={night.SensorId}");
        }
    }
}
