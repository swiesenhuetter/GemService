namespace GemService
{
    public class WorkerOptions
    {
        public int DelayMilliseconds { get; set; } = 1000;
        public string Message { get; set; } = string.Empty;
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}