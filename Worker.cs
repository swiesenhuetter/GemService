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
                        _logger.LogDebug("{Message}: {time}", _options.Message, DateTimeOffset.Now);
                    }
                    await Task.Delay(_options.DelayMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker service stopping at: {time}", DateTimeOffset.Now);
                // Expected during shutdown, not an error
            }

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
            catch (EndOfStreamException)
            {
                _logger.LogError("Server: TCP client disconnected");
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

                while (client.Connected && !stoppingToken.IsCancellationRequested)
                {
                    var lengthBuffer = new byte[4];
                    await stream.ReadExactlyAsync(lengthBuffer, 0, 4, stoppingToken);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBuffer);
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    var msgBuffer = new byte[messageLength];
                    await stream.ReadExactlyAsync(msgBuffer, 0, messageLength, stoppingToken);
                    
                    string msg = System.Text.Encoding.UTF8.GetString(msgBuffer);

                    if (msg == null)
                    {
                        _logger.LogInformation("Client closed connection");
                        break;
                    }
                    _logger.LogInformation("Received TCP message: {msg}", msg);
                    // await writer.WriteLineAsync($"ACK: {msg}");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - not an error
                _logger.LogDebug("Client connection canceled during shutdown");
            }
            catch (EndOfStreamException) 
            {
                _logger.LogError("Handler: TCP client disconnected");
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
