using System.ComponentModel.DataAnnotations;
using System.Timers;
using Iot.Device.CharacterLcd;
using Microsoft.Extensions.Options;
using TuringPi.Panel.Drivers;
using UnitsNet;

namespace TuringPi.Panel.UI;

public abstract class PageBase
{
  protected event PageOpenedEventHandler<EventArgs>? PageOpened;
  protected event PageClosedEventHandler<EventArgs>? PageClosed;
  protected event RefreshEventHandler<ElapsedEventArgs>? Refresh;
  protected event ButtonPressedEventHandler<ButtonPressedEventArgs>? ButtonPressed;
  protected event ButtonDoublePressedEventHandler<ButtonDoublePressedEventArgs>? ButtonDoublePressed;
  protected event ButtonHoldEventHandler<ButtonHoldEventArgs>? ButtonHold;

  private const int FIRST_REFRESH_INTERVAL_MS = 25;

  // Special character codes, created in Panel.CreateCustomCharacters().  Use GetSpecialCharacter() to get a string representation that
  // you can use with LcdDisplay.Write()
  public enum SpecialCharacters : byte
  {
    // these are always available
    CHAR_PROGRESS_HALF = 0,
    CHAR_PROGRESS_FULL = 1,
    CHAR_PROGRESS_BG = 2,

    // these are only available while the home screen is being displayed for the first time
    CHAR_LOGO_TOP_LEFT = 3,
    CHAR_LOGO_TOP_RIGHT = 4,
    CHAR_LOGO_BOTTOM_LEFT = 5,
    CHAR_LOGO_BOTTOM_RIGHT = 6,

    // these replace the logo characters once the home page has been shown for the first time
    CHAR_UP_ARROW = 3,
    CHAR_DOWN_ARROW = 4,
    CHAR_DEGREES = 5,
    CHAR_SELECTED_ITEM = 6,
    CHAR_SELECTED_PAGE = 7     
  }

  // This is for use by json serialization in WebApiController/Shell
  public string Name => this.GetType().Name;

  protected Shell Shell { get; }
  protected IOptions<AppSettings> AppSettings { get; }

  /// <summary>
  /// Progress counter.  This is used to track the progress display.
  /// </summary>
  private int ProgressCounter { get; set; }

  private readonly static TimeSpan DEFAULT_PROGRESS_DELAY = TimeSpan.FromMilliseconds(500);

  /// <summary>
  /// Progress minimum 
  /// </summary>
  private int ProgressMin { get; set; }
  private int ProgressMax { get; set; }
  private double ProgressInterval { get; set; }

  private System.Timers.Timer RefreshTimer { get; }
  private System.Timers.Timer ProgressTimer { get; }

  protected bool AutoRefreshDisplay { get; private set; } = true;

  public PageBase(Shell shell, IOptions<AppSettings> appSettings)
  {
    this.Shell = shell;
    this.AppSettings = appSettings;

    this.ProgressTimer = new()
    {
      Enabled = false,
      AutoReset = false
    };
    this.ProgressTimer.Elapsed += ProgressTimer_Elapsed;

    this.RefreshTimer = new(TimeSpan.FromMilliseconds(FIRST_REFRESH_INTERVAL_MS))
    {
      Enabled = false,
      AutoReset = false
    };
    this.RefreshTimer.Elapsed += RefreshTimer_Elapsed;
  }

  public PageBase(Shell shell, IOptions<AppSettings> appSettings, bool autoRefreshDisplay) : this(shell, appSettings)
  {
    this.AutoRefreshDisplay = autoRefreshDisplay;
  }

  protected void Close()
  {
    this.RefreshTimer?.Stop();
    this.ProgressTimer?.Stop();

    this.Shell?.ClosePage();
  }

  /// <summary>
  /// Return the character width of the display panel.
  /// </summary>
  protected int PanelWidth => this.Shell.Panel.LcdDisplay?.Size.Width ?? 16;

  /// <summary>
  /// Return the character height of the display panel.
  /// </summary>
  protected int PanelHeight => this.Shell.Panel.LcdDisplay?.Size.Height ?? 2;

  /// <summary>
  /// Return the specified string, padded with spaces to fill/overwrite the display width.
  /// </summary>
  /// <param name="value"></param>
  /// <returns></returns>
  public string PadDisplayValue(string value) => value.PadRight(this.PanelWidth);

  /// <summary>
  /// Return a display object if this page is the current page, or null if not.
  /// </summary>
  /// <returns></returns>
  /// <remarks>
  /// This is to prevent pages which are not "topmost" from updating the display.
  /// </remarks>
  protected ICharacterLcd? GetDisplay()
  {
    return this.Shell.CurrentPage == this ? this.Shell.Panel.LcdDisplay : null;
  }

  /// <summary>
  /// Return a string representation for the specified special character.  Special characters are created by
  /// CreateSpecialCharacters().
  /// </summary>
  /// <param name="character"></param>
  /// <returns></returns>
  protected string GetSpecialCharacter(SpecialCharacters character) => char.ConvertFromUtf32((int)character);

  /// <summary>
  /// Schedule a display refresh after the specified time.
  /// </summary>
  /// <param name="time"></param>
  /// <param name="autoRefresh"></param>
  /// <remarks>
  /// Specify time=TimeSpan.Zero to disable the display refresh timer.
  /// </remarks>
  protected void ScheduleRefresh(TimeSpan time, Boolean autoRefresh)
  {
    this.AutoRefreshDisplay = autoRefresh;

    this.RefreshTimer.Stop();

    if (time != TimeSpan.Zero)
    {
      this.RefreshTimer.Interval = time.TotalMilliseconds;
      this.RefreshTimer.Start();
    }
  }

  /// <summary>
  /// Handle refresh timer events.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void RefreshTimer_Elapsed(object? sender, ElapsedEventArgs e)
  {
    if (this.Shell.CurrentPage == this)
    {
      Task task = this.OnRefresh(e);
    }
  }

  /// <summary>
  /// Call refresh event handlers.
  /// </summary>
  /// <param name="e"></param>
  protected virtual async Task OnRefresh(ElapsedEventArgs e)
  {
    if (this.Refresh != null)
    {
      try
      {
        await this.Refresh.InvokeAsync(this, e);
      }
      finally
      {
        if (this.AutoRefreshDisplay)
        {
          this.RefreshTimer.Stop();
          this.RefreshTimer.Interval = AppSettings.Value.RefreshInterval.TotalMilliseconds;
          this.RefreshTimer.Start();
        }
      }
    }
  }

  /// <summary>
  /// Start a progress display, specifying the total duration.
  /// </summary>
  /// <param name="show"></param>
  /// <remarks>
  /// A progress meter is shown in the bottom row of the display.  It automatically updates until complete.  Callers must call StopProgress before
  /// updating the display.
  /// </remarks>

  protected void ShowProgress(TimeSpan duration)
  {
    ShowProgress(0, this.PanelWidth-1, duration, DEFAULT_PROGRESS_DELAY);
  }

  /// <summary>
  /// Start a progress display, specifying the total duration, start and end column, with the default delay before starting.
  /// </summary>
  /// <param name="startCol"></param>
  /// <param name="finishCol"></param>
  /// <param name="duration"></param>
  protected void ShowProgress(int startCol, int finishCol, TimeSpan duration)
  {
    ShowProgress(startCol, finishCol, duration, DEFAULT_PROGRESS_DELAY);
  }

  /// <summary>
  /// Start a progress display, specifying the total duration, start and end column and delay.
  /// </summary>
  /// <param name="show"></param>
  /// <remarks>
  /// A progress meter is shown in the bottom row of the display.  It automatically updates until complete.  Callers must call StopProgress before
  /// updating the display.
  /// </remarks>
    protected void ShowProgress(int startCol, int finishCol, TimeSpan duration, TimeSpan delay)
  {
    if (duration < delay)
    {
      throw new ArgumentException("Duration must be greater than delay.", nameof(duration));
    }

    // we show progress using a half-block character for odd values and full-block character for even values, so
    // the max progress count is (finishCol-startCol) * 2.  
    this.ProgressMin = startCol * 2;
    this.ProgressMax = finishCol * 2;
    this.ProgressCounter = this.ProgressMin;

    this.ProgressInterval = (duration - delay).TotalMilliseconds / (this.ProgressMax - this.ProgressMin);
    this.ProgressTimer.Interval = delay == TimeSpan.Zero ? this.ProgressInterval : delay.TotalMilliseconds;

    this.ProgressTimer.Stop();
    this.ProgressTimer.Start();
  }

  /// <summary>
  /// Stop the progress display.
  /// </summary>
  protected void StopProgress()
  {
    this.ProgressTimer.Stop();
  }

  /// <summary>
  /// Update the progress display.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void ProgressTimer_Elapsed(object? sender, ElapsedEventArgs e)
  {
    if (this.ProgressCounter == this.ProgressMin)
    {
      // draw background
      this.GetDisplay()?.SetCursorPosition(this.ProgressMin / 2, this.PanelHeight - 1);
      this.GetDisplay()?.Write(new string(GetSpecialCharacter(SpecialCharacters.CHAR_PROGRESS_BG).First(), ((this.ProgressMax - this.ProgressMin) / 2) + 1));
    }

    // we show progress using a half-block character for odd values and full-block character for even values, so
    // the total progress "max" is PanelWidth * 2.  
    if (this.ProgressCounter <= this.ProgressMax)
    {
      this.ProgressCounter++;

      int column = (int)Math.Floor((decimal)(this.ProgressCounter) / 2) ;      
      this.GetDisplay()?.SetCursorPosition(column, this.PanelHeight - 1);
      
      if (this.ProgressCounter % 2 == 0)
      {
        // odd number, write a half character
        this.GetDisplay()?.Write(GetSpecialCharacter(SpecialCharacters.CHAR_PROGRESS_HALF));
      }
      else
      {
        // even number, write a full character
        this.GetDisplay()?.Write(GetSpecialCharacter(SpecialCharacters.CHAR_PROGRESS_FULL));
      }

      this.ProgressTimer.Stop();
      this.ProgressTimer.Interval = this.ProgressInterval;
      this.ProgressTimer.Start();
    }
    else
    {
      this.StopProgress();
    }
  }

  public virtual async Task OnPageOpened(EventArgs e)
  {
    if (this.PageOpened != null)
    {
      await PageOpened.InvokeAsync<EventArgs>(this, e);
    }

    this.RefreshTimer.Stop();
    this.RefreshTimer.Interval = FIRST_REFRESH_INTERVAL_MS;
    this.RefreshTimer.Start();
  }

  public virtual async Task OnPageClosed(EventArgs e)
  {
    if (this.PageClosed != null)
    {
      await PageClosed.InvokeAsync(this, e);
    }

    this.RefreshTimer.Stop();
  }

  public virtual async Task OnButtonPressed(ButtonPressedEventArgs e)
  {
    if (this.ButtonPressed != null)
    {
      await this.ButtonPressed.InvokeAsync(this, e);
    }
  }

  public virtual async Task OnButtonDoublePressed(ButtonDoublePressedEventArgs e)
  {
    if (this.ButtonDoublePressed != null)
    {
      await this.ButtonDoublePressed.InvokeAsync(this, e);
    }
  }

  public virtual async Task OnButtonHold(ButtonHoldEventArgs e)
  {
    if (this.ButtonHold != null)
    {
      await this.ButtonHold.Invoke(this, e);
    }
  }

  protected enum CharacterSets
  {
    Splash,
    Application
  }

  protected void CreateSpecialCharacters(CharacterSets characterSet)
  {
    if (this.Shell.Panel.LcdDisplay == null) return;      

    // progress indicator (half chararacter)
    this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_PROGRESS_HALF,
      0b_00000,
      0b_11100,
      0b_11100,
      0b_11111,
      0b_11111,
      0b_11100,
      0b_11100,
      0b_00000);

    // progress indicator (full chararacter)
    this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_PROGRESS_FULL,
      0b_00000,
      0b_11111,
      0b_11111,
      0b_11111,
      0b_11111,
      0b_11111,
      0b_11111,
      0b_00000);

    // progress "background"
    this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_PROGRESS_BG,
      0b_00000,
      0b_00000,
      0b_00000,
      0b_11111,
      0b_11111,
      0b_00000,
      0b_00000,
      0b_00000);

    if (characterSet == CharacterSets.Splash)
    {
      // logo - top left
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_LOGO_TOP_LEFT,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_11111,
        0b_10000,
        0b_10111,
        0b_10100,
        0b_10101);

      // logo - top right
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_LOGO_TOP_RIGHT,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_11111,
        0b_00001,
        0b_11101,
        0b_00101,
        0b_10101);

      // logo - bottom left
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_LOGO_BOTTOM_LEFT,
        0b_10101,
        0b_10100,
        0b_10111,
        0b_10000,
        0b_11111,
        0b_00000,
        0b_00000,
        0b_00000);

      // logo - bottom right
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_LOGO_BOTTOM_RIGHT,
        0b_10101,
        0b_00101,
        0b_11101,
        0b_00001,
        0b_11111,
        0b_00000,
        0b_00000,
        0b_00000);
    }
    else if (characterSet == CharacterSets.Application)
    {
      // application characters

      //// Up arrow
      //this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_UP_ARROW,
      //  0b_00100,
      //  0b_01110,
      //  0b_11111,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000);

      //// Down arrow
      //this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_DOWN_ARROW,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000,
      //  0b_00000,
      //  0b_11111,
      //  0b_01110,
      //  0b_00100);
      // Up arrow
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_UP_ARROW,
        0b_00010,
        0b_00111,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000);

      // Down arrow
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_DOWN_ARROW,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00111,
        0b_00010);

      // degrees (temperature) symbol
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_DEGREES,
        0b_00110,
        0b_01001,
        0b_01001,
        0b_00110,
        0b_00000,
        0b_00000,
        0b_00000,
        0b_00000);

      // menu page selected item indicator
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_SELECTED_ITEM,
        0b_00000,
        0b_00000,
        0b_01100,
        0b_11110,
        0b_11110,
        0b_01100,
        0b_00000,
        0b_00000);

      // menu page selected page indicator
      this.Shell.Panel.LcdDisplay.CreateCustomCharacter((int)UI.PageBase.SpecialCharacters.CHAR_SELECTED_PAGE,
        0b_00000,
        0b_10000,
        0b_11000,
        0b_11100,
        0b_11000,
        0b_10000,
        0b_00000,
        0b_00000);

    }

  }
}
