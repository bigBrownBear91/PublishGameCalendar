using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PublishGameCalendar.DTOs;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        string? expectedUsername = _config["Admin:Username"];
        string? expectedPassword = _config["Admin:Password"];

        if (request.Username != expectedUsername || request.Password != expectedPassword)
            return Unauthorized();

        string token = GenerateJwt();
        return Ok(new LoginResponse { Token = token });
    }

    private string GenerateJwt()
    {
        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.Role, "Admin")
        };
        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new JwtSecurityToken(
            _config["Jwt:Issuer"],
            _config["Jwt:Audience"],
            claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
