using Microsoft.Extensions.Options;
using System.Linq.Expressions;
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

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("{Message}: {time}", _options.Message, DateTimeOffset.Now);
                    }
                    await Task.Delay(_options.DelayMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown, not an error
            }

            _logger.LogInformation("Worker service stopping at: {time}", DateTimeOffset.Now);

            // Wait for TCP server to finish cleanup
            try
            {
                await tcpTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }


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
            catch (OperationCanceledException)
            {
                // Expected during shutdown - not an error
                _logger.LogInformation("TCP server shutting down gracefully");
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
            catch (OperationCanceledException)
            {
                // Expected during shutdown - not an error
                _logger.LogDebug("Client connection canceled during shutdown");
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
