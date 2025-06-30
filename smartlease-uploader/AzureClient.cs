using Microsoft.Azure.Devices.Client;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Serilog;

namespace SmartleaseUploader;

public class AzureClient
{
    private readonly DeviceClient _client;

    public AzureClient(string connectionString)
    {
        _client = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
    }

    public async Task SendJsonAsync(object payload, int retryCount = 5)
    {
        try
        {
            string json = JsonSerializer.Serialize(payload);
            using var message = new Message(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
            };

            await _client.SendEventAsync(message);
            Log.Information($"[AZURE] Payload sent: {json}");
        }
        catch (Exception ex)
        {
            Log.Error($"[AZURE ERROR] {ex.Message}. Retrying...");
            await Task.Delay(2000);

            if (retryCount <= 0)
            {
                Log.Error("[AZURE RETRY FAILED] No more retries left.");
                await SendFailureEmailAsync(ex.Message);
                return;
            }

            await SendJsonAsync(payload, --retryCount);
        }
    }

    private async Task SendFailureEmailAsync(string errorMessage)
    {
        try
        {
            //azure smtp
            var smtpClient = new SmtpClient("outlook.office365.com")
            {
                Port = 587,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("concentrateur@smartlease.ch", "B(252374102030uz"),
                EnableSsl = true,
            };

            var mail = new MailMessage
            {
                From = new MailAddress("concentrateur@smartlease.ch"),
                Subject = "[SMARTLEASE] Azure Upload Failure",
                Body = $"Une erreur s'est produite lors de l'envoi à Azure IoT Hub :\n\n{errorMessage}",
                IsBodyHtml = false,
            };

            mail.To.Add("johann.werkle@elitebeds.ch");

            await smtpClient.SendMailAsync(mail);
            Log.Information("[MAIL] Notification envoyée à rapports@smartlease.ch");
        }
        catch (Exception mailEx)
        {
            Log.Error($"[MAIL ERROR] Impossible d'envoyer l'alerte par e-mail: {mailEx.Message}");
        }
    }
}
