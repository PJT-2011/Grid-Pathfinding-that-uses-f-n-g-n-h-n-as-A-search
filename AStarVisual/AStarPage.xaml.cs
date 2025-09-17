using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AStarVisual;

public partial class AStarPage : ContentPage
{
    readonly GridModel _model;
    readonly GridDrawable _drawable;
    Mode _mode = Mode.Wall;
    bool _allowDiagonal = false;

    CancellationTokenSource? _cts;

    public AStarPage()
    {
        InitializeComponent();

        _model = new GridModel(rows: 22, cols: 34);
        _drawable = new GridDrawable(_model, () => chkScores.IsChecked);
        Canvas.Drawable = _drawable;

        // Touch/drag to edit the grid
        Canvas.StartInteraction += (s, e) => HandleTouch(e);
        Canvas.DragInteraction += (s, e) => HandleTouch(e);
        Canvas.EndInteraction += (s, e) => { };

        Randomize(0.22);
        _model.Walls[_model.Start.R, _model.Start.C] = false;
        _model.Walls[_model.Goal.R, _model.Goal.C] = false;
        Canvas.Invalidate();
    }

    void OnModeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        if (rbWall.IsChecked) _mode = Mode.Wall;
        else if (rbErase.IsChecked) _mode = Mode.Erase;
        else if (rbStart.IsChecked) _mode = Mode.Start;
        else if (rbGoal.IsChecked) _mode = Mode.Goal;
    }

    void OnDiagChanged(object sender, CheckedChangedEventArgs e) => _allowDiagonal = e.Value;

    void HandleTouch(TouchEventArgs e)
    {
        if (!e.Touches.Any()) return;          // fixes "method group vs int" Count error
        var p = e.Touches.First();
        var cell = CellFromPoint((float)p.X, (float)p.Y);
        if (cell is null) return;

        switch (_mode)
        {
            case Mode.Wall:
                if (cell != _model.Start && cell != _model.Goal)
                    _model.Walls[cell.Value.R, cell.Value.C] = true;
                break;
            case Mode.Erase:
                _model.Walls[cell.Value.R, cell.Value.C] = false;
                break;
            case Mode.Start:
                _model.Start = cell.Value;
                _model.Walls[_model.Start.R, _model.Start.C] = false;
                break;
            case Mode.Goal:
                _model.Goal = cell.Value;
                _model.Walls[_model.Goal.R, _model.Goal.C] = false;
                break;
        }

        _model.Path.Clear();
        _model.Open.Clear();
        _model.Closed.Clear();
        lblInfo.Text = "";
        Canvas.Invalidate();
    }

    async void OnRun(object sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _model.Path.Clear();
        _model.Open.Clear();
        _model.Closed.Clear();
        lblInfo.Text = "";
        Canvas.Invalidate();

        int delay = (int)speedSlider.Value; // 0 = instant
        bool animate = chkAnimate.IsChecked;

        try
        {
            var result = await AStarAnimator.RunAsync(
                _model, _allowDiagonal, animate, delay,
                redraw: () => MainThread.BeginInvokeOnMainThread(Canvas.Invalidate),
                token: _cts.Token);

            if (result is null)
                await DisplayAlert("A*", "No path found.", "OK");
            else
                lblInfo.Text = $"Steps: {result.Count - 1}   g(n): {AStarAnimator.LastCost:0.###}   expanded: {_model.Closed.Count}";
        }
        catch (OperationCanceledException)
        {
            lblInfo.Text = "Stopped.";
        }
    }

    void OnStop(object sender, EventArgs e) => _cts?.Cancel();

    void OnClear(object sender, EventArgs e)
    {
        _cts?.Cancel();
        _model.ClearWalls();
        _model.Path.Clear();
        _model.Open.Clear();
        _model.Closed.Clear();
        lblInfo.Text = "";
        Canvas.Invalidate();
    }

    void OnRandom(object sender, EventArgs e)
    {
        _cts?.Cancel();
        Randomize(0.28);
        Canvas.Invalidate();
    }

    void Randomize(double density)
    {
        var rnd = new Random();
        for (int r = 0; r < _model.Rows; r++)
            for (int c = 0; c < _model.Cols; c++)
                _model.Walls[r, c] = rnd.NextDouble() < density;

        _model.Start = new Cell(0, 0);
        _model.Goal = new Cell(_model.Rows - 1, _model.Cols - 1);
        _model.Walls[_model.Start.R, _model.Start.C] = false;
        _model.Walls[_model.Goal.R, _model.Goal.C] = false;
        _model.Path.Clear();
        _model.Open.Clear();
        _model.Closed.Clear();
        lblInfo.Text = "";
    }

    Cell? CellFromPoint(float x, float y)
    {
        if (Canvas.Width <= 0 || Canvas.Height <= 0) return null;
        int c = (int)(x / (Canvas.Width / _model.Cols));
        int r = (int)(y / (Canvas.Height / _model.Rows));
        if (r < 0 || r >= _model.Rows || c < 0 || c >= _model.Cols) return null;
        return new Cell(r, c);
    }

    enum Mode { Wall, Erase, Start, Goal }
}

/* =================== Model =================== */
public readonly record struct Cell(int R, int C);

public class GridModel
{
    public int Rows { get; }
    public int Cols { get; }
    public bool[,] Walls { get; }
    public Cell Start { get; set; }
    public Cell Goal { get; set; }

    // For drawing
    public HashSet<Cell> Path { get; set; } = new();
    public HashSet<Cell> Open { get; set; } = new();
    public HashSet<Cell> Closed { get; set; } = new();

    // Optional: store last-known f/g/h to draw
    public Dictionary<Cell, (double g, double h, double f)> Scores { get; } = new();

    public GridModel(int rows, int cols)
    {
        Rows = rows; Cols = cols;
        Walls = new bool[rows, cols];
        Start = new Cell(0, 0);
        Goal = new Cell(rows - 1, cols - 1);
    }

    public bool InBounds(Cell p) => p.R >= 0 && p.C >= 0 && p.R < Rows && p.C < Cols;
    public bool Passable(Cell p) => !Walls[p.R, p.C];

    public void ClearWalls()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                Walls[r, c] = false;
    }
}

/* =================== Animated A* =================== */
public static class AStarAnimator
{
    public static double LastCost { get; private set; } = 0;

    public static async Task<List<Cell>?> RunAsync(
        GridModel m,
        bool diagonal,
        bool animate,
        int delayMs,
        Action redraw,
        CancellationToken token)
    {
        var dirs4 = new Cell[] { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
        var dirs8 = new Cell[] { new(-1,0), new(1,0), new(0,-1), new(0,1),
                                 new(-1,-1), new(-1,1), new(1,-1), new(1,1) };
        var dirs = diagonal ? dirs8 : dirs4;

        double StepCost(Cell a, Cell b)
            => (a.R != b.R && a.C != b.C) ? Math.Sqrt(2) : 1.0;

        double Heuristic(Cell a, Cell b)
        {
            int dx = Math.Abs(a.R - b.R), dy = Math.Abs(a.C - b.C);
            if (diagonal)
                return (dx + dy) + (Math.Sqrt(2) - 2) * Math.Min(dx, dy); // octile
            return dx + dy; // manhattan
        }

        var openPQ = new PriorityQueue<Cell, double>();
        var g = new Dictionary<Cell, double> { [m.Start] = 0.0 };
        var came = new Dictionary<Cell, Cell>();

        m.Open.Clear(); m.Closed.Clear(); m.Path.Clear(); m.Scores.Clear();

        openPQ.Enqueue(m.Start, Heuristic(m.Start, m.Goal));
        m.Open.Add(m.Start);
        m.Scores[m.Start] = (0, Heuristic(m.Start, m.Goal), Heuristic(m.Start, m.Goal));
        redraw();

        while (openPQ.TryDequeue(out var current, out _))
        {
            token.ThrowIfCancellationRequested();

            if (m.Open.Contains(current)) m.Open.Remove(current);
            m.Closed.Add(current);

            if (current.Equals(m.Goal))
            {
                LastCost = g[current];
                var path = Reconstruct(came, current);
                m.Path = new HashSet<Cell>(path);
                redraw();
                return path;
            }

            foreach (var d in dirs)
            {
                var nb = new Cell(current.R + d.R, current.C + d.C);
                if (!m.InBounds(nb) || !m.Passable(nb)) continue;

                var tentative = g[current] + StepCost(current, nb);
                if (!g.TryGetValue(nb, out var old) || tentative < old)
                {
                    g[nb] = tentative;
                    came[nb] = current;
                    var h = Heuristic(nb, m.Goal);
                    var f = tentative + h;

                    openPQ.Enqueue(nb, f);
                    if (!m.Closed.Contains(nb)) m.Open.Add(nb);
                    m.Scores[nb] = (tentative, h, f);
                }
            }

            if (animate && delayMs > 0)
            {
                redraw();
                await Task.Delay(delayMs, token);
            }
        }

        redraw();
        return null;
    }

    static List<Cell> Reconstruct(Dictionary<Cell, Cell> came, Cell cur)
    {
        var path = new List<Cell> { cur };
        while (came.ContainsKey(cur))
        {
            cur = came[cur];
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }
}

/* =================== Drawing =================== */
public class GridDrawable : IDrawable
{
    readonly GridModel _m;
    readonly Func<bool> _showScores;

    public GridDrawable(GridModel m, Func<bool> showScores)
    {
        _m = m;
        _showScores = showScores;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        float cw = rect.Width / _m.Cols;
        float ch = rect.Height / _m.Rows;

        // background
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(rect);

        for (int r = 0; r < _m.Rows; r++)
            for (int c = 0; c < _m.Cols; c++)
            {
                float x = rect.X + c * cw;
                float y = rect.Y + r * ch;
                var cell = new Cell(r, c);

                // base
                if (_m.Walls[r, c]) canvas.FillColor = Colors.DarkGray;        // wall
                else if (_m.Path.Contains(cell)) canvas.FillColor = Colors.Gold;  // final path
                else if (_m.Closed.Contains(cell)) canvas.FillColor = Colors.SlateGray.WithAlpha(0.35f); // explored
                else if (_m.Open.Contains(cell)) canvas.FillColor = Colors.LightSkyBlue.WithAlpha(0.45f); // frontier
                else canvas.FillColor = Colors.White;

                canvas.FillRectangle(x, y, cw, ch);

                // start/goal overlays
                if (cell == _m.Start)
                {
                    canvas.FillColor = Colors.ForestGreen;
                    canvas.FillRectangle(x, y, cw, ch);
                }
                if (cell == _m.Goal)
                {
                    canvas.FillColor = Colors.IndianRed;
                    canvas.FillRectangle(x, y, cw, ch);
                }

                // grid lines
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 1;
                canvas.DrawRectangle(x, y, cw, ch);

                // tiny f/g/h text
                if (_showScores() && _m.Scores.TryGetValue(cell, out var s))
                {
                    canvas.FontSize = MathF.Max(10f, MathF.Min(cw, ch) * 0.28f);
                    canvas.FontColor = Colors.Black;
                    var txt = $"f:{s.f:0.#}\ng:{s.g:0.#}\nh:{s.h:0.#}";
                    canvas.DrawString(txt, x + 2, y + 2, cw - 4, ch - 4,
                        HorizontalAlignment.Left, VerticalAlignment.Top);
                }
            }
    }
}
