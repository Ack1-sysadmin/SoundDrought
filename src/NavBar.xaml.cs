using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoundDrought;

/// <summary>Spotify-style title bar that replaces SoundCloud's own header.</summary>
public partial class NavBar : UserControl
{
    // Below this width feed/library/notifications/messages move into the account menu.
    private const double CompactWidth = 760;

    private MainWindow? _host;
    private PlayerState _state = new();
    private string _avatarUrl = "";

    public NavBar()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayoutMode();
    }

    private bool Compact => ActualWidth < CompactWidth;

    public void Attach(MainWindow host)
    {
        _host = host;
        host.StateChanged += (_, _) => UpdateMaximizeButton();
        UpdateMaximizeButton();
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void Update(PlayerState state)
    {
        _state = state;

        // SoundCloud navigates in-page, which WebView2's own history doesn't see; the page reports it.
        BackButton.IsEnabled = state.CanGoBack;
        ForwardButton.IsEnabled = state.CanGoForward;
        SignInButton.Visibility = state.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
        NotificationsDot.Visibility = state.NewNotifications ? Visibility.Visible : Visibility.Collapsed;
        MessagesDot.Visibility = state.NewMessages ? Visibility.Visible : Visibility.Collapsed;

        if (state.Avatar != _avatarUrl)
        {
            _avatarUrl = state.Avatar;
            Avatar.Fill = Uri.TryCreate(state.Avatar, UriKind.Absolute, out var uri)
                ? new ImageBrush(new BitmapImage(uri)) { Stretch = Stretch.UniformToFill }
                : null;
        }

        Highlight(HomeButton, state.Path is "/" or "/discover");
        Highlight(FeedButton, state.Path == "/feed");
        Highlight(LibraryButton, state.Path.StartsWith("/you/"));
        Highlight(NotificationsButton, state.Path == "/notifications");
        Highlight(MessagesButton, state.Path.StartsWith("/messages"));

        UpdateLayoutMode();
    }

    private static void Highlight(Button button, bool active)
    {
        if (active) button.Foreground = (Brush)Application.Current.Resources["TextBrush"];
        else button.ClearValue(ForegroundProperty);
    }

    private void UpdateLayoutMode()
    {
        SectionButtons.Visibility = Compact || !_state.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
        // In compact mode the badges live on the avatar.
        AccountDot.Visibility = Compact && (_state.NewNotifications || _state.NewMessages)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateMaximizeButton()
    {
        bool maximized = _host?.WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = maximized ? L.Restore : L.Maximize;
    }

    private void Go(string path) => _host?.Send("nav:" + path);

    private void OnBack(object sender, RoutedEventArgs e) => _host?.Send("back");
    private void OnForward(object sender, RoutedEventArgs e) => _host?.Send("forward");
    private void OnHome(object sender, RoutedEventArgs e) => Go("/discover");
    private void OnFeed(object sender, RoutedEventArgs e) => Go("/feed");
    private void OnLibrary(object sender, RoutedEventArgs e) => Go("/you/library");
    private void OnNotifications(object sender, RoutedEventArgs e) => Go("/notifications");
    private void OnMessages(object sender, RoutedEventArgs e) => Go("/messages");
    private void OnSignIn(object sender, RoutedEventArgs e) => _host?.Send("signin");

    // ---- Search ----

    private void OnSearchFieldClick(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Focus();
        e.Handled = true;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        bool empty = SearchBox.Text.Length == 0;
        SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Hidden;
        ClearSearchButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchBox.Text.Trim().Length > 0)
        {
            Go("/search?q=" + Uri.EscapeDataString(SearchBox.Text.Trim()));
            _host?.FocusPage();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            _host?.FocusPage();
            e.Handled = true;
        }
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    // ---- Account menu ----

    private void OnAccount(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.Resources["DarkMenu"],
            PlacementTarget = AccountButton,
            Placement = PlacementMode.Bottom,
            HorizontalOffset = -170,
            VerticalOffset = 4,
        };

        if (_state.LoggedIn)
        {
            if (Compact)
            {
                menu.Items.Add(Item(L.Feed, () => Go("/feed")));
                menu.Items.Add(Item(L.Library, () => Go("/you/library")));
                menu.Items.Add(Item(Badge(L.Notifications, _state.NewNotifications), () => Go("/notifications")));
                menu.Items.Add(Item(Badge(L.Messages, _state.NewMessages), () => Go("/messages")));
                menu.Items.Add(Separator());
            }
            if (_state.UserUrl.Length > 0)
                menu.Items.Add(Item(L.Profile, () => Go(_state.UserUrl)));
            menu.Items.Add(Item(L.Likes, () => Go("/you/likes")));
            menu.Items.Add(Item(L.Playlists, () => Go("/you/sets")));
            menu.Items.Add(Item(L.Stations, () => Go("/you/stations")));
            menu.Items.Add(Item(L.Following, () => Go("/you/following")));
            menu.Items.Add(Item(L.History, () => Go("/you/history")));
            menu.Items.Add(Separator());
            menu.Items.Add(Item(L.Upload, () => Go("/upload")));
        }
        menu.Items.Add(Item(L.MiniPlayer, () => _host?.ShowMini(), L.MiniPlayerShortcut));
        menu.Items.Add(Item(L.Equalizer + " …", () => _host?.ShowEqualizer()));
        menu.Items.Add(Separator());
        menu.Items.Add(Item(L.SoundCloudSettings, () => Go("/settings")));
        if (_state.LoggedIn)
            menu.Items.Add(Item(L.SignOut, () => Go("/logout")));
        else
            menu.Items.Add(Item(L.SignIn, () => _host?.Send("signin")));

        menu.IsOpen = true;
    }

    private static string Badge(string text, bool active) => active ? text + "  •" : text;

    private static MenuItem Item(string header, Action action, string? gesture = null)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = gesture ?? "",
            Style = (Style)Application.Current.Resources["DarkMenuItem"],
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static Separator Separator() => new() { Style = (Style)Application.Current.Resources["DarkSeparator"] };

    // ---- Window caption ----

    private void OnMinimize(object sender, RoutedEventArgs e) => _host!.WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        _host!.WindowState = _host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => _host!.Close();
}
