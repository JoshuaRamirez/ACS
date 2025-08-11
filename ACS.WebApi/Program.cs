using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var Configuration = builder.Configuration;

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddSingleton<ACS.Service.Services.IUserService,
    ACS.Service.Services.UserService>();
builder.Services.AddSingleton<ACS.Service.Services.IGroupService,
    ACS.Service.Services.GroupService>();
builder.Services.AddSingleton<ACS.Service.Services.IRoleService,
    ACS.Service.Services.RoleService>();
builder.Services.AddSingleton<ACS.Service.Services.IPermissionService,
    ACS.Service.Services.PermissionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program {}
