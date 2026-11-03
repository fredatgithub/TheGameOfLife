using System.Windows;
using System.Windows.Threading;

namespace TheGameOfLife
{
  public partial class MainWindow: Window
  {
    private readonly DispatcherTimer _timer = new();
    private bool[,] _cells = new bool[LifeBoard.Columns, LifeBoard.Rows];
    private int _generation;

    public MainWindow()
    {
      InitializeComponent();

      Board.CellClicked += OnCellClicked;
      SpeedSlider.ValueChanged += (_, _) => UpdateSpeed();
      _timer.Tick += (_, _) => AdvanceGeneration();
      UpdateSpeed();
      UpdateStats();
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
