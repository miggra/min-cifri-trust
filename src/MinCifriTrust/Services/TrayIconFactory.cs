namespace MinCifriTrust.Services;

/// <summary>
/// Рисует простые иконки состояния без внешних PNG/ICO файлов.
/// </summary>
public static class TrayIconFactory
{
    public static Icon Create(bool enabled)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var fill = enabled
            ? Color.FromArgb(255, 34, 139, 34)
            : Color.FromArgb(255, 180, 40, 40);

        using (var brush = new SolidBrush(fill))
        {
            graphics.FillEllipse(brush, 2, 2, size - 5, size - 5);
        }

        using (var pen = new Pen(Color.White, 2.4f))
        {
            if (enabled)
            {
                // галочка
                graphics.DrawLines(pen, new[]
                {
                    new Point(9, 17),
                    new Point(14, 22),
                    new Point(23, 11)
                });
            }
            else
            {
                // крестик
                graphics.DrawLine(pen, 11, 11, 21, 21);
                graphics.DrawLine(pen, 21, 11, 11, 21);
            }
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
