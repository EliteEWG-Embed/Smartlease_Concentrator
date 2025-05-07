using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;

namespace SmartleaseUploader;

public class AzureClient
{
    private readonly DeviceClient _client;

    public AzureClient(string connectionString)
    {
        _client = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
    }

    public async Task SendJsonAsync(object payload)
    {
        try
        {
            string json = JsonSerializer.Serialize(payload);
            using var message = new Message(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _client.SendEventAsync(message);
            //Console.WriteLine($"[AZURE] Payload sent: {json}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AZURE ERROR] {ex.Message}. Retrying...");
            await Task.Delay(2000);
            try
            {
                await _client.SendEventAsync(new Message(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
            }
            catch (Exception retryEx)
            {
                Console.WriteLine($"[AZURE RETRY FAILED] {retryEx.Message}");
            }
        }
    }
}
