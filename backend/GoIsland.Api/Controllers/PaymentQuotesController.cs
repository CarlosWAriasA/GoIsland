using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/experiences/{experienceId:int}/payment-quote")]
public sealed class PaymentQuotesController : ControllerBase
{
    private readonly GoIslandDbContext _context;
    private readonly IPaymentPricingService _pricing;

    public PaymentQuotesController(GoIslandDbContext context, IPaymentPricingService pricing)
    {
        _context = context;
        _pricing = pricing;
    }

    [HttpGet]
    public async Task<ActionResult<PaymentQuoteResponse>> Get(int experienceId, [FromQuery] int quantity = 1)
    {
        if (quantity is < 1 or > 100)
        {
            ModelState.AddModelError(nameof(quantity), "La cantidad debe estar entre 1 y 100.");
            return ValidationProblem(ModelState);
        }

        var experience = await _context.Experiences.AsNoTracking()
            .Where(item => item.Id == experienceId
                && item.IsApproved
                && item.ApprovalStatus == ExperienceApprovalStatuses.Approved
                && !item.IsHidden
                && _context.HostProfiles.Any(profile => profile.UserId == item.HostId
                    && profile.VerificationStatus == HostVerificationStatuses.Approved))
            .Select(item => new { item.Price })
            .SingleOrDefaultAsync();
        if (experience is null)
        {
            return NotFound(new { message = "No encontramos esta experiencia." });
        }

        var breakdown = _pricing.Calculate(experience.Price * quantity);
        return Ok(new PaymentQuoteResponse
        {
            Currency = breakdown.Currency,
            UnitPrice = experience.Price,
            Quantity = quantity,
            SubtotalAmount = breakdown.Subtotal,
            ServiceFeeAmount = breakdown.ServiceFee,
            TotalAmount = breakdown.Total
        });
    }
}
