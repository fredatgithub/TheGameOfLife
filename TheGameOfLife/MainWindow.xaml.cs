using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace TheGameOfLife
{
  public partial class MainWindow: Window
  {
    private static readonly string WindowSettingsPath = Path.Combine(AppContext.BaseDirectory, "window-settings.json");

    private readonly DispatcherTimer _timer = new();
    private bool[,] _cells = new bool[LifeBoard.Columns, LifeBoard.Rows];
    private int _generation;

    public MainWindow()
    {
      InitializeComponent();
      WindowStartupLocation = WindowStartupLocation.Manual;
      RestoreWindowSettings();
      var version = typeof(MainWindow).Assembly.GetName().Version!;
      Title = $"Fred's Game of life V{version.Major}.{version.Minor}.{version.Build}";
      Board.CellClicked += OnCellClicked;
      SpeedSlider.ValueChanged += (_, _) => UpdateSpeed();
      _timer.Tick += (_, _) => AdvanceGeneration();
      Closing += (_, _) => SaveWindowSettings();
      UpdateSpeed();
      UpdateStats();
    }

    private void RestoreWindowSettings()
    {
      try
      {
        using var stream = File.OpenRead(WindowSettingsPath);
        var settings = JsonSerializer.Deserialize<WindowSettings>(stream);
        if (settings is null || !settings.IsValid)
        {
          ShowSettingsWarning("Les dimensions et la position enregistrées sont invalides. La fenêtre utilisera sa taille par défaut.");
          return;
        }

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var width = Math.Min(settings.Width, SystemParameters.VirtualScreenWidth);
        var height = Math.Min(settings.Height, SystemParameters.VirtualScreenHeight);

        if (width < MinWidth || height < MinHeight)
        {
          ShowSettingsWarning("La résolution actuelle est trop petite pour restaurer la fenêtre enregistrée. La fenêtre utilisera sa taille par défaut.");
          return;
        }

        Width = width;
        Height = height;
        Left = Math.Clamp(settings.Left, virtualLeft, virtualRight - width);
        Top = Math.Clamp(settings.Top, virtualTop, virtualBottom - height);

        if (settings.IsMaximized)
        {
          WindowState = WindowState.Maximized;
        }
      }
      catch (FileNotFoundException)
      {
      }
      catch (DirectoryNotFoundException)
      {
      }
      catch (JsonException)
      {
        ShowSettingsWarning("Le fichier de position de la fenêtre est illisible. La fenêtre utilisera sa taille par défaut.");
      }
      catch (IOException)
      {
        ShowSettingsWarning("Le fichier de position de la fenêtre n'a pas pu être lu. La fenêtre utilisera sa taille par défaut.");
      }
      catch (UnauthorizedAccessException)
      {
        ShowSettingsWarning("L'accès au fichier de position de la fenêtre a été refusé. La fenêtre utilisera sa taille par défaut.");
      }
    }

    private void SaveWindowSettings()
    {
      try
      {
        var bounds = WindowState == WindowState.Normal
          ? new Rect(Left, Top, Width, Height)
          : RestoreBounds;
        var settings = new WindowSettings(
          bounds.Left,
          bounds.Top,
          bounds.Width,
          bounds.Height,
          WindowState == WindowState.Maximized);

        Directory.CreateDirectory(Path.GetDirectoryName(WindowSettingsPath)!);
        File.WriteAllText(WindowSettingsPath, JsonSerializer.Serialize(settings));
      }
      catch (IOException)
      {
        ShowSettingsWarning("La position de la fenêtre n'a pas pu être enregistrée.");
      }
      catch (UnauthorizedAccessException)
      {
        ShowSettingsWarning("L'accès au fichier de position de la fenêtre a été refusé. Les paramètres n'ont pas été enregistrés.");
      }
    }

    private void ShowSettingsWarning(string message)
    {
      MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private sealed record WindowSettings(double Left, double Top, double Width, double Height, bool IsMaximized)
    {
      public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Top) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
      if (_timer.IsEnabled)
      {
        _timer.Stop();
        PlayButton.Content = "Démarrer";
      }
      else
      {
        _timer.Start();
        PlayButton.Content = "Pause";
      }
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
      AdvanceGeneration();
    }

    private void RandomButton_Click(object sender, RoutedEventArgs e)
    {
      var random = new Random();
      for (var x = 0; x < LifeBoard.Columns; x++)
      {
        for (var y = 0; y < LifeBoard.Rows; y++)
        {
          _cells[x, y] = random.NextDouble() < 0.28;
        }
      }

      _generation = 0;
      RefreshBoard();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
      _timer.Stop();
      PlayButton.Content = "Démarrer";
      _cells = new bool[LifeBoard.Columns, LifeBoard.Rows];
      _generation = 0;
      RefreshBoard();
    }

    private void OnCellClicked(int x, int y)
    {
      _cells[x, y] = !_cells[x, y];
      RefreshBoard();
    }

    private void AdvanceGeneration()
    {
      var next = new bool[LifeBoard.Columns, LifeBoard.Rows];

      for (var x = 0; x < LifeBoard.Columns; x++)
      {
        for (var y = 0; y < LifeBoard.Rows; y++)
        {
          var neighbors = CountLiveNeighbors(x, y);
          next[x, y] = _cells[x, y]
              ? neighbors is 2 or 3
              : neighbors == 3;
        }
      }

      _cells = next;
      _generation++;
      RefreshBoard();
    }

    private int CountLiveNeighbors(int cellX, int cellY)
    {
      var count = 0;

      for (var offsetX = -1; offsetX <= 1; offsetX++)
      {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
          if (offsetX == 0 && offsetY == 0)
          {
            continue;
          }

          var x = cellX + offsetX;
          var y = cellY + offsetY;
          if (x >= 0 && x < LifeBoard.Columns &&
              y >= 0 && y < LifeBoard.Rows && _cells[x, y])
          {
            count++;
          }
        }
      }

      return count;
    }

    private void RefreshBoard()
    {
      Board.Cells = _cells;
      Board.InvalidateVisual();
      UpdateStats();
    }

    private void UpdateStats()
    {
      var population = 0;
      foreach (var alive in _cells)
      {
        if (alive)
        {
          population++;
        }
      }

      StatsText.Text = $"Génération {_generation}   ·   {population} cellules vivantes";
    }

    private void UpdateSpeed()
    {
      var generationsPerSecond = (int)SpeedSlider.Value;
      _timer.Interval = TimeSpan.FromMilliseconds(1000d / generationsPerSecond);
      SpeedText.Text = $"{generationsPerSecond} gen/s";
    }
  }
}
