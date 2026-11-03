using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace TheGameOfLife
{
  public partial class MainWindow: Window
  {
    private const int MaxRememberedStates = 10_000;
    private static readonly string WindowSettingsPath = Path.Combine(AppContext.BaseDirectory, "window-settings.json");

    private readonly DispatcherTimer _timer = new();
    private readonly HashSet<string> _seenStates = new(StringComparer.Ordinal);
    private readonly Queue<string> _stateHistory = new();
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
      catch (FileNotFoundException fileNotFoundException)
      {
        if (fileNotFoundException.FileName != WindowSettingsPath)
        {
          ShowSettingsWarning("Le fichier de position de la fenêtre est introuvable. La fenêtre utilisera sa taille par défaut.");
        }
      }
      catch (DirectoryNotFoundException directoryNotFoundException)
      {
        if (directoryNotFoundException.Message.Contains(WindowSettingsPath))
        {
          ShowSettingsWarning("Le fichier de position de la fenêtre est introuvable. La fenêtre utilisera sa taille par défaut.");
        }
      }
      catch (JsonException jsonException)
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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new SaveFileDialog
      {
        Title = "Enregistrer une partie",
        Filter = "Fichier du Jeu de la vie (*.json)|*.json",
        DefaultExt = ".json",
        AddExtension = true,
        FileName = $"FredGameOfLife-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json"
      };

      if (dialog.ShowDialog(this) != true)
      {
        return;
      }

      try
      {
        var savedCells = new bool[LifeBoard.Columns * LifeBoard.Rows];
        for (var y = 0; y < LifeBoard.Rows; y++)
        {
          for (var x = 0; x < LifeBoard.Columns; x++)
          {
            savedCells[y * LifeBoard.Columns + x] = _cells[x, y];
          }
        }

        var save = new GameSave(
          LifeBoard.Columns,
          LifeBoard.Rows,
          _generation,
          CountLiveCells(),
          savedCells);
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(save));
      }
      catch (IOException)
      {
        ShowGameSaveError("La partie n'a pas pu être enregistrée dans le fichier choisi.");
      }
      catch (UnauthorizedAccessException)
      {
        ShowGameSaveError("L'accès au fichier choisi a été refusé. La partie n'a pas été enregistrée.");
      }
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog
      {
        Title = "Charger une partie",
        Filter = "Fichier du Jeu de la vie (*.json)|*.json",
        CheckFileExists = true
      };

      if (dialog.ShowDialog(this) != true)
      {
        return;
      }

      try
      {
        var save = JsonSerializer.Deserialize<GameSave>(File.ReadAllText(dialog.FileName));
        if (save is null ||
            save.Columns != LifeBoard.Columns ||
            save.Rows != LifeBoard.Rows ||
            save.Generation < 0 ||
            save.Cells is null ||
            save.Cells.Length != LifeBoard.Columns * LifeBoard.Rows)
        {
          ShowGameSaveError("Ce fichier ne contient pas une partie valide pour cette grille.");
          return;
        }

        var loadedCells = new bool[LifeBoard.Columns, LifeBoard.Rows];
        for (var y = 0; y < LifeBoard.Rows; y++)
        {
          for (var x = 0; x < LifeBoard.Columns; x++)
          {
            loadedCells[x, y] = save.Cells[y * LifeBoard.Columns + x];
          }
        }

        _timer.Stop();
        PlayButton.Content = "Démarrer";
        _cells = loadedCells;
        _generation = save.Generation;
        ResetCycleDetection();
        RefreshBoard();
      }
      catch (JsonException)
      {
        ShowGameSaveError("Le fichier choisi est illisible ou son format JSON est invalide.");
      }
      catch (IOException)
      {
        ShowGameSaveError("Le fichier choisi n'a pas pu être lu.");
      }
      catch (UnauthorizedAccessException)
      {
        ShowGameSaveError("L'accès au fichier choisi a été refusé.");
      }
    }

    private void ShowGameSaveError(string message)
    {
      MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private sealed record GameSave(int Columns, int Rows, int Generation, int Population, bool[] Cells);

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
      if (_timer.IsEnabled)
      {
        _timer.Stop();
        PlayButton.Content = "Démarrer";
      }
      else
      {
        ResetCycleDetection();
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
      ResetCycleDetection();
      RefreshBoard();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
      _timer.Stop();
      PlayButton.Content = "Démarrer";
      _cells = new bool[LifeBoard.Columns, LifeBoard.Rows];
      _generation = 0;
      ResetCycleDetection();
      RefreshBoard();
    }

    private void OnCellClicked(int x, int y)
    {
      _cells[x, y] = !_cells[x, y];
      ResetCycleDetection();
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

      if (_timer.IsEnabled && HasRepeatedState())
      {
        _timer.Stop();
        PlayButton.Content = "Démarrer";
      }
    }

    private void ResetCycleDetection()
    {
      _seenStates.Clear();
      _stateHistory.Clear();
      RememberCurrentState();
    }

    private bool HasRepeatedState()
    {
      var state = CreateStateKey();
      if (_seenStates.Contains(state))
      {
        return true;
      }

      _seenStates.Add(state);
      _stateHistory.Enqueue(state);
      if (_stateHistory.Count > MaxRememberedStates)
      {
        _seenStates.Remove(_stateHistory.Dequeue());
      }

      return false;
    }

    private void RememberCurrentState()
    {
      var state = CreateStateKey();
      _seenStates.Add(state);
      _stateHistory.Enqueue(state);
    }

    private string CreateStateKey()
    {
      var state = new byte[(LifeBoard.Columns * LifeBoard.Rows + 7) / 8];
      for (var y = 0; y < LifeBoard.Rows; y++)
      {
        for (var x = 0; x < LifeBoard.Columns; x++)
        {
          if (_cells[x, y])
          {
            var index = y * LifeBoard.Columns + x;
            state[index / 8] |= (byte)(1 << (index % 8));
          }
        }
      }

      return Convert.ToBase64String(state);
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
      StatsText.Text = $"Génération {_generation}   ·   {CountLiveCells()} cellules vivantes";
    }

    private int CountLiveCells()
    {
      var count = 0;
      foreach (var alive in _cells)
      {
        if (alive)
        {
          count++;
        }
      }

      return count;
    }

    private void UpdateSpeed()
    {
      var generationsPerSecond = (int)SpeedSlider.Value;
      _timer.Interval = TimeSpan.FromMilliseconds(1000d / generationsPerSecond);
      SpeedText.Text = $"{generationsPerSecond} gen/s";
    }
  }
}
