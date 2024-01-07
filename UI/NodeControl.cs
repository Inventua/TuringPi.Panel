using System.Reflection.Metadata.Ecma335;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Services;

namespace TuringPi.Panel.UI;

public class NodeControl : Menu
{
  private TuringController TuringController { get; }
  private DeviceController DeviceController { get; }

  private static readonly object _lockObj = new();

  public NodeControl(TuringController turingController, DeviceController deviceController, Shell shell, IOptions<AppSettings> settings) : base(shell, settings, true)
  {
    this.TuringController = turingController;
    this.DeviceController = deviceController;

    base.PageOpened += Page_Opened;
    base.Refresh += Page_Refresh;
  }

  private Task Page_Opened(object? sender, EventArgs e)
  {
    this.GetDisplay()?.Clear();
    this.GetDisplay()?.Write("Node Control ...");
    this.ShowProgress(TimeSpan.FromSeconds(3));
    
    return Task.CompletedTask;
  }

  private async Task Page_Refresh(object? sender, EventArgs e)
  {
    Boolean[] powerStates = await this.TuringController.QueryNodePower();

    this.ClearMenuItems();

    for (int index = 0; index < powerStates.Length; index++)
    {      
      if (powerStates[index] == true)
      {
        int nodeIndex = index + 1;
        this.AddMenuItem($"{nodeIndex}: Switch OFF", async () =>
        {
          this.GetDisplay()?.SetCursorPosition(1, 0);
          this.GetDisplay()?.Write(PadDisplayValue("Executing ..."));

          //// execute shutdown command and wait to give the OS time to shut down
          //await this.DeviceController.Shutdown(this.AppSettings.Value.Nodes.Skip(nodeIndex - 1).First());
          //await Task.Delay(TimeSpan.FromSeconds(15));

          // power off the node
          await this.TuringController.PowerOffNode(nodeIndex); 
          this.ScheduleRefresh(TimeSpan.FromSeconds(2), true);
        });
      }
      else
      {
        int nodeIndex = index + 1;
        this.AddMenuItem($"{nodeIndex}: Switch ON", async () =>
        {
          this.GetDisplay()?.SetCursorPosition(1, 0);
          this.GetDisplay()?.Write(PadDisplayValue("Executing ..."));
          await this.TuringController.PowerOnNode(nodeIndex); 
          this.ScheduleRefresh(TimeSpan.FromSeconds(2), true);
        });
      }
    }

    if (powerStates.Where(value => value == false).Any())
    {
      this.AddMenuItem($"Power ON ALL", async () =>
      {
        this.GetDisplay()?.SetCursorPosition(1, 0);
        this.GetDisplay()?.Write(PadDisplayValue("Executing ..."));
        await this.TuringController.PowerOnAllNodes();
        this.ScheduleRefresh(TimeSpan.FromSeconds(2), true);
      });
    }

    if (powerStates.Where(value => value == true).Any())
    {
      this.AddMenuItem($"Power OFF ALL", async () =>
      {
        this.GetDisplay()?.SetCursorPosition(1, 0);
        this.GetDisplay()?.Write(PadDisplayValue("Executing ..."));
        await this.TuringController.PowerOffAllNodes();
        this.ScheduleRefresh(TimeSpan.FromSeconds(2), true);
      });
    }

    this.AddBackMenuItem();

    ShowMenu();
  }
}