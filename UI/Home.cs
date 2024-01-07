using System.Net.WebSockets;
using System.Timers;
using Iot.Device.CharacterLcd;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

/// <summary>
/// Initial (home) page.  Displays the 
/// </summary>
public class Home : PageBase
{
  private DeviceController DeviceController { get; }
  private static readonly object _lockObj = new();

  private ILogger<Home> Logger { get; }

  public Home(Shell shell, DeviceController deviceController, IOptions<AppSettings> settings, ILogger<Home> logger) : base(shell, settings)
  {
    this.DeviceController = deviceController;
    this.Logger = logger;

    base.PageOpened += this.Page_Opened;
    base.PageClosed += this.Page_Closed;
    base.Refresh += this.Page_Refresh;
    base.ButtonPressed += this.Page_ButtonPressed;
  }

  private Task Page_Opened(object? sender, EventArgs e)
  {
    this.Logger?.LogTrace("Home Page opened.");
    this.Logger?.LogTrace("Configured nodes: " + String.Join(", ", this.AppSettings.Value.Nodes.Select(node => node.Name)));

    this.StopProgress();

    this.GetDisplay()?.Clear();
    this.CreateSpecialCharacters(CharacterSets.Application);

    // draw the node numbers
    for (int nodeIndex = 0; nodeIndex < 4; nodeIndex++)
    {
      if (!String.IsNullOrEmpty(this.AppSettings.Value.Nodes.Skip(nodeIndex).FirstOrDefault()?.Name))
      {
        int row = GetRow(nodeIndex);
        int col = GetCol(nodeIndex); // (nodeIndex % 2 == 0 ? 0 : (this.PanelWidth / 2) + 1);

        this.GetDisplay()?.SetCursorPosition(col, row);
        this.GetDisplay()?.Write($"{nodeIndex + 1}: ---".PadRight(this.PanelWidth / 2));
      }
    }

    return Task.CompletedTask;
  }

  private int GetRow(int nodeIndex)
  {
    return (int)Math.Floor((double)nodeIndex / 2);
  }

  private int GetCol(int nodeIndex)
  {
    return (nodeIndex % 2 == 0 ? 0 : (this.PanelWidth / 2) + 1);
  }

  private async Task Page_Refresh<ElapsedEventArgs>(object? sender, ElapsedEventArgs e)
  {
    this.Logger?.LogTrace("Home Page refresh.");

    await Parallel.ForEachAsync(this.AppSettings.Value.Nodes.Select((node, index) => new { node, index }), async (item, cancellationToken) =>
    {
      await DisplayNodeInformation(item.index, item.node);
    });
  }

  private async Task DisplayNodeInformation(int index, AppSettings.Node node)
  {
    // Calculate row/col for each node.  .SetCursorPosition args are zero-based.  The node numbers are drawn in Page_Load, we are
    // only updating the temperature values here.
    // 1:T.TTC  2:T.TTC
    // 3:T.TTC  4:T.TTC
    int row = GetRow(index);
    int col = GetCol(index) + 2; 

    if (!String.IsNullOrEmpty(node.Name) && !String.IsNullOrEmpty(node.HostName))
    {
      decimal temperature = await this.DeviceController.QueryCPUTemperature(node);

      string displayText;

      if (temperature == decimal.MinValue)
      {
        displayText = " ---";
      }
      else
      {
        displayText = $"{temperature:00.0}{GetSpecialCharacter(SpecialCharacters.CHAR_DEGREES)}";
      }

      // we need a lock here because the queries are run in parallel and we must update the display one at a time
      lock (_lockObj)
      {
        this.GetDisplay()?.SetCursorPosition(col, row);
        this.GetDisplay()?.Write(displayText.PadRight((this.PanelWidth / 2) - 1));
      }
    }
    else
    {
      this.Logger?.LogWarning("Node {index + 1} has a missing name or hostname in config.", index);
    }
  }

  private Task Page_Closed(object? sender, EventArgs e)
  {
    return Task.CompletedTask;
  }

  private Task Page_ButtonPressed(object? sender, ButtonPressedEventArgs e)
  {
    // on this page, all of the buttons do the same thing
    this.StopProgress();
    this.Shell?.OpenPage<UI.Actions>();

    return Task.CompletedTask;
  }
}
