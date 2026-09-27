using Gateway.Clients;
using Gateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Controllers;

[ApiController]
[Route("api/v1/cars")]
public class CarsController : ControllerBase
{
    private readonly CarsClient _cars;

    public CarsController(CarsClient cars) => _cars = cars;

    [HttpGet]
    public async Task<ActionResult<PaginationResponse<CarResponse>>> GetCars(
        [FromQuery] int? page,
        [FromQuery] int? size,
        [FromQuery] bool showAll = false)
    {
        var result = await _cars.GetCarsAsync(page, size, showAll);
        return Ok(result);
    }
}
