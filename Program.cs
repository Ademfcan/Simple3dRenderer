using MathNet.Numerics;

class Program
{
    private const int WINDOW_WIDTH = 1920;
    private const int WINDOW_HEIGHT = 1080;
    static void Main(string[] args)
    {
        try
        {
            var game = new Game(WINDOW_WIDTH, WINDOW_HEIGHT, downScaleRes: 1, targetFps: 30);
            game.Run();
            game.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}