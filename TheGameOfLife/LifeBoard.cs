using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TheGameOfLife
{
    public sealed class LifeBoard : FrameworkElement
    {
        public const int Columns = 60;
        public const int Rows = 35;

        private static readonly Brush BackgroundBrush = CreateBrush(Color.FromRgb(16, 29, 35));
        private static readonly Brush CellBrush = CreateBrush(Color.FromRgb(82, 220, 180));
        private static readonly Pen GridPen = CreatePen(Color.FromRgb(36, 57, 64));

        public bool[,] Cells { get; set; } = new bool[Columns, Rows];

        public event Action<int, int>? CellClicked;

        public LifeBoard()
        {
            Focusable = true;
            MouseLeftButtonDown += HandleMouseLeftButtonDown;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var cellWidth = ActualWidth / Columns;
            var cellHeight = ActualHeight / Rows;
            drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            for (var x = 0; x < Columns; x++)
            {
                for (var y = 0; y < Rows; y++)
                {
                    if (Cells[x, y])
                    {
                        drawingContext.DrawRectangle(
                            CellBrush,
                            null,
                            new Rect(x * cellWidth + 1, y * cellHeight + 1,
                                Math.Max(0, cellWidth - 2), Math.Max(0, cellHeight - 2)));
                    }
                }
            }

            for (var x = 0; x <= Columns; x++)
            {
                var position = x * cellWidth;
                drawingContext.DrawLine(GridPen, new Point(position, 0), new Point(position, ActualHeight));
            }

            for (var y = 0; y <= Rows; y++)
            {
                var position = y * cellHeight;
                drawingContext.DrawLine(GridPen, new Point(0, position), new Point(ActualWidth, position));
            }
        }

        private void HandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            var position = e.GetPosition(this);
            var x = (int)(position.X / (ActualWidth / Columns));
            var y = (int)(position.Y / (ActualHeight / Rows));

            if (x >= 0 && x < Columns && y >= 0 && y < Rows)
            {
                CellClicked?.Invoke(x, y);
                e.Handled = true;
            }
        }

        private static Brush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen CreatePen(Color color)
        {
            var pen = new Pen(CreateBrush(color), 1);
            pen.Freeze();
            return pen;
        }
    }
}
