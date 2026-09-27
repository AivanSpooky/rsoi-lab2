using System.Net;
using System.Net.Http.Json;
using Gateway.Models;

namespace Gateway.Clients;

public class RentalClient
{
    private readonly HttpClient _http;

    public RentalClient(HttpClient http) => _http = http;

    public async Task<List<RentalRecord>> GetRentalsAsync(string username)
    {
        var rentals = await _http.GetFromJsonAsync<List<RentalRecord>>(
            $"api/v1/rental?username={Uri.EscapeDataString(username)}");
        return rentals ?? [];
    }

    public async Task<RentalRecord?> GetRentalAsync(Guid rentalUid, string username)
    {
        var response = await _http.GetAsync($"api/v1/rental/{rentalUid}?username={Uri.EscapeDataString(username)}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RentalRecord>();
    }

    public async Task<RentalRecord?> CreateRentalAsync(CreateRentalRecordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/v1/rental", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RentalRecord>();
    }

    public async Task<bool> FinishRentalAsync(Guid rentalUid, string username)
    {
        var response = await _http.PostAsync(
            $"api/v1/rental/{rentalUid}/finish?username={Uri.EscapeDataString(username)}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CancelRentalAsync(Guid rentalUid, string username)
    {
        var response = await _http.PostAsync(
            $"api/v1/rental/{rentalUid}/cancel?username={Uri.EscapeDataString(username)}", null);
        return response.IsSuccessStatusCode;
    }
}
