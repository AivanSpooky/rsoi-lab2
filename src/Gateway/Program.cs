using Gateway.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<CarsClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Cars"]!));
builder.Services.AddHttpClient<RentalClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Rental"]!));
builder.Services.AddHttpClient<PaymentClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Payment"]!));

var app = builder.Build();

app.MapControllers();
app.MapGet("/manage/health", () => Results.Ok());

app.Run();
