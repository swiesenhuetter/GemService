using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;

using Insphere.Connectivity.Application.SecsToHost;
using Insphere.Connectivity.Application.Common;
using Insphere.Connectivity.Application.MessageServices;
using Insphere.Connectivity.Common;
using Insphere.Connectivity.Application.Exceptions;

namespace GemService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _options;
        private GEMController _gem_ctrl;
        private TcpClient? _gemEquipmentClient;
        private MessageServiceManager? _serviceManager;
        private readonly HostCommandHandler _hostCommandHandler;
        private readonly MachineCommandHandler _machineCommandHandler;

        public Worker(ILogger<Worker> logger, IOptions<WorkerOptions> options, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _options = options.Value;
            _gem_ctrl = new GEMController();
            _hostCommandHandler = new HostCommandHandler();

            var machineLogger = loggerFactory.CreateLogger<MachineCommandHandler>();
            _machineCommandHandler = new MachineCommandHandler(machineLogger, gemController: _gem_ctrl);
            // Copy EulithaPhableX.xml to eulitha folder : C:\ProgramData\Eulitha
            CopyConfigurationFile();

            if (SynchronizationContext.Current != null)
            {
                // Synchronize the GUI Thread
                _gem_ctrl.UISynchronizationContext = SynchronizationContext.Current;
            }
            else
            {
                _gem_ctrl.UISynchronizationContext = new SynchronizationContext();
            }

            // Subscribe to the Communication state transition event
            _gem_ctrl.CommunicationStateChanged += OnCommunicationStateChanged;

            // Subscribe to the GEM control state transition event
            _gem_ctrl.ControlStateChanged += OnControlStateChanged;

            // Subscribe to the GEM Host Command S2F41.
            _gem_ctrl.HostCommandReceived += OnHostCommandReceived;


            _gem_ctrl.RemoteCommandReceived += OnRemoteCommandReceived;

            _gem_ctrl.PrimaryMessageIn += OnPrimaryMessage;

            _gem_ctrl.RecipeDirectoryRequested += OnRecipeDirectoryRequested;

            InitGemController();
        }

        private void InitGemController()
        {
            try
            {
                var cfg_file = Path.Combine(_options.EulithaFolder, _options.SecsGemConfigFile);
                _gem_ctrl.Initialize(cfg_file, @"C:\Temp");
                _serviceManager = _gem_ctrl.Services;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "GEM Controller initialization error: File not found");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GEM Controller initialization error");
                throw;
            }
          }

        private void OnPrimaryMessage(object sender, SECsPrimaryInEventArgs e)
        {             // Log the received primary message
            string message_id = $"S{e.Inputs.Stream}F{e.Inputs.Function}";
            _logger.LogInformation("Received Primary Message {id}: {message}", message_id, e.ToString());
        }

        private void OnRecipeDirectoryRequested(object sender, RecipeDirectoryEventArgs<List<string>> e)
        {
            string userHomeFolder = Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string desktopFolder = Path.Combine(userHomeFolder, "Desktop");
            string recipeFolder = Path.Combine(desktopFolder, "Recipes");
            // find all rcp files in folder
            string[] rcpFiles = Directory.GetFiles(recipeFolder, "*.rcp");
            var recipe_list = new List<string>(rcpFiles);
            _logger.LogInformation("Handling S7F19 message");
            e.SetReply(recipe_list);
        }



        private void OnCommunicationStateChanged(object sender, SECsEventArgs e)
        {
            // Update the txtCommunicationStatus with the latest Communication State
            string comm_state = _gem_ctrl.CommunicationState.ToString().ToUpper();
            _logger.LogInformation("received : {comm_state}", comm_state);
        }

        private void OnControlStateChanged(object sender, SECsEventArgs e)
        {
            // Update the txtControlState with the latest Control State
            string ctrl_state = _gem_ctrl.ControlState.ToString().ToUpper();
            _logger.LogInformation("received : {ctrl_state}", ctrl_state);
        }

        private void OnRemoteCommandReceived(object sender, RemoteCommandEventArgs<CMDA> e)
        {
            string cmd = e.LogicalName;

            // args is empthy dictionary for now 
            var empty_dict = new Dictionary<string, CommandParameterEx>();
            _hostCommandHandler.Dispatch(cmd, empty_dict);
            e.SetReply(CMDA.Accepted);
        }

        private void OnHostCommandReceived(Object sender, HostCommandEventArgs<HCACK> e)
        {
            string cmd = e.LogicalName;

            _hostCommandHandler.Dispatch(cmd, e.Parameters);

            _logger.LogInformation("received : {cmd}", cmd);
            e.SetReply(HCACK.Accepted);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

            // Start TCP  communication server
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
                    _gemEquipmentClient = client;  // Store the GemEquipment client
                    _hostCommandHandler.SetTcpClient(_gemEquipmentClient);
                    _logger.LogInformation("GemEquipment client connected");
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
                _gemEquipmentClient?.Close();
                _gemEquipmentClient = null;
            }
        }


        private void CopyConfigurationFile()
        {
            try
            {
                var sourceFile = Path.Combine(AppContext.BaseDirectory, _options.SecsGemConfigFile);
                var destinationFolder = _options.EulithaFolder;
                var destinationFile = Path.Combine(destinationFolder, _options.SecsGemConfigFile);

                // Create destination directory if it doesn't exist
                Directory.CreateDirectory(destinationFolder);

                // Copy file (overwrite if exists)
                File.Copy(sourceFile, destinationFile, overwrite: true);

                Console.WriteLine($"Successfully copied EulithaPhableX.xml to {destinationFolder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying configuration file: {ex.Message}");
                // Consider whether you want to throw or just log - depends on if this is critical
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
                    
                    _machineCommandHandler.Dispatch(msg);

                    char[] four_bytes = new char[] { 'A', 'C', 'K', '\0' };
                    await writer.WriteAsync(four_bytes, 0, 4);
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
                if (_gem_ctrl.CommunicationState == CommunicationState.Enabled)
                {
                    _gem_ctrl.SetDisable();
                }
                client.Close();
            }
        }
    }
}
