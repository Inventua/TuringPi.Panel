using System.Reflection.Metadata;

namespace TuringPi.Panel.UI;

public class ButtonPressedEventArgs : EventArgs
{
    public enum Buttons
    {
        LeftButton,
        RightButton,
        ActionButton
    }

    public Buttons Button { get; }

    public ButtonPressedEventArgs(Buttons button)
    {
        Button = button;
    }
}


public class ButtonDoublePressedEventArgs : ButtonPressedEventArgs
{
  public ButtonDoublePressedEventArgs(Buttons button) : base(button) { }
}


public class ButtonHoldEventArgs : ButtonPressedEventArgs
{
    public ButtonHoldEventArgs(Buttons button) : base(button) { }
}
