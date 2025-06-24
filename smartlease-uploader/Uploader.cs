using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Timers;
using Microsoft.Azure.Devices.Client;
using Microsoft.Data.Sqlite;
using Serilog;
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

    private int HourStart =
        Environment.GetEnvironmentVariable("HOUR_START") is string hourStartStr
        && int.TryParse(hourStartStr, out var hourStart)
            ? hourStart
            : 14;
    private int HourEnd =
        Environment.GetEnvironmentVariable("HOUR_END") is string hourEndStr
        && int.TryParse(hourEndStr, out var hourEnd)
            ? hourEnd
            : 10;

    private int IntervalNightReports =
        Environment.GetEnvironmentVariable("INTERVAL_NIGHT_REPORTS") is string intervalStr
        && int.TryParse(intervalStr, out var interval)
            ? interval
            : 60; // minutes
    private int IntervalBilan =
        Environment.GetEnvironmentVariable("INTERVAL_BILAN") is string bilanStr
        && int.TryParse(bilanStr, out var bilan)
            ? bilan
            : 12; // minutes

    private int PurgeHour =
        Environment.GetEnvironmentVariable("PURGE_HOUR") is string purgeHourStr
        && int.TryParse(purgeHourStr, out var purgeHour)
            ? purgeHour
            : 3; // hour for daily purge

    private int maxPayloadSize = Environment.GetEnvironmentVariable("MAX_PAYLOAD_SIZE") is string maxSizeStr
        && int.TryParse(maxSizeStr, out var maxSize)
            ? maxSize
            : 4000; // bytes
    private int SumMovementThreshold =
        Environment.GetEnvironmentVariable("SUM_MOVEMENT_THRESHOLD") is string thresholdStr
        && int.TryParse(thresholdStr, out var threshold)
            ? threshold
            : 1000; // minimum movement sum to consider night detected

    private int MinFramesCount =
        Environment.GetEnvironmentVariable("MIN_FRAMES_COUNT") is string minFramesStr
        && int.TryParse(minFramesStr, out var minFrames)
            ? minFrames
            : 12; // minimum frames count to consider night detected

    public async Task StartAsync()
    {
        NightRepository.Initialize();

        _nightTimer = CreateIntervalTimer(
            IntervalNightReports,
            async () => await CalculateAndSendNightReports()
        );
        _bilanTimer = CreateIntervalTimer(IntervalBilan, async () => await SendSensorBilan());
        _purgeTimer = CreateDailyTimer(PurgeHour, 0, async () => await PurgeOldData());

        Log.Information("Uploader started. Press Ctrl+C to exit.");
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
        Log.Information("[NIGHT] Calculating and recording night reports…");

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
        WHERE time >= datetime('now', '-1 day', 'start of day', '+@HourStart hours')
          AND time <  datetime('now', 'start of day', '+@HourEnd hours')
          AND motion > 0
        ORDER BY sensor_id, time;";
        cmd.Parameters.AddWithValue("@HourStart", HourStart);
        cmd.Parameters.AddWithValue("@HourEnd", HourEnd);

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

            if (frames >= MinFramesCount && motionSum > SumMovementThreshold)
            {
                NightRepository.InsertNight(sensorId, lastOrientation, detected: 1);
            }
        }

        await SendUnsentNights();
    }

    private async Task SendSensorBilan()
    {
        Log.Information("[BILAN] Sending sensor + repeater status...");

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

        Log.Information($"[BILAN] Sent grouped bilans to Azure");
    }

    private async Task PurgeOldData()
    {
        Log.Information("[PURGE] Cleaning old data...");
        try
        {
            using var conn = new SqliteConnection("Data Source=/database/concentrator.db");
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Frames WHERE time < datetime('now', '-15 days');";
            var rowsFrame = cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM Night WHERE time < datetime('now', '-15 days');";
            var rowsNight = cmd.ExecuteNonQuery();
            Log.Information($"[PURGE] {rowsFrame + rowsNight} rows deleted.");
        }
        catch (Exception ex)
        {
            Log.Error($"[PURGE ERROR] {ex.Message}");
        }
    }

    private async Task SendUnsentNights()
    {
        var unsent = NightRepository.GetUnsentNights();

        var batch = new List<object>();
        int currentSize = 0;

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
            Log.Information($"[NIGHT SENT] ID={night.Id} Sensor={night.SensorId}");
        }
    }
}
