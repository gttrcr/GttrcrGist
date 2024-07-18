namespace GttrcrGist
{
    public static class MutexConsole
    {
        private static readonly Mutex ConsoleWriteMutex = new();
        private static List<int> Xs = [];
        private static int LineOffset = 0;
        public static void WriteLine(string text, int? y = null, ConsoleColor color = ConsoleColor.White, bool zeroLeft = false)
        {
            ConsoleWriteMutex.WaitOne();
            int left = 0;
            if (y != null)
            {
                if (y >= Xs.Count)
                    Xs.AddRange(Enumerable.Repeat(0, y.Value - Xs.Count + 1));

                if (zeroLeft)
                {
                    left = 0;
                    Xs[y.Value] = text.Length;
                }
                else
                {
                    left = Xs[y.Value];
                    Xs[y.Value] += text.Length;
                }
            }

            Console.SetCursorPosition(left, (y ?? 0) + (y == null ? LineOffset++ : LineOffset));
            ConsoleColor tmp = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = tmp;
            ConsoleWriteMutex.ReleaseMutex();
        }

        public static void Clear()
        {
            Xs = [];
            LineOffset = 0;
            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }
    }
}