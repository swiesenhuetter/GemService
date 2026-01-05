namespace GemService
{
    public class WorkerOptions
    {
        public int DelayMilliseconds { get; set; } = 1000;
        public string Message { get; set; } = string.Empty;
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public string EulithaFolder { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Eulitha");

        public string SecsGemConfigFile => "EulithaPhableX.xml";

        private string userHomeFolder => 
            Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        
        public string desktopFolder => 
            Path.Combine(userHomeFolder, "Desktop");

        public string recipeFolder => 
            Path.Combine(desktopFolder, "Recipes");

        public string batchFolder => 
            Path.Combine(recipeFolder, "Batches");



    }
}