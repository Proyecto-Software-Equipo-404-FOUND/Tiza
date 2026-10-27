using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.UnitTests;

// Sólo testing: reutiliza la fábrica y dobles existentes, no cambia autenticación productiva.
public sealed class Qa02Fixture : IDisposable
{
    private readonly PlatformHttpFactory original = new();
    private readonly WebApplicationFactory<Program> host;
    public HttpClient Client { get; }
    public Dictionary<string, string> Variables { get; } = new();
    public int UserCount => original.InstitucionalStore.Users.Count;
    public int PlatformWrites => original.Store.Altas;
    public Qa02Fixture()
    {
        var store = original.InstitucionalStore;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        store.Admin.Email = "qa02-admin@test.invalid";
        store.Admin.PasswordHash = new BCryptPasswordHasher().Hash(password);
        var teacher = new Usuario { InstitucionId=store.Admin.InstitucionId, Rol="Docente", Email="qa02-docente@test.invalid" };
        var student = new Usuario { InstitucionId=store.Admin.InstitucionId, Rol="Alumno", Email="qa02-alumno@test.invalid" };
        store.Users[teacher.Id]=teacher; store.Users[student.Id]=student;
        var issuer = new JwtTokenService(Options.Create(original.Institucional));
        Variables["institutionalAdminToken"]=issuer.GenerateToken(store.Admin);
        Variables["institutionalTeacherToken"]=issuer.GenerateToken(teacher);
        Variables["institutionalStudentToken"]=issuer.GenerateToken(student);
        Variables["qa02ExpiredToken"]=InvalidToken(expired:true);
        Variables["qa02WrongSignatureToken"]=InvalidToken(wrongKey:true);
        Variables["qa02MissingRoleToken"]=InvalidToken(missingRole:true);
        Variables["qa02MalformedToken"]="deliberately.invalid.token";
        Variables["qa02Password"]=password;
        Variables["qa02WrongPassword"]=Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        Variables["qa02ShortPassword"]=Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
        Variables["qa02LongPassword"]=new string('é',37); //74 bytes UTF8,37 caracteres.
        Variables["qa02LongRefresh"]=Convert.ToHexString(RandomNumberGenerator.GetBytes(129));
        Variables["qa02UnknownRefresh"]=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Variables["qa02AdminEmail"]=store.Admin.Email;
        host=original.WithWebHostBuilder(builder=>
        {
            var directory=new DirectoryInfo(AppContext.BaseDirectory);
            while(directory!=null && !File.Exists(Path.Combine(directory.FullName,"PEI.sln")))directory=directory.Parent;
            if(directory==null)throw new InvalidOperationException("Ejecutar QA02 desde el checkout del repositorio.");
            builder.UseContentRoot(Path.Combine(directory.FullName,"services","identidad","src","PEI.Identidad.Api"));
            builder.ConfigureLogging(logging=>logging.ClearProviders());
            builder.ConfigureServices(services=>
            {
                services.RemoveAll<IRegistrationDataAccess>();
                services.AddSingleton<IRegistrationDataAccess>(store);
            });
        });
        Client=host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false });
    }
    private string InvalidToken(bool expired=false,bool wrongKey=false,bool missingRole=false)
    {
        var user=original.InstitucionalStore.Admin; var settings=original.Institucional;
        var claims=new List<Claim>{new("sub",user.Id.ToString()),new("institucion_id",user.InstitucionId.ToString()),new("jti",Guid.NewGuid().ToString())};
        if(!missingRole)claims.Add(new("role",user.Rol));
        var key=wrongKey?Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)):settings.SecretKey;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(settings.Issuer,settings.Audience,claims,
            DateTime.UtcNow.AddHours(-2),expired?DateTime.UtcNow.AddHours(-1):DateTime.UtcNow.AddMinutes(15),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256)));
    }
    public string Expand(string raw) => Variables.Aggregate(raw,(value,pair)=>value.Replace("{{"+pair.Key+"}}",pair.Value));
    public void Dispose(){Client.Dispose();host.Dispose();original.Dispose();}
}
