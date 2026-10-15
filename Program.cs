using control_asistencia.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides; // <-- IMPORTANTE: Necesario para Render

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

// Servicio de Autenticacion
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Index"; // A donde te manda si no estás logueado
        options.AccessDeniedPath = "/Auth/Index"; // A donde te manda si no tienes el rol correcto
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Duración de la sesión
    });

// =========================================================================
// CONFIGURACIÓN PARA RENDER (PROXY INVERSO)
// Esto soluciona el crash del error [7] Antiforgery al hacer Login o POST
// =========================================================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// =========================================================================
// MIDDLEWARE DE PROXY (¡Debe ir antes de cualquier otro Use...!)
// =========================================================================
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// COMENTADO INTENCIONALMENTE: En Render, la redirección HTTPS la hace su propio proxy.
// Dejar esto activo puede causar un bucle infinito de redirecciones y romper la app.
// app.UseHttpsRedirection(); 

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

app.Run();