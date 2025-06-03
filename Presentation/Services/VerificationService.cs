using Microsoft.Extensions.Caching.Distributed;
using Presentation.Models;
using StackExchange.Redis;

namespace Presentation.Services;

public class VerificationService(IDistributedCache cache)
{
    private readonly IDistributedCache _cache = cache;

    private static readonly Random _random = new();

    public async Task<string> GenerateCodeAsync(string email)
    {
        var code = _random.Next(100000, 999999).ToString();
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        var result = await SaveVerificationCodeAsync(new SaveVerificationCodeRequest
        {
            Email = email,
            Code = code,
            ValidFor = TimeSpan.FromMinutes(5) 
        });

        if (!result)
            return string.Empty; // Failed to save the code

        return code;    

    }

    private async Task<bool> SaveVerificationCodeAsync(SaveVerificationCodeRequest request)
    {
        try
        {
            await _cache.SetStringAsync(request.Email.ToLowerInvariant(), request.Code, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = request.ValidFor
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }


    public EmailRequest GenerateVerificationEmail(string firstName, string lastName, string email, string code)
    {
        return new EmailRequest
        {
            To = email,
            Subject = "Your verification code from Ventixe",
            HtmlContent = @$"
                    <!DOCTYPE html>
                    <html lang=""en"">
                    <head>
                        <meta charset=""UTF-8"">
                        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                        <title>Email Verification</title>
                    </head>
                    <body style=""margin:0; padding:0; background-color:#ffffff; font-family: Inter, Arial, sans-serif;"">
                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; padding:2rem;"">
                            <tr>
                                <td>
                                    <table width=""600"" align=""center"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ededed; padding:1rem; margin:1rem auto;"">
                                        <tr>
                                            <td>
                                                <h2 style=""color:#F26CF9; margin:0 0 1rem;"">Hello {firstName} {lastName} - Welcome to Ventixe</h2>
                                                <p style=""color:#434345; margin:0;"">Before you can start managing events, we need to verify your email address.</p>
                                            </td>
                                        </tr>
                                    </table>

                                    <table width=""600"" align=""center"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ededed; padding:1rem; margin:1rem auto;"">
                                        <tr>
                                            <td>
                                                <p style=""color:#434345; margin:0;"">
                                                    Follow this <a href=""https://jolly-ocean-090980503.6.azurestaticapps.net/verify?email={email}"" style=""color:#5562A2;"">link</a> and enter the code 
                                                    <span style=""color:#5562A2; font-size:24px; font-weight:bold;"">{code}</span> to verify your email address.
                                                </p>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </body>
                    </html>",
            PlainTextContent = $"Hello {firstName} {lastName}! Follow the the link and enter the code {code} to verify your email address. Link: https://jolly-ocean-090980503.6.azurestaticapps.net/verify?email={email}"
        };
    }


    public VerificationServiceResult VerifyVerificationCode(VerifyVerificationCodeRequest request)
    {
        var key = request.Email.ToLowerInvariant();

        try
        {
            var storedCode = _cache.GetString(key);

            if (storedCode != null)
            {
                if (storedCode.Equals(request.Code))
                {
                    try
                    {
                        _cache.Remove(key);
                    }
                    catch (RedisConnectionException)
                    {
                        return new VerificationServiceResult
                        {
                            Succeeded = false,
                            Error = "Failed to remove verification code."
                        };
                    }

                    return new VerificationServiceResult
                    {
                        Succeeded = true
                    };
                }
            }

            return new VerificationServiceResult
            {
                Succeeded = false,
                Error = "Invalid or expired verification code."
            };
        }
        catch (RedisConnectionException)
        {
            // Maybe retry once or return a specific error
            return new VerificationServiceResult
            {
                Succeeded = false,
                Error = "Verification service temporarily unavailable. Please try again."
            };
        }
    }
}
