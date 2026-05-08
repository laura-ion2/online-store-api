using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OnlineStoreAPI.Services.Interfaces;
using OnlineStoreAPI.DTOs;
using OnlineStoreAPI.Models;

namespace OnlineStoreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

public class AccountController: ControllerBase
{
       private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Password != model.ConfirmPassword)
                return BadRequest(new AuthResponseDTO 
                { 
                    Success = false, 
                    Message = "Passwords do not match." 
                });

            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Address = model.Address
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new AuthResponseDTO 
                { 
                    Success = false, 
                    Message = "User registration failed: " + string.Join(", ", errors) 
                });
            }

            // Generate JWT token for newly registered user
            var token = _jwtService.GenerateJwtToken(user);

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Token = token,
                Message = "User registered successfully",
                Expiration = DateTime.UtcNow.AddHours(8)
            });
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new AuthResponseDTO 
                { 
                    Success = false, 
                    Message = "Invalid login attempt." 
                });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new AuthResponseDTO 
                { 
                    Success = false, 
                    Message = "Invalid login attempt." 
                });

            var token = _jwtService.GenerateJwtToken(user);

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Token = token,
                Message = "Login successful",
                Expiration = DateTime.UtcNow.AddHours(8)
            });
        }
        [HttpGet("CustomerId")]
        public IActionResult WhoAmI()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });
            return Ok(claims);
        }
    }
}