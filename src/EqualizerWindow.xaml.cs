using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace SoundDrought;

public partial class EqualizerWindow : Window
{
    // dB for 60 Hz, 230 Hz, 910 Hz, 3.6 kHz, 14 kHz
    private static readonly (string Name, double[] Bands)[] Presets =
    [
        ("Flat", [0, 0, 0, 0, 0]),
        ("Bass Boost", [6, 3, 0, 0, 0]),
        ("Hardcore", [5, 2, -2, 2, 4]),
        ("Vocal", [-2, 0, 3, 3, 0]),
        ("Treble", [0, 0, 0, 3, 6]),
    ];

    private readonly MainWindow _main;
    private readonly Slider[] _sliders = new Slider[5];
    private readonly TextBlock[] _values = new TextBlock[5];
    private bool _loading = true;

    public EqualizerWindow(MainWindow main, AudioSettings audio)
    {
        InitializeComponent();
        _main = main;
        SourceInitialized += (_, _) => Native.UseDarkTitleBar(new WindowInteropHelper(this).Handle);

        for (int i = 0; i < 5; i++)
        {
            BandsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _values[i] = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 6) };
            _sliders[i] = new Slider { Style = (Style)Resources["BandSlider"], Value = audio.Bands.Length > i ? audio.Bands[i] : 0 };
            _sliders[i].ValueChanged += (_, _) => OnChanged(this, new RoutedEventArgs());
            var hz = AudioSettings.Frequencies[i];
            var label = new TextBlock
            {
                Text = hz >= 1000 ? (hz / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " kHz" : hz + " Hz",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xA0, 0xA0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
            };
            column.Children.Add(_values[i]);
            column.Children.Add(_sliders[i]);
            column.Children.Add(label);
            Grid.SetColumn(column, i);
            BandsGrid.Children.Add(column);
        }

        foreach (var (name, bands) in Presets)
        {
            var button = new Button { Content = name, Style = (Style)Resources["PresetButton"] };
            button.Click += (_, _) =>
            {
                _loading = true;
                for (int i = 0; i < 5; i++) _sliders[i].Value = bands[i];
                EnabledBox.IsChecked = true;
                _loading = false;
                OnChanged(this, new RoutedEventArgs());
            };
            PresetPanel.Children.Add(button);
        }

        EnabledBox.IsChecked = audio.Enabled;
        NormalizeBox.IsChecked = audio.Normalize;
        _loading = false;
        UpdateLabels();
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateLabels();
        var bands = new double[5];
        for (int i = 0; i < 5; i++) bands[i] = _sliders[i].Value;
        _main.ApplyAudio(new AudioSettings
        {
            Enabled = EnabledBox.IsChecked == true,
            Bands = bands,
            Normalize = NormalizeBox.IsChecked == true,
        });
    }

    private void UpdateLabels()
    {
        bool on = EnabledBox.IsChecked == true;
        for (int i = 0; i < 5; i++)
        {
            var v = _sliders[i].Value;
            _values[i].Text = (v > 0 ? "+" : "") + v.ToString("0.#", CultureInfo.InvariantCulture) + " dB";
            _sliders[i].Opacity = on ? 1 : 0.45;
        }
    }
}
