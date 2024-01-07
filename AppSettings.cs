namespace TuringPi.Panel;

public class AppSettings 
{
  public I2CSettings? I2C { get; set; }
  public SPISettings? SPI { get; set; }
  public GpioSettings? Gpio { get; set; }

  public ShiftRegisterSettings? ShiftRegister { get; set; }

  public DisplaySettings Display { get; set; } = new();  
  public ButtonSettings ActionButton { get; set; } = new(22);
  public ButtonSettings LeftButton { get; set; } = new(17);
  public ButtonSettings RightButton { get; set; } = new(27);

  public Node TuringPi { get; set; } = new();
  public IEnumerable<Node> Nodes { get; set; } = new List<Node>();

  public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(60);

  /// <summary>
  /// Return a "safe" copy of settings with security settings replaced with "********"
  /// </summary>
  /// <returns></returns>
  public AppSettings? SafeCopy()
  {
    AppSettings? safeCopy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(this));
    const string censoredValue = "********";

    if (safeCopy != null)
    { 
      foreach (Node? node in safeCopy.Nodes.Append(safeCopy.TuringPi))
      {
        node.KeyFile = String.IsNullOrEmpty(node.KeyFile) ? null : censoredValue;
        node.KeyFilePassPhrase=String.IsNullOrEmpty(node.KeyFilePassPhrase) ? null : censoredValue;
        node.UserName = censoredValue;
        node.Password = String.IsNullOrEmpty(node.Password) ? null : censoredValue;
        node.ExpectedFingerPrint = String.IsNullOrEmpty(node.ExpectedFingerPrint) ? null : censoredValue;        
      }
    }
    return safeCopy;
  }

  public class Node
  {
    public string? Name { get; set; }
    public string HostName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Password { get; set; }

    public string? KeyFile { get; set; }
    public string? KeyFilePassPhrase { get; set; }

    public string? ExpectedFingerPrint { get; set; }

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20);
  }

  public class I2CSettings
  {
    public int BusId { get; set; } = 1;
    public int Address { get; set; } = 0x27;
  }

  public class SPISettings
  {
    public int BusId { get; set; } = 0;
    public int ChipSelectLine { get; set; } = -1;
  }

  public abstract class PinSettings
  {
    public int RSPin { get; set; }
    public int ENPin { get; set; }
    public int BacklightPin { get; set; }
    public int D0Pin { get; set; }
    public int D1Pin { get; set; }
    public int D2Pin { get; set; }
    public int D3Pin { get; set; }
    public int D4Pin { get; set; }
    public int D5Pin { get; set; }
    public int D6Pin { get; set; }
    public int D7Pin { get; set; }

    public int[] DataPins4Bit()
    {
      return new int[] { this.D0Pin, this.D1Pin, this.D2Pin, this.D3Pin };
    }

    public int[] DataPins8Bit()
    {
      return new int[] { this.D0Pin, this.D1Pin, this.D2Pin, this.D3Pin, this.D4Pin, this.D5Pin, this.D6Pin, this.D7Pin };
    }
  }

  public class GpioSettings : PinSettings
  {
    public int RWPin { get; set; }
  }

  public class ShiftRegisterSettings : PinSettings
  {
    public int SDPin { get; set; }
    public int ClkPin { get; set; }
    public int LatchPin { get; set; }
    public int OutPin { get; set; }

  }

  public class DisplaySettings
  {
    public enum InterfaceTypes
    {
      I2C,
      GPIO,
      GpioShiftRegister,
      SPIShiftRegister
    }
    
    public InterfaceTypes InterfaceType { get; set; } = InterfaceTypes.I2C;

    public int Rows { get; set; } = 2;
    public int Columns { get; set; } = 20;
    public Boolean Use8Bit { get; set; } = false;
    public TimeSpan BacklightTimeout { get; set; } = TimeSpan.FromSeconds(45);
  }

  public class ButtonSettings
  {    
    public int GpioPin { get; set; }
    public int DebounceTimeMs { get; set; } = 250;
    public int DoublePressTimeMs { get; set; } = 750;
    public int HoldingTimeMs { get; set; } = 3000;


    public ButtonSettings() { }
   
    public ButtonSettings(int gpioPin) 
    { 
      this.GpioPin = gpioPin;
    }
  }
}
