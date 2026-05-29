using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.UI;

internal static class DarkTheme
{
    internal static readonly Brush Foreground = Frozen(Color.FromRgb(0xC8, 0xC8, 0xC8));
    internal static readonly Brush Muted      = Frozen(Color.FromRgb(0xA0, 0xA0, 0xA0));
    internal static readonly Brush Accent     = Frozen(Color.FromRgb(0x29, 0xAB, 0xE2));
    internal static readonly Brush FieldBack  = Frozen(Color.FromRgb(0x2A, 0x2A, 0x2E));
    internal static readonly Brush Border     = Frozen(Color.FromRgb(0x3F, 0x3F, 0x46));

    internal static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static LinearGradientBrush WindowBackground()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x22, 0x22, 0x26), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x1C), 1));
        brush.Freeze();
        return brush;
    }

    internal static void Apply(FrameworkElement element)
    {
        AddStyle(element, typeof(ComboBox), ComboBoxStyle);
        AddStyle(element, typeof(ComboBoxItem), ComboBoxItemStyle);
        AddStyle(element, typeof(Slider), SliderStyle);
        AddStyle(element, typeof(Button), ButtonStyle);
    }

    private static void AddStyle(FrameworkElement element, Type targetType, string xaml)
    {
        try
        {
            if (XamlReader.Parse(xaml) is Style style)
                element.Resources[targetType] = style;
        }
        catch (Exception e)
        {
            Debug.Print($"加载 {targetType.Name} 样式失败: {e.Message}");
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    internal static void EnableDarkTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int enabled = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
        }
        catch (Exception e)
        {
            Debug.Print($"设置深色标题栏失败: {e.Message}");
        }
    }

    private const string Ns =
        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

    private static readonly string ComboBoxStyle = $@"
<Style {Ns} TargetType='ComboBox'>
  <Setter Property='Foreground' Value='#C8C8C8'/>
  <Setter Property='FontSize' Value='13'/>
  <Setter Property='Height' Value='34'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBox'>
        <Grid>
          <ToggleButton Focusable='False' ClickMode='Press'
                        IsChecked='{{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={{RelativeSource TemplatedParent}}}}'>
            <ToggleButton.Template>
              <ControlTemplate TargetType='ToggleButton'>
                <Border CornerRadius='8' BorderThickness='1'>
                  <Border.Background><SolidColorBrush Color='#2A2A2E'/></Border.Background>
                  <Border.BorderBrush><SolidColorBrush x:Name='bb' Color='#3F3F46'/></Border.BorderBrush>
                  <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'
                        Data='M0,0 L8,0 L4,5 Z' Fill='#A0A0A0'/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property='IsMouseOver' Value='True'>
                    <Trigger.EnterActions>
                      <BeginStoryboard><Storyboard>
                        <ColorAnimation Storyboard.TargetName='bb' Storyboard.TargetProperty='Color' To='#29ABE2' Duration='0:0:0.18'/>
                      </Storyboard></BeginStoryboard>
                    </Trigger.EnterActions>
                    <Trigger.ExitActions>
                      <BeginStoryboard><Storyboard>
                        <ColorAnimation Storyboard.TargetName='bb' Storyboard.TargetProperty='Color' To='#3F3F46' Duration='0:0:0.18'/>
                      </Storyboard></BeginStoryboard>
                    </Trigger.ExitActions>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter IsHitTestVisible='False' Margin='12,0,30,0'
                            VerticalAlignment='Center' HorizontalAlignment='Left'
                            Content='{{TemplateBinding SelectionBoxItem}}'
                            ContentTemplate='{{TemplateBinding SelectionBoxItemTemplate}}'
                            ContentStringFormat='{{TemplateBinding SelectionBoxItemStringFormat}}'/>
          <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False'
                 IsOpen='{{TemplateBinding IsDropDownOpen}}' PopupAnimation='Fade'>
            <Border MinWidth='{{TemplateBinding ActualWidth}}' MaxHeight='{{TemplateBinding MaxDropDownHeight}}'
                    CornerRadius='8' Background='#252528' BorderBrush='#3F3F46' BorderThickness='1'
                    Margin='0,4,0,6' Padding='0,5' SnapsToDevicePixels='True'>
              <Border.Effect><DropShadowEffect BlurRadius='14' ShadowDepth='2' Opacity='0.5' Color='#000000'/></Border.Effect>
              <ScrollViewer SnapsToDevicePixels='True'>
                <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/>
              </ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string ComboBoxItemStyle = $@"
<Style {Ns} TargetType='ComboBoxItem'>
  <Setter Property='Foreground' Value='#C8C8C8'/>
  <Setter Property='Padding' Value='12,8'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBoxItem'>
        <Border CornerRadius='6' Margin='5,2' Padding='{{TemplateBinding Padding}}'>
          <Border.Background><SolidColorBrush x:Name='bg' Color='#003277A0'/></Border.Background>
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsHighlighted' Value='True'>
            <Setter Property='Foreground' Value='#FFFFFF'/>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#FF3277A0' Duration='0:0:0.12'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#003277A0' Duration='0:0:0.12'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string SliderStyle = $@"
<Style {Ns} TargetType='Slider'>
  <Setter Property='Height' Value='24'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Slider'>
        <Grid VerticalAlignment='Center'>
          <Border Height='4' CornerRadius='2' Background='#3F3F46' VerticalAlignment='Center'/>
          <Track x:Name='PART_Track'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Focusable='False' Command='{{x:Static Slider.DecreaseLarge}}'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Height='4' CornerRadius='2' Background='#29ABE2' VerticalAlignment='Center'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Focusable='False' Command='{{x:Static Slider.IncreaseLarge}}'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Background='#00000000'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Focusable='False'>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Ellipse Width='14' Height='14' Fill='#FFFFFF'>
                      <Ellipse.Effect><DropShadowEffect BlurRadius='4' ShadowDepth='0' Opacity='0.4' Color='#000000'/></Ellipse.Effect>
                    </Ellipse>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string ButtonStyle = $@"
<Style {Ns} TargetType='Button'>
  <Setter Property='Foreground' Value='#FFFFFF'/>
  <Setter Property='FontSize' Value='13'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Padding' Value='16,6'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Button'>
        <Border CornerRadius='6' Padding='{{TemplateBinding Padding}}'>
          <Border.Background><SolidColorBrush x:Name='bg' Color='#29ABE2'/></Border.Background>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#45B8E8' Duration='0:0:0.15'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#29ABE2' Duration='0:0:0.15'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
}
