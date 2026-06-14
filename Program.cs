using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Kart okut - otomatik giriş/çıkış
app.MapPost("/kart-okut", async (KartIstegi istek, AppDbContext db) =>
{
    var personel = await db.Personeller.FirstOrDefaultAsync(p => p.SicilNo == istek.SicilNo);
    if (personel is null) return Results.NotFound("Personel bulunamadı!");

    var sonGecis = await db.Gecisler
        .Where(g => g.PersonelId == personel.Id)
        .OrderByDescending(g => g.Zaman)
        .FirstOrDefaultAsync();

    string tip;

    if (istek.Kapi == "A")
    {
        if (sonGecis != null && sonGecis.Tip == "Giriş")
            return Results.BadRequest("Hata: Zaten içeridesiniз!");
        tip = "Giriş";
    }
    else if (istek.Kapi == "B")
    {
        if (sonGecis == null || sonGecis.Tip == "Çıkış")
            return Results.BadRequest("Hata: Zaten dışarıdasınız!");
        tip = "Çıkış";
    }
    else
    {
        return Results.BadRequest("Geçersiz kapı! A veya B olmalı.");
    }

    db.Gecisler.Add(new Gecis
    {
        PersonelId = personel.Id,
        Zaman = DateTime.UtcNow,
        Tip = tip
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { Mesaj = $"{tip} yapıldı", Ad = personel.Ad, Zaman = DateTime.UtcNow });
});

// Yönetici - içeridekiler
app.MapGet("/yonetici/icerdekiler", async (string sifre, AppDbContext db) =>
{
    if (sifre != "admin123") return Results.Unauthorized();

    var personeller = await db.Personeller.ToListAsync();
    var sonuc = new List<object>();

    foreach (var p in personeller)
    {
        var sonGecis = await db.Gecisler
            .Where(g => g.PersonelId == p.Id)
            .OrderByDescending(g => g.Zaman)
            .FirstOrDefaultAsync();

        if (sonGecis != null && sonGecis.Tip == "Giriş")
        {
            sonuc.Add(new { p.Ad, p.SicilNo, GirisSaati = sonGecis.Zaman });
        }
    }
    return Results.Ok(sonuc);
});

// Yönetici - günlük rapor
app.MapGet("/yonetici/rapor", async (string sifre, AppDbContext db) =>
{
    if (sifre != "admin123") return Results.Unauthorized();

    var bugun = DateTime.UtcNow.Date;
    var personeller = await db.Personeller.ToListAsync();
    var rapor = new List<object>();

    foreach (var p in personeller)
    {
        var gecisler = await db.Gecisler
            .Where(g => g.PersonelId == p.Id && g.Zaman.Date == bugun)
            .OrderBy(g => g.Zaman)
            .ToListAsync();

        var ilkGiris = gecisler.FirstOrDefault(g => g.Tip == "Giriş");
        var sonCikis = gecisler.LastOrDefault(g => g.Tip == "Çıkış");

        double toplamSaat = 0;
        if (ilkGiris != null && sonCikis != null)
            toplamSaat = (sonCikis.Zaman - ilkGiris.Zaman).TotalHours;

        var mesaiSaat = TimeSpan.Parse(p.MesaiBaslangic);
        bool gecKaldi = ilkGiris != null && ilkGiris.Zaman.ToLocalTime().TimeOfDay > mesaiSaat.Add(TimeSpan.FromMinutes(5));

        rapor.Add(new
        {
            p.Ad,
            p.SicilNo,
            IlkGiris = ilkGiris?.Zaman,
            SonCikis = sonCikis?.Zaman,
            ToplamSaat = Math.Round(toplamSaat, 1),
            GecKaldi = gecKaldi
        });
    }
    return Results.Ok(rapor);
});

// Personel ekle
app.MapPost("/personel", async (Personel personel, AppDbContext db) =>
{
    db.Personeller.Add(personel);
    await db.SaveChangesAsync();
    return Results.Created($"/personel/{personel.Id}", personel);
});

// Son geçişler - personel ekranı için
app.MapGet("/gecisler", async (AppDbContext db) =>
    await db.Gecisler
        .Include(g => g.Personel)
        .OrderByDescending(g => g.Zaman)
        .Take(10)
        .ToListAsync());
app.Run();

class Personel
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string SicilNo { get; set; } = "";
    public string MesaiBaslangic { get; set; } = "09:00:00";
}

class Gecis
{
    public int Id { get; set; }
    public int PersonelId { get; set; }
    public DateTime Zaman { get; set; }
    public string Tip { get; set; } = "";
    public Personel? Personel { get; set; }
}

class KartIstegi
{
  public string SicilNo { get; set; } = "";
  public string Kapi { get; set; } = ""; // "A" veya "B"
}

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Personel> Personeller { get; set; }
    public DbSet<Gecis> Gecisler { get; set; }
}