using Microsoft.AspNetCore.Http.Features;
using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 110L * 1024 * 1024);
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = 110L * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 110L * 1024 * 1024);

var app = builder.Build();
HomeServices.Core.Local.Accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
app.UseMiddleware<IHandler>();

app.UseStaticHttpCurrent();

app.Run();
