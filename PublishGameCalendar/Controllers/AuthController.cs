using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Identity;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        ApplicationUser user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        bool isFirstUser = !_userManager.Users.Any();

        IdentityResult result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        string role = isFirstUser ? Roles.Admin : Roles.User;
        await _userManager.AddToRoleAsync(user, role);

        return Ok();
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized();

        IList<string> roles = await _userManager.GetRolesAsync(user);
        string role = roles.FirstOrDefault() ?? Roles.User;
        string token = GenerateJwt(user, role);

        return Ok(new LoginResponse { Token = token, Email = user.Email!, Role = role });
    }

    private string GenerateJwt(ApplicationUser user, string role)
    {
        Claim[] claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Role, role)
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