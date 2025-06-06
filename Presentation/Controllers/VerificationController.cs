using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Presentation.Services;
using System.Text.Json;

namespace Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VerificationController(VerificationService verificationService, ServiceBusClient serviceBusClient) : ControllerBase
{
    private readonly VerificationService _verificationService = verificationService;
    private readonly ServiceBusClient _serviceBusClient = serviceBusClient;

    [HttpPost("sendverification")]
    public async Task<IActionResult> SendVerificationCode([FromBody] VerificationRequest model)
    {
        try
        {
            // Generate verification code
            var code = await _verificationService.GenerateCodeAsync(model.Email);
            if (string.IsNullOrEmpty(code))
            {
                return Conflict("Failed to generate verification code.");
            }

            // Generate and send the verification email
            var emailRequest = _verificationService.GenerateVerificationEmail(model.FirstName, model.LastName, model.Email, code);
            if (emailRequest == null)
            {
                return Conflict("Failed to generate email request.");
            }

            var sender = _serviceBusClient.CreateSender("email_request");
            await sender.SendMessageAsync(new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(emailRequest)));

            return Ok("Verification code sent successfully via Service Bus.");

        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error sending verification code: {ex.Message}");
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyVerificationCodeRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Error = "Invalid or expired verification code." });

        var result = _verificationService.VerifyVerificationCode(req);
        if (result.Succeeded)
        {
            var httpClient = new HttpClient();
            var apiResponse = await httpClient.PostAsJsonAsync(
                "https://authservice-jasmin-h9euf4dpghc5d7a8.swedencentral-01.azurewebsites.net/api/auth/confirm-email",
                new { req.Email });

            if (!apiResponse.IsSuccessStatusCode)
            {
                return StatusCode(500, "Failed to confirm email");
            }

            // Forward the entire response from auth service
            var content = await apiResponse.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        else
        {
            return StatusCode(500, result);
        }
    }
}
