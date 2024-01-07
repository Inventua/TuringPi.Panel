using System.Net.WebSockets;
using System.Timers;
using Iot.Device.CharacterLcd;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

/// <summary>
/// Splash (startup) page.
/// </summary>
public class Splash : PageBase
{
  private DeviceController DeviceController { get; }
  private static readonly object _lockObj = new();
 
  private ILogger<Home> Logger { get; }

  public Splash(Shell shell, DeviceController deviceController, IOptions<AppSettings> settings, ILogger<Home> logger) : base(shell, settings)
  {
    this.DeviceController = deviceController;
    this.Logger = logger;

    base.PageOpened += this.Page_Opened;
    base.Refresh += this.Page_Refresh;
  }

  private async Task Page_Opened(object? sender, EventArgs e)
  {
    this.Logger?.LogTrace("Splash Page opened.");
    
    this.CreateSpecialCharacters(CharacterSets.Splash);
    this.GetDisplay()?.Clear();

    this.GetDisplay()?.Write($" {this.GetSpecialCharacter(SpecialCharacters.CHAR_LOGO_TOP_LEFT)}{this.GetSpecialCharacter(SpecialCharacters.CHAR_LOGO_TOP_RIGHT)}  Turing Pi");
    this.GetDisplay()?.SetCursorPosition(0, 1);
    this.GetDisplay()?.Write($" {this.GetSpecialCharacter(SpecialCharacters.CHAR_LOGO_BOTTOM_LEFT)}{this.GetSpecialCharacter(SpecialCharacters.CHAR_LOGO_BOTTOM_RIGHT)}");

    this.ShowProgress(4, this.PanelWidth - 2, TimeSpan.FromSeconds(2), TimeSpan.Zero);
    await this.DeviceController.Connect(this.AppSettings.Value.Nodes);   
  }

  private async Task Page_Refresh<ElapsedEventArgs>(object? sender, ElapsedEventArgs e)
  {
    await Shell.ClearPageStack();
    await Shell.OpenPage<Home>();
  }

}
