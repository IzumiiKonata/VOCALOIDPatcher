namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public readonly record struct Point(int X, int Y)
{
    public const int StartX = -192000;
    public const int EndX = 1073741823;

    public static Point StartPoint(int value = -100) => new(StartX, value);
    public static Point EndPoint(int value = -100) => new(EndX, value);

    public Point WithY(int y) => this with { Y = y };
    public Point WithX(int x) => this with { X = x };
}
