using System.Drawing;
using System.Net.WebSockets;
using System.Timers;
using Iot.Device.CharacterLcd;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

public class TuringInformation : Menu
{
  private TuringController TuringController { get; set; }

  private ILogger<NodeInformation> Logger { get; }

  public TuringInformation(Shell shell, TuringController turingController, IOptions<AppSettings> settings, ILogger<NodeInformation> logger) : base(shell, settings)
  {
    this.TuringController = turingController;
    this.Logger = logger;

    base.PageOpened += this.Page_Opened;
    base.PageClosed += this.Page_Closed;
    base.Refresh += this.Page_Refresh;
  }

  private Task Page_Opened(object? sender, EventArgs e)
  {
    this.GetDisplay()?.Clear();
    this.GetDisplay()?.Write("Turing Info ...");
    this.ShowProgress(0, this.PanelWidth-1, TimeSpan.FromSeconds(3), TimeSpan.Zero);

    this.Logger?.LogDebug("Turing Information Page Opened");

    return Task.CompletedTask;
  }

  private async Task Page_Refresh(object? sender, EventArgs e)
  {
    this.ClearMenuItems();

    this.AddMenuItem($"BMC: v{await this.TuringController.Version()}");
    Boolean[] nodePower = await this.TuringController.QueryNodePower();    

    if (nodePower.All(value => value == true))
    {
      this.AddMenuItem($"All Nodes On");
    }
    else
    {
      string nodesOnList = String.Join(',', nodePower.Where(value => value == true).Select((value, index) => $"{index + 1}").ToList());
      this.AddMenuItem($"Node {nodesOnList} on");
    }    

    // we can't display an "IP" caption to give context because the biggest IP 255.255.255.255 uses all 16 characters
    this.AddMenuItem((await this.TuringController.QueryNetworkAddress()).ToString());

    // MAC with ':' separators won't fit on a 16 character display
    this.AddMenuItem((await this.TuringController.QueryMac()).Replace(":", ""));

    this.AddBackMenuItem();

    ShowMenu();
  }

  private Task Page_Closed(object? sender, EventArgs e)
  {
    this.StopProgress();
    return Task.CompletedTask;
  }

}
