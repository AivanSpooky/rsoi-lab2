using System.Net;
using System.Net.Http.Json;
using Gateway.Models;

namespace Gateway.Clients;

public class PaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http) => _http = http;

    public async Task<PaymentInfo?> GetPaymentAsync(Guid paymentUid)
    {
        var response = await _http.GetAsync($"api/v1/payment/{paymentUid}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentInfo>();
    }

    public async Task<PaymentInfo?> CreatePaymentAsync(int price)
    {
        var response = await _http.PostAsJsonAsync("api/v1/payment", new CreatePaymentRequest(price));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentInfo>();
    }

    public async Task<bool> CancelPaymentAsync(Guid paymentUid)
    {
        var response = await _http.PostAsync($"api/v1/payment/{paymentUid}/cancel", null);
        return response.IsSuccessStatusCode;
    }
}
