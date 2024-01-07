using Iot.Device.Button;
using System.Device.Gpio.Drivers;
using System.Device.Gpio;
using System.Device.I2c;
using Microsoft.Extensions.Options;
using Iot.Device.CharacterLcd;
using TuringPi.Panel.UI;
using System.Timers;
using Iot.Device.Multiplexing;

namespace TuringPi.Panel.Drivers;

// https://github.com/dotnet/iot/blob/main/src/devices/README.md

public class Panel : IDisposable
{
  public event ButtonPressedEventHandler<ButtonPressedEventArgs>? ButtonPressed;
  public event ButtonDoublePressedEventHandler<ButtonDoublePressedEventArgs>? ButtonDoublePressed;
  public event ButtonHoldEventHandler<ButtonHoldEventArgs>? ButtonHold;
  
  private ILogger<Panel>? Logger { get; }

  private I2cDevice? I2cDevice { get; }
  private ShiftRegister? ShiftRegister { get; }

  private GpioController? GpioController { get; }
  public Hd44780? LcdDisplay { get; }
  private Boolean IsBacklightOn { get; set; }

  private GpioButton? ActionButton { get; }
  private GpioButton? LeftButton { get; }
  private GpioButton? RightButton { get; }

  private System.Timers.Timer BacklightTimer { get; }

  private bool disposedValue;

  public Panel(IOptions<AppSettings> settings, ILogger<Panel> logger)
  {
    this.Logger = logger;

    this.BacklightTimer = new(settings.Value.Display.BacklightTimeout)
    {
      AutoReset = false
    };
    this.BacklightTimer.Elapsed += BacklightTimer_Elapsed;

    // set up hardware when we run in Linux.  This is so that when we run in Windows (for testing) we do not try to create driver objects for 
    // components that are not available in Windows.
    if (System.Environment.OSVersion.Platform == PlatformID.Unix)
    {
      this.GpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver());

      // create display interface based on config settings
      switch (settings.Value.Display.InterfaceType)
      {
        case AppSettings.DisplaySettings.InterfaceTypes.I2C:
          if (settings.Value.I2C == null) settings.Value.I2C = new();  // use default settings
          this.I2cDevice = I2cDevice.Create(new(settings.Value.I2C.BusId, settings.Value.I2C.Address));
          this.LcdDisplay = new Hd44780(new System.Drawing.Size(settings.Value.Display.Columns, settings.Value.Display.Rows), LcdInterface.CreateI2c(I2cDevice, settings.Value.Display.Use8Bit));
          break;

        case AppSettings.DisplaySettings.InterfaceTypes.GPIO:
          this.Logger?.LogWarning("Display interface type Gpio is for future use and is untested.");

          if (settings.Value.Gpio == null) settings.Value.Gpio = new();  // use default settings

          this.LcdDisplay = new Hd44780
          (
            new System.Drawing.Size(settings.Value.Display.Columns, settings.Value.Display.Rows), 
            LcdInterface.CreateGpio
            (
              settings.Value.Gpio.RSPin, 
              settings.Value.Gpio.ENPin, 
              settings.Value.Display.Use8Bit ? settings.Value.Gpio.DataPins8Bit() : settings.Value.Gpio.DataPins4Bit(), 
              settings.Value.Gpio.BacklightPin, 
              1, 
              settings.Value.Gpio.RWPin, 
              GpioController, 
              false
            )
          );          
          break;

        case AppSettings.DisplaySettings.InterfaceTypes.SPIShiftRegister:
          this.Logger?.LogWarning("Display interface type SPIShiftRegister is for future use and is untested.");

          if (settings.Value.ShiftRegister == null) settings.Value.ShiftRegister = new();  // use default settings
          if (settings.Value.SPI == null) settings.Value.SPI = new();   // use default settings

          this.ShiftRegister = new(System.Device.Spi.SpiDevice.Create(new(settings.Value.SPI.BusId, settings.Value.SPI.ChipSelectLine)), 8);
          this.LcdDisplay = new Hd44780
          (
            new System.Drawing.Size(settings.Value.Display.Columns, settings.Value.Display.Rows), 
            LcdInterface.CreateFromShiftRegister
            (
              settings.Value.ShiftRegister.RSPin,
              settings.Value.ShiftRegister.ENPin,
              settings.Value.Display.Use8Bit ? settings.Value.ShiftRegister.DataPins8Bit() : settings.Value.ShiftRegister.DataPins4Bit(),
              settings.Value.ShiftRegister.BacklightPin,
              this.ShiftRegister,
              false
            )
          );          
          break;

        case AppSettings.DisplaySettings.InterfaceTypes.GpioShiftRegister:
          this.Logger?.LogWarning("Display interface type GpioShiftRegister is for future use and is untested.");

          if (settings.Value.ShiftRegister == null) settings.Value.ShiftRegister = new();  // use default settings
          if (settings.Value.Gpio == null) settings.Value.Gpio = new();  // use default settings

          this.ShiftRegister = new(new ShiftRegisterPinMapping(settings.Value.ShiftRegister.SDPin, settings.Value.ShiftRegister.ClkPin, settings.Value.ShiftRegister.LatchPin, settings.Value.ShiftRegister.OutPin), 8, this.GpioController, false);

          this.LcdDisplay = new Hd44780
          (
            new System.Drawing.Size(settings.Value.Display.Columns, settings.Value.Display.Rows),
            LcdInterface.CreateFromShiftRegister
            (
              settings.Value.Gpio.RSPin,
              settings.Value.Gpio.ENPin,
              settings.Value.Display.Use8Bit ? settings.Value.Gpio.DataPins8Bit() : settings.Value.Gpio.DataPins4Bit(),
              settings.Value.Gpio.BacklightPin,
              this.ShiftRegister,
              false
            )
          );
          break;
      }

      if (this.LcdDisplay != null)
      {
        this.LcdDisplay.DisplayOn = true;
        this.LcdDisplay.Clear();
        EnableBacklight();
      }

      this.ActionButton = CreateButton(settings.Value.ActionButton, ButtonPressedEventArgs.Buttons.ActionButton);
      this.LeftButton = CreateButton(settings.Value.LeftButton, ButtonPressedEventArgs.Buttons.LeftButton);
      this.RightButton = CreateButton(settings.Value.RightButton, ButtonPressedEventArgs.Buttons.RightButton);
    }
        
    this.Logger?.LogTrace("Panel Driver Started.");
  }

  /// <summary>
  /// Create and return a button instance to set up a GPIO button using the supplied settings.
  /// </summary>
  /// <param name="settings"></param>
  /// <returns></returns>
  public GpioButton CreateButton(AppSettings.ButtonSettings settings, ButtonPressedEventArgs.Buttons which)
  {
    if (settings.DoublePressTimeMs < settings.DebounceTimeMs * 3)
    {
      settings.DoublePressTimeMs = settings.DebounceTimeMs * 3;
      this.Logger?.LogWarning("The configured DoublePressTimeMs value[{doublepress}] is less than three times the DebounceTimeMs value [{debounce}] and has been reset to [{newvalue}].", settings.DoublePressTimeMs, settings.DebounceTimeMs, settings.DebounceTimeMs * 3);
    }

    GpioButton button = new
    (
      settings.GpioPin,
      TimeSpan.FromMilliseconds(settings.DoublePressTimeMs),
      TimeSpan.FromMilliseconds(settings.HoldingTimeMs),
      false,
      false,
      this.GpioController,
      false,
      TimeSpan.FromMilliseconds(settings.DebounceTimeMs)
    );

    button.IsDoublePressEnabled = true;
    button.IsHoldingEnabled = true;

    button.Press += async (sender, e) => { await OnRaiseButtonPressed(sender as ButtonBase, which); };    
    button.DoublePress += async (sender, e) => { await OnRaiseButtonDoublePressed(sender as ButtonBase, which); };   
    button.Holding += async (sender, e) => { await OnRaiseButtonHold(sender as ButtonBase, which); };
    
    return button;
  }

  /// <summary>
  /// Switch the LCD backlight on, if it is not already on.  If the backlight setting is changed, start the backlight timer so that it switches 
  /// off automatically. Return true/false to indicate whether the backlight setting was changed.  If the returned value is true, the keypress 
  /// should not be propagated to event subscribers.
  /// </summary>
  /// <returns></returns>
  public Boolean EnableBacklight()
  {
    if (!this.IsBacklightOn)
    {
      if (this.LcdDisplay != null)
      {
        this.LcdDisplay.BacklightOn = true;
        this.IsBacklightOn = true;
      }

      ResetBacklightTimeout();

      Logger?.LogTrace("Backlight on");

      return true;
    }

    return false;
  }

  /// <summary>
  /// Restart the backlight timer.
  /// </summary>
  public void ResetBacklightTimeout()
  {
    this.BacklightTimer.Stop();
    this.BacklightTimer.Start();
  }

  /// <summary>
  /// Switch the LCD backlight off.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void BacklightTimer_Elapsed(object? sender, ElapsedEventArgs e)
  {
    if (this.LcdDisplay != null)
    {
      this.LcdDisplay.BacklightOn = false;
      this.IsBacklightOn = false;
      this.Logger?.LogTrace("Backlight off");
    }
  }

  /// <summary>
  /// If the backlight is off, switch it on.  If the backlight is on, invoke the button pressed event in event subscribers.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  public virtual async Task OnRaiseButtonPressed(ButtonBase? sender, ButtonPressedEventArgs.Buttons button)
  {
    Logger?.LogDebug("Button Pressed: {btn}", button.ToString());

    if (!this.IsBacklightOn)
    {
      EnableBacklight();
    }
    else
    {
      // only invoke the event if the backlight was already on - so that all buttons switch the backlight on, if it was off and
      // don't do their normal action
      if (this.ButtonPressed != null)
      {
        this.Logger?.LogTrace("Invoke Button Pressed: {btn}", button.ToString());
        await this.ButtonPressed.InvokeAsync<ButtonPressedEventArgs>(this, new(button));
      }

      // every time a button is pressed, the backlight timeout is reset
      ResetBacklightTimeout();
    }
  }

  /// <summary>
  /// Invoke the button DoublePressed event in event subscribers.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  public virtual async Task OnRaiseButtonDoublePressed(ButtonBase? sender, ButtonPressedEventArgs.Buttons button)
  {
    Logger?.LogDebug("Button Double-Pressed: {btn}", button.ToString());

    if (this.ButtonDoublePressed != null)
    {
      await this.ButtonDoublePressed.InvokeAsync<ButtonDoublePressedEventArgs>(this, new(button));
    }

    // every time a button is pressed, the backlight timeout is reset
    ResetBacklightTimeout();
  }

  /// <summary>
  /// Invoke the button Hold event in event subscribers.
  /// </summary>
  /// <param name="button"></param>
  /// <returns></returns>
  public virtual async Task OnRaiseButtonHold(ButtonBase? sender, ButtonPressedEventArgs.Buttons button)
  {
    Logger?.LogDebug("Button Hold: {btn}", button.ToString());

    if (this.ButtonHold != null)
    {
      await this.ButtonHold.InvokeAsync<ButtonHoldEventArgs>(this, new(button));
    }

    // every time a button is pressed, the backlight timeout is reset
    ResetBacklightTimeout();
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!disposedValue)
    {
      if (disposing)
      {
        this.ActionButton?.Dispose();
        this.LeftButton?.Dispose();
        this.RightButton?.Dispose();

        if (this.LcdDisplay != null)
        {
          this.LcdDisplay.DisplayOn = false;
        }

        this.LcdDisplay?.Dispose();
        this.ShiftRegister?.Dispose();
        this.I2cDevice?.Dispose();

        this.GpioController?.Dispose();
      }

      // TODO: free unmanaged resources (unmanaged objects) and override finalizer
      // TODO: set large fields to null
      disposedValue = true;
    }
  }

  // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
  // ~Panel()
  // {
  //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
  //     Dispose(disposing: false);
  // }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
