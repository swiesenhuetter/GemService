using Insphere.Connectivity.Application.Common;
using Insphere.Connectivity.Application.SecsToHost;
using Insphere.Connectivity.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;



namespace GemService
{
    internal class MachineCommandHandler
    {
        private GEMController _gem_ctrl;
        private readonly ILogger<MachineCommandHandler> _logger;

        public MachineCommandHandler(ILogger<MachineCommandHandler> logger, GEMController gemController)
        {
            _gem_ctrl = gemController;
            _logger = logger;
        }

        public void Dispatch(string json_command_txt)
        {
            using JsonDocument doc = JsonDocument.Parse(json_command_txt);
            JsonElement root = doc.RootElement;

            // Extract method name
            if (!root.TryGetProperty("method", out JsonElement methodElement))
            {
                _logger.LogError("Missing 'method' property in command: {json}", json_command_txt);
                return;
            }

            if (methodElement.ValueKind != JsonValueKind.String)
            {
                _logger.LogError("Invalid 'method' property type in command: {json}", json_command_txt);
                return;
            }

            string? methodName = methodElement.GetString();
            if (string.IsNullOrEmpty(methodName))
            {
                _logger.LogError("Invalid method name in command: {json}", json_command_txt);
                return;
            }
            else 
            {
                _logger.LogInformation("Dispatching command: {method}", methodName);
            }

            var my_type = this.GetType();
            var method = my_type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                // Extract parameters if they exist rest of the Dictionary are parameters
                // Get method parameters info
                var methodParams = method.GetParameters();
                var parameters = new object[methodParams.Length];

                // Extract all properties except "method" from JSON
                for (int i = 0; i < methodParams.Length; i++)
                {
                    var paramInfo = methodParams[i];
                    string paramName = paramInfo.Name ?? string.Empty;

                    // Try to find the parameter in the JSON
                    if (root.TryGetProperty(paramName, out JsonElement paramElement))
                    {
                        // Convert JsonElement to the expected parameter type
                        parameters[i] = ConvertJsonElementToType(paramElement, paramInfo.ParameterType);
                    }
                    else
                    {
                        // Use default value if parameter not found
                        if (paramInfo.HasDefaultValue)
                        {
                            parameters[i] = paramInfo.DefaultValue!;
                        }
                        else
                        {
                            _logger.LogError("Required parameter '{param}' not found for method '{method}'", paramName, methodName);
                            return;
                        }
                    }
                }

                method.Invoke(this, parameters);
            }
            else
            {
                _logger.LogWarning($"Unknown command {methodName} received");
            }

        }

        private void Connect(string device, string version)
        {
            _logger.LogInformation("Started Device {device} version:{version}", device, version);
            _gem_ctrl.SetEnable();
        }

        private void Disconnect()
        {
            _logger.LogInformation("Disconnected PhableX Device");
            _gem_ctrl.SetDisable();
        }

        private void LaserOn(bool on)
        {
            _logger.LogInformation("Laser On: {on}", on);
            string laser_txt = on ? "1" : "0";
            _gem_ctrl.SetAttribute("LaserOnIndicator", AttributeType.SV, laser_txt);
            if (on)
            {
                _gem_ctrl.SetAlarm("LaserOn");
            }
            else
            {
                _gem_ctrl.ClearAlarm("LaserOn");
            }
        }

        private void on_back_cassette_change(string cassette_id)
        {
            _logger.LogInformation($"Back Cassette Changed: {cassette_id}");
            _gem_ctrl.SetAttribute("BackCassetteID", AttributeType.DV, cassette_id);
            _gem_ctrl.SendCollectionEvent("BackCassetteLoaded");
        }

        private void on_front_cassette_change(string cassette_id)
        {
            _logger.LogInformation($"Front Cassette Changed: {cassette_id}");
            _gem_ctrl.SetAttribute("FrontCassetteID", AttributeType.DV, cassette_id);
            _gem_ctrl.SendCollectionEvent("FrontCassetteLoaded");
        }

        private void on_back_cassette_scanned(List<bool> wafer_list)
        {
            _logger.LogInformation("Back cassette scanned");
            var list_data = new SECsDataItem(SECsFormat.List);
            for (int i = 0; i < wafer_list.Count; i++)
            {
                bool wafer_present = wafer_list[i];
                list_data.Add($"Slot_{i + 1}", wafer_present, SECsFormat.Boolean);
            }
            _gem_ctrl.SetListAttribute("BackCassetteSlots", AttributeType.SV, list_data);
            _gem_ctrl.SendCollectionEvent("BackCassetteScanned");
        }

        private void on_front_cassette_scanned(List<Boolean> wafer_list)
        {
            _logger.LogInformation("Front cassette scanned");
            var list_data = new SECsDataItem(SECsFormat.List);
            for (int i = 0; i < wafer_list.Count; i++)
            {
                bool wafer_present = wafer_list[i];
                list_data.Add($"Slot_{i + 1}", wafer_present, SECsFormat.Boolean);
            }
            _gem_ctrl.SetListAttribute("FrontCassetteSlots", AttributeType.SV, list_data);
            _gem_ctrl.SendCollectionEvent("FrontCassetteScanned");
        }

        private void wafer_arrived_at_station(string station_id, string wafer_id,
            int origin_slot, int destination_slot)
        {
            if (origin_slot == 0)
            {
                _logger.LogInformation("Origin slot 0, initialization value");
            }
            else if (origin_slot < 0 || origin_slot > 25)
            {
                _logger.LogError("Invalid origin slot: {origin_slot}. Must be between 1 and 25.", origin_slot);
            }
            _logger.LogInformation("Wafer {wafer_id} arrived at station {station_id}", wafer_id, station_id);

            _gem_ctrl.SetAttribute("StationId", AttributeType.DV, station_id);
            _gem_ctrl.SetAttribute("ScannedWaferId", AttributeType.DV, wafer_id);
            _gem_ctrl.SetAttribute("OriginSlot", AttributeType.DV, origin_slot.ToString());
            _gem_ctrl.SetAttribute("DestinationSlot", AttributeType.DV, destination_slot.ToString());
            _gem_ctrl.SendCollectionEvent("WaferArrivedAtStation");
        }

        private void ReInitialize(string eapRemoteIp, int eapPort)
        {
            _gem_ctrl.SetDisable();

            if (!_gem_ctrl.IsInitialized)
            {
                _logger.LogError("GEM Controller is not initialized. Cannot reinitialize.");
                return;
            }

            _gem_ctrl.EquipmentModel.GemConnection.HSMS.remoteIPAddress = eapRemoteIp;
            _gem_ctrl.EquipmentModel.GemConnection.HSMS.remotePortNumber = eapPort;
            _gem_ctrl.EquipmentModel.GemConnection.HSMS.localIPAddress = eapRemoteIp;
            _gem_ctrl.EquipmentModel.GemConnection.HSMS.localPortNumber = eapPort;

            _gem_ctrl.SaveToolModel();
            _gem_ctrl.ReInitialize(@"C:\Temp");
            _gem_ctrl.SetEnable();

            _logger.LogInformation("GEM Controller reinitialized with IP:{eapRemoteIp} Port:{eapPort}", 
                eapRemoteIp, eapPort);
        }

        private object ConvertJsonElementToType(JsonElement element, Type targetType)
        {
            try
            {
                // Handle nullable types
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                return underlyingType.Name switch
                {
                    nameof(String) => element.GetString() ?? string.Empty,
                    nameof(Int32) => element.GetInt32(),
                    nameof(Int64) => element.GetInt64(),
                    nameof(Double) => element.GetDouble(),
                    nameof(Boolean) => element.GetBoolean(),
                    nameof(Decimal) => element.GetDecimal(),
                    nameof(DateTime) => element.GetDateTime(),
                    nameof(List<bool>) when element.ValueKind == JsonValueKind.Array =>
                        element.EnumerateArray().Select(e => e.GetBoolean()).ToList(),
                    _ => JsonSerializer.Deserialize(element.GetRawText(), targetType) ?? throw new InvalidOperationException($"Cannot convert to type {targetType.Name}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert JSON element to type {type}", targetType.Name);
                throw;
            }
        }

    }
}
