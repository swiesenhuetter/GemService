using Insphere.Connectivity.Application.Common;
using Insphere.Connectivity.Common;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;


namespace GemService
{
    public class HostCommandHandler
    {
        private TcpClient? _tcpClient;
        private readonly ILogger<HostCommandHandler> _logger;

        public HostCommandHandler(ILogger<HostCommandHandler> logger)
        {
            _logger = logger;
        }

        public void SetTcpClient(TcpClient? client)
        {
            _tcpClient = client;
        }


        public void Dispatch(string commandName, Dictionary<string, CommandParameterEx> parameters)
        {
            var cs_params = TranslateParams(parameters);
            _ = SendCommandToGemEquipment(commandName, cs_params);
        }

        private async Task SendCommandToGemEquipment(string commandName, Dictionary<string, object> parameters)
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                Console.WriteLine($"GemEquipment not connected. Cannot send command: {commandName}");
                return;
            }

            try
            {
                var stream = _tcpClient.GetStream();

                // Create JSON command structure
                var commandObject = new Dictionary<string, object>
                {
                    { "command", commandName },
                    { "parameters", parameters },
                    { "timestamp", DateTime.UtcNow }
                };

                // Serialize to JSON
                string jsonCommand = JsonSerializer.Serialize(commandObject);
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(jsonCommand);

                // Send length prefix (4 bytes, big-endian)
                byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                }

                await stream.WriteAsync(lengthBytes);
                await stream.WriteAsync(messageBytes);
                await stream.FlushAsync();

                Console.WriteLine($"Sent command to GemEquipment: {jsonCommand}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command to GemEquipment: {ex.Message}");
                _tcpClient = null;
            }
        }

        private void Reset(Dictionary<string, object> parameters) 
        {
            _logger.LogInformation("Received Reset command from EAP host");
        }

        private void HomeAll(Dictionary<string, object> parameters) 
        {
            _logger.LogInformation("Received Homing command from EAP host");
        }

        private void StartBatch(string BatchId)
        {
            _logger.LogInformation($"Starting batch with ID: {BatchId}");
        }


        private Dictionary<string, object> TranslateParams(Dictionary<string, CommandParameterEx> secs_params)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in secs_params)
            {
                string k = kvp.Key;
                object v = SecsToNative(kvp.Value);
                result[k] = v;
            }
            return result;
        }


        private static object SecsToNative(CommandParameterEx param)
        {
            SECsFormat fmt = param.SecsFormat;
            object result = fmt switch
            {
                // ASCII / 
                SECsFormat.Ascii => param.GetValue<string>(),
                SECsFormat.U1 => param.GetValue<byte>(),
                SECsFormat.U4 => param.GetValue<UInt32>(),
                SECsFormat.I4 => param.GetValue<Int32>(),
                SECsFormat.Boolean => param.GetValue<bool>(),
                _ => throw new NotSupportedException($"SECS Format {fmt} is not supported.")
            };
            Console.WriteLine($"Converted parameter {param.Name} to native type: {result} ({result.GetType().Name})");
            return result;
        }

    }
}
