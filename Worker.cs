using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace GemService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _options;

        public Worker(ILogger<Worker> logger, IOptions<WorkerOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

            // Start TCP communication server
            var tcpTask = RunTcpServer(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("{Message}: {time}", _options.Message, DateTimeOffset.Now);
                }
                await Task.Delay(_options.DelayMilliseconds, stoppingToken);
            }
            
            _logger.LogInformation("Worker service stopping at: {time}", DateTimeOffset.Now);
        }

        private async Task RunTcpServer(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 8888);
            listener.Start();
            _logger.LogInformation("TCP server started on port 8888");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleTcpClient(client, stoppingToken); // Fire and forget
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCP server error");
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleTcpClient(TcpClient client, CancellationToken stoppingToken)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                string? message = await reader.ReadLineAsync(stoppingToken);
                if (message != null)
                {
                    _logger.LogInformation("Received TCP message: {message}", message);
                    await writer.WriteLineAsync($"ACK: {message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCP client handling error");
            }
            finally
            {
                client.Close();
            }
        }
    }
}
