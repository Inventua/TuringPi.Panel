using TuringPi.Panel;
using TuringPi.Panel.Drivers;
using TuringPi.Panel.Services;
using TuringPi.Panel.UI;

// https://github.com/dotnet/iot/blob/main/src/devices/README.md

// sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-8.0
// sudo apt install gpiod
// sudo apt install net-tools

// sudo dotnet tpi-panel.dll

Console.WriteLine($"tpi-panel {typeof(Shell).Assembly.GetName().Version}");

// setup app
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Using config files: " + String.Join (", ", builder.Configuration.Sources
  .Where(source => source is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource)
  .Cast<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>()
  .Where(source => System.IO.File.Exists(source.Path))
  .Select(source => source.Path))
);

Console.WriteLine(new String('-', Console.WindowWidth));

// provide notification and logging services for Linux
builder.Host.UseSystemd();

// configure the application
builder.Host.ConfigureServices((context, services) =>
{
  // Enable logging
  services.AddLogging(logging =>
  {
    logging.AddSimpleConsole(options => { options.TimestampFormat = "dd-MMM-yyyy HH:mm:ss: "; });;
  });

  services.Configure<AppSettings>(builder.Configuration.GetSection("Settings"));
  services.AddSingleton<Panel>();
  services.AddSingleton<DeviceController>();
  services.AddSingleton<TuringController>();

  // Automatically register UI components (implementations of UI.Base.Page)
  foreach (Type type in typeof(Program).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(PageBase))))
  {
    services.AddSingleton(type);
  }

  services.AddSingleton<Shell>();
  services.AddHostedService<Shell>(services => services.GetRequiredService<Shell>());

  services.AddControllers();
});
WebApplication app = builder.Build();

// web services
app.MapControllers();

// start
if (args.Contains("--debug-pause"))
{
  Console.WriteLine("Press any key");
  Console.ReadKey();
}

app.Run();


