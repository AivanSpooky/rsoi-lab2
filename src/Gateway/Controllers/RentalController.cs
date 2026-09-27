using Gateway.Clients;
using Gateway.Models;
using Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Controllers;

[ApiController]
[Route("api/v1/rental")]
public class RentalController : ControllerBase
{
    private readonly CarsClient _cars;
    private readonly RentalClient _rental;
    private readonly PaymentClient _payment;

    public RentalController(CarsClient cars, RentalClient rental, PaymentClient payment)
    {
        _cars = cars;
        _rental = rental;
        _payment = payment;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalResponse>>> GetRentals(
        [FromHeader(Name = "X-User-Name")] string? username)
    {
        if (string.IsNullOrEmpty(username))
            return BadRequest(new ValidationErrorResponse("Missing X-User-Name header",
                [new ErrorDescription("X-User-Name", "Header is required")]));

        var rentals = await _rental.GetRentalsAsync(username);
        var result = await Task.WhenAll(rentals.Select(ToResponse));
        return Ok(result);
    }

    [HttpGet("{rentalUid:guid}")]
    public async Task<ActionResult<RentalResponse>> GetRental(
        Guid rentalUid,
        [FromHeader(Name = "X-User-Name")] string? username)
    {
        if (string.IsNullOrEmpty(username))
            return BadRequest(new ValidationErrorResponse("Missing X-User-Name header",
                [new ErrorDescription("X-User-Name", "Header is required")]));

        var rental = await _rental.GetRentalAsync(rentalUid, username);
        if (rental is null)
            return NotFound(new ErrorResponse($"Rental with uid {rentalUid} not found"));

        return Ok(await ToResponse(rental));
    }

    [HttpPost]
    public async Task<ActionResult<CreateRentalResponse>> CreateRental(
        [FromBody] CreateRentalRequest request,
        [FromHeader(Name = "X-User-Name")] string? username)
    {
        var errors = ValidateRequest(request, username);
        if (errors.Count > 0)
            return BadRequest(new ValidationErrorResponse("Invalid request", errors));

        var car = await _cars.GetCarAsync(request.CarUid);
        if (car is null)
            return NotFound(new ErrorResponse($"Car with uid {request.CarUid} not found"));
        if (!car.Available)
            return BadRequest(new ValidationErrorResponse("Car is not available",
                [new ErrorDescription("carUid", "Car is already reserved")]));

        await _cars.SetAvailabilityAsync(car.CarUid, false);

        var price = RentalPriceCalculator.Calculate(request.DateFrom, request.DateTo, car.Price);
        var payment = await _payment.CreatePaymentAsync(price);
        if (payment is null)
        {
            await _cars.SetAvailabilityAsync(car.CarUid, true);
            return StatusCode(500, new ErrorResponse("Failed to create payment"));
        }

        var rental = await _rental.CreateRentalAsync(new CreateRentalRecordRequest(
            username!, payment.PaymentUid, car.CarUid, request.DateFrom, request.DateTo));
        if (rental is null)
        {
            await _cars.SetAvailabilityAsync(car.CarUid, true);
            await _payment.CancelPaymentAsync(payment.PaymentUid);
            return StatusCode(500, new ErrorResponse("Failed to create rental"));
        }

        return Ok(new CreateRentalResponse(
            rental.RentalUid, rental.Status, car.CarUid,
            rental.DateFrom, rental.DateTo, payment));
    }

    [HttpPost("{rentalUid:guid}/finish")]
    public async Task<IActionResult> FinishRental(
        Guid rentalUid,
        [FromHeader(Name = "X-User-Name")] string? username)
    {
        if (string.IsNullOrEmpty(username))
            return BadRequest(new ValidationErrorResponse("Missing X-User-Name header",
                [new ErrorDescription("X-User-Name", "Header is required")]));

        var rental = await _rental.GetRentalAsync(rentalUid, username);
        if (rental is null)
            return NotFound(new ErrorResponse($"Rental with uid {rentalUid} not found"));

        await _cars.SetAvailabilityAsync(rental.CarUid, true);
        await _rental.FinishRentalAsync(rentalUid, username);

        return NoContent();
    }

    [HttpDelete("{rentalUid:guid}")]
    public async Task<IActionResult> CancelRental(
        Guid rentalUid,
        [FromHeader(Name = "X-User-Name")] string? username)
    {
        if (string.IsNullOrEmpty(username))
            return BadRequest(new ValidationErrorResponse("Missing X-User-Name header",
                [new ErrorDescription("X-User-Name", "Header is required")]));

        var rental = await _rental.GetRentalAsync(rentalUid, username);
        if (rental is null)
            return NotFound(new ErrorResponse($"Rental with uid {rentalUid} not found"));

        await _cars.SetAvailabilityAsync(rental.CarUid, true);
        await _rental.CancelRentalAsync(rentalUid, username);
        await _payment.CancelPaymentAsync(rental.PaymentUid);

        return NoContent();
    }

    private async Task<RentalResponse> ToResponse(RentalRecord rental)
    {
        var carTask = _cars.GetCarAsync(rental.CarUid);
        var paymentTask = _payment.GetPaymentAsync(rental.PaymentUid);
        await Task.WhenAll(carTask, paymentTask);

        var car = carTask.Result;
        var payment = paymentTask.Result;

        return new RentalResponse(
            rental.RentalUid,
            rental.Status,
            rental.DateFrom,
            rental.DateTo,
            new CarInfo(rental.CarUid, car?.Brand ?? "", car?.Model ?? "", car?.RegistrationNumber ?? ""),
            payment ?? new PaymentInfo(rental.PaymentUid, "", 0));
    }

    private static List<ErrorDescription> ValidateRequest(CreateRentalRequest request, string? username)
    {
        var errors = new List<ErrorDescription>();
        if (string.IsNullOrEmpty(username))
            errors.Add(new ErrorDescription("X-User-Name", "Header is required"));
        if (request.CarUid == Guid.Empty)
            errors.Add(new ErrorDescription("carUid", "Must be a valid car UUID"));
        if (!DateOnly.TryParse(request.DateFrom, out var dateFrom))
            errors.Add(new ErrorDescription("dateFrom", "Must be a valid ISO 8601 date"));
        if (!DateOnly.TryParse(request.DateTo, out var dateTo))
            errors.Add(new ErrorDescription("dateTo", "Must be a valid ISO 8601 date"));
        if (errors.Count == 0 && dateTo <= dateFrom)
            errors.Add(new ErrorDescription("dateTo", "Must be after dateFrom"));
        return errors;
    }
}
