class Program
{
    static void Main(string[] args)
    {
        try
        {
            var game = new Game();
            game.Run();
            game.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}