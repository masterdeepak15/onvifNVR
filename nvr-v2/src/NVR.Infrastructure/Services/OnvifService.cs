using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NVR.Core.Entities;
using NVR.Core.Interfaces;

namespace NVR.Infrastructure.Services
{
    /// <summary>
    /// ONVIF implementation using WS-Discovery and SOAP
    /// Compatible with ONVIF Profile S, G cameras
    /// </summary>
    public class OnvifService : IOnvifService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OnvifService> _logger;

        // ONVIF XML Namespaces
        private static readonly XNamespace SOAP = "http://www.w3.org/2003/05/soap-envelope";
        private static readonly XNamespace ONVIF_DEVICE = "http://www.onvif.org/ver10/device/wsdl";
        private static readonly XNamespace ONVIF_MEDIA = "http://www.onvif.org/ver10/media/wsdl";
        private static readonly XNamespace ONVIF_PTZ = "http://www.onvif.org/ver20/ptz/wsdl";
        private static readonly XNamespace ONVIF_TT = "http://www.onvif.org/ver10/schema";
        private static readonly XNamespace ONVIF_IMAGING = "http://www.onvif.org/ver20/imaging/wsdl";
        private static readonly XNamespace WSA = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
        private static readonly XNamespace WSD = "http://schemas.xmlsoap.org/ws/2005/04/discovery";


        public OnvifService(IHttpClientFactory httpClientFactory, ILogger<OnvifService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ============================================================
        // WS-DISCOVERY
        // ============================================================
        public async Task<IEnumerable<OnvifDiscoveredDevice>> DiscoverDevicesAsync(int timeoutMs = 5000, CancellationToken ct = default)
        {
            var devices = new List<OnvifDiscoveredDevice>();
            try
            {
                // WS-Discovery multicast probe
                var probe = BuildWsDiscoveryProbe();
                using var udpClient = new System.Net.Sockets.UdpClient();
                udpClient.EnableBroadcast = true;
                var multicastEp = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("239.255.255.250"), 3702);
                var probeBytes = Encoding.UTF8.GetBytes(probe);
                await udpClient.SendAsync(probeBytes, probeBytes.Length, multicastEp);

                udpClient.Client.ReceiveTimeout = timeoutMs;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync();
                        var xml = Encoding.UTF8.GetString(result.Buffer);
                        var device = ParseDiscoveryResponse(xml, result.RemoteEndPoint);
                        if (device != null) devices.Add(device);
                    }
                    catch (System.Net.Sockets.SocketException) { break; }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "WS-Discovery failed"); }

            return devices.DistinctBy(d => d.IpAddress);
        }

        private string BuildWsDiscoveryProbe()
        {
            var msgId = $"uuid:{Guid.NewGuid()}";
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope""
                                xmlns:a=""http://schemas.xmlsoap.org/ws/2004/08/addressing""
                                xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
                      <s:Header>
                        <a:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
                        <a:MessageID>{msgId}</a:MessageID>
                        <a:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
                      </s:Header>
                      <s:Body>
                        <d:Probe>
                          <d:Types>dn:NetworkVideoTransmitter</d:Types>
                        </d:Probe>
                      </s:Body>
                    </s:Envelope>";
        }

        private OnvifDiscoveredDevice? ParseDiscoveryResponse(string xml, System.Net.IPEndPoint remoteEp)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var xAddrs = doc.Descendants(WSD + "XAddrs").FirstOrDefault()?.Value ?? string.Empty;
                var types = doc.Descendants(WSD + "Types").FirstOrDefault()?.Value ?? string.Empty;
                var scopes = doc.Descendants(WSD + "Scopes").FirstOrDefault()?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(xAddrs)) return null;

                var firstAddr = xAddrs.Split(' ').FirstOrDefault() ?? string.Empty;
                var uri = new Uri(firstAddr);
                return new OnvifDiscoveredDevice(firstAddr, uri.Host, uri.Port, types, scopes);
            }
            catch { return null; }
        }

        // ============================================================
        // DEVICE INFO
        // ============================================================
        public async Task<OnvifDeviceInfo> GetDeviceInfoAsync(Camera camera, CancellationToken ct = default)
        {
            var soap = BuildSoap(camera, ONVIF_DEVICE, "<tds:GetDeviceInformation/>");
            var response = await SendSoapAsync(camera.OnvifServiceUrl, soap, camera.Username, camera.Password, ct);
            return ParseDeviceInfo(response);
        }

        private OnvifDeviceInfo ParseDeviceInfo(string xml)
        {
            var doc = XDocument.Parse(xml);
            var ns = ONVIF_TT;
            string Get(string name) => doc.Descendants(ONVIF_DEVICE + name).FirstOrDefault()?.Value ?? string.Empty;
            return new OnvifDeviceInfo(Get("Manufacturer"), Get("Model"), Get("FirmwareVersion"), Get("SerialNumber"), Get("HardwareId"));
        }

        // ============================================================
        // MEDIA PROFILES
        // ============================================================
        public async Task<IEnumerable<OnvifProfile>> GetProfilesAsync(Camera camera, CancellationToken ct = default)
        {
            var mediaUrl = await GetMediaServiceUrlAsync(camera, ct);
            var soap = BuildSoap(camera, ONVIF_MEDIA, "<trt:GetProfiles/>");
            var response = await SendSoapAsync(mediaUrl, soap, camera.Username, camera.Password, ct);
            return ParseProfiles(response);
        }

        private IEnumerable<OnvifProfile> ParseProfiles(string xml)
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants(ONVIF_MEDIA + "Profiles").Select(p =>
            {
                var token = p.Attribute(ONVIF_TT + "token")?.Value ?? p.Attribute("token")?.Value ?? string.Empty;
                var name = p.Element(ONVIF_TT + "Name")?.Value ?? string.Empty;
                var vsc = p.Descendants(ONVIF_TT + "VideoEncoderConfiguration").FirstOrDefault();
                var enc = vsc?.Element(ONVIF_TT + "Encoding")?.Value ?? "H264";
                var res = vsc?.Element(ONVIF_TT + "Resolution");
                int.TryParse(res?.Element(ONVIF_TT + "Width")?.Value, out int w);
                int.TryParse(res?.Element(ONVIF_TT + "Height")?.Value, out int h);
                var fps = vsc?.Descendants(ONVIF_TT + "FrameRateLimit").FirstOrDefault();
                int.TryParse(fps?.Value, out int fr);
                return new OnvifProfile(token, name, enc, w, h, fr);
            });
        }

        // ============================================================
        // RTSP STREAM URI
        // ============================================================
        public async Task<string> GetRtspStreamUriAsync(Camera camera, string profileToken, CancellationToken ct = default)
        {
            var mediaUrl = await GetMediaServiceUrlAsync(camera, ct);
            var body = $@"<trt:GetStreamUri>
                          <trt:StreamSetup>
                            <tt:Stream>RTP-Unicast</tt:Stream>
                            <tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport>
                          </trt:StreamSetup>
                          <trt:ProfileToken>{profileToken}</trt:ProfileToken>
                        </trt:GetStreamUri>";

            var soap = BuildSoap(camera, ONVIF_MEDIA, body);
            var response = await SendSoapAsync(mediaUrl, soap, camera.Username, camera.Password, ct);

            var doc = XDocument.Parse(response);
            var uri = doc.Descendants(ONVIF_TT + "Uri").FirstOrDefault()?.Value ?? string.Empty;

            // Inject credentials into RTSP URL
            if (!string.IsNullOrEmpty(uri) && !string.IsNullOrEmpty(camera.Username))
            {
                var uriObj = new Uri(uri);
                uri = $"rtsp://{Uri.EscapeDataString(camera.Username)}:{Uri.EscapeDataString(camera.Password)}@{uriObj.Host}:{uriObj.Port}{uriObj.PathAndQuery}";
            }
            return uri;
        }

        // ============================================================
        // PTZ OPERATIONS
        // ============================================================
        public async Task<OnvifPtzStatus> GetPtzStatusAsync(Camera camera, CancellationToken ct = default)
        {
            var ptzUrl = await GetPtzServiceUrlAsync(camera, ct);
            var profiles = await GetProfilesAsync(camera, ct);
            var token = profiles.FirstOrDefault()?.Token ?? string.Empty;
            var body = $"<tptz:GetStatus><tptz:ProfileToken>{token}</tptz:ProfileToken></tptz:GetStatus>";
            var soap = BuildSoap(camera, ONVIF_PTZ, body);
            var response = await SendSoapAsync(ptzUrl, soap, camera.Username, camera.Password, ct);
            return ParsePtzStatus(response);
        }

        public async Task PtzAbsoluteMoveAsync(Camera camera, float pan, float tilt, float zoom, CancellationToken ct = default)
        {
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $@"<tptz:AbsoluteMove>
                          <tptz:ProfileToken>{token}</tptz:ProfileToken>
                          <tptz:Position>
                            <tt:PanTilt x=""{pan}"" y=""{tilt}""/>
                            <tt:Zoom x=""{zoom}""/>
                          </tptz:Position>
                        </tptz:AbsoluteMove>";
            await SendPtzCommandAsync(camera, body, ct);
        }

        public async Task PtzRelativeMoveAsync(Camera camera, float panDelta, float tiltDelta, float zoomDelta, CancellationToken ct = default)
        {
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $@"<tptz:RelativeMove>
                          <tptz:ProfileToken>{token}</tptz:ProfileToken>
                          <tptz:Translation>
                            <tt:PanTilt x=""{panDelta}"" y=""{tiltDelta}""/>
                            <tt:Zoom x=""{zoomDelta}""/>
                          </tptz:Translation>
                        </tptz:RelativeMove>";
            await SendPtzCommandAsync(camera, body, ct);
        }

        public async Task PtzContinuousMoveAsync(Camera camera, float panSpeed, float tiltSpeed, float zoomSpeed, CancellationToken ct = default)
        {
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $@"<tptz:ContinuousMove>
                          <tptz:ProfileToken>{token}</tptz:ProfileToken>
                          <tptz:Velocity>
                            <tt:PanTilt x=""{panSpeed}"" y=""{tiltSpeed}""/>
                            <tt:Zoom x=""{zoomSpeed}""/>
                          </tptz:Velocity>
                        </tptz:ContinuousMove>";
            await SendPtzCommandAsync(camera, body, ct);
        }

        public async Task PtzStopAsync(Camera camera, CancellationToken ct = default)
        {
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $@"<tptz:Stop>
                          <tptz:ProfileToken>{token}</tptz:ProfileToken>
                          <tptz:PanTilt>true</tptz:PanTilt>
                          <tptz:Zoom>true</tptz:Zoom>
                        </tptz:Stop>";
            await SendPtzCommandAsync(camera, body, ct);
        }

        public async Task<IEnumerable<OnvifPtzPreset>> GetPresetsAsync(Camera camera, CancellationToken ct = default)
        {
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var ptzUrl = await GetPtzServiceUrlAsync(camera, ct);
            var body = $"<tptz:GetPresets><tptz:ProfileToken>{token}</tptz:ProfileToken></tptz:GetPresets>";
            var soap = BuildSoap(camera, ONVIF_PTZ, body);
            var response = await SendSoapAsync(ptzUrl, soap, camera.Username, camera.Password, ct);
            return ParsePresets(response);
        }

        public async Task GoToPresetAsync(Camera camera, string presetToken, CancellationToken ct = default)
        {
            var profileToken = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $@"<tptz:GotoPreset>
                          <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                          <tptz:PresetToken>{presetToken}</tptz:PresetToken>
                        </tptz:GotoPreset>";
            await SendPtzCommandAsync(camera, body, ct);
        }

        public async Task<string> SetPresetAsync(Camera camera, string presetName, CancellationToken ct = default)
        {
            var profileToken = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var ptzUrl = await GetPtzServiceUrlAsync(camera, ct);
            var body = $@"<tptz:SetPreset>
                          <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                          <tptz:PresetName>{presetName}</tptz:PresetName>
                        </tptz:SetPreset>";
            var soap = BuildSoap(camera, ONVIF_PTZ, body);
            var response = await SendSoapAsync(ptzUrl, soap, camera.Username, camera.Password, ct);
            var doc = XDocument.Parse(response);
            return doc.Descendants(ONVIF_PTZ + "PresetToken").FirstOrDefault()?.Value ?? string.Empty;
        }

        public async Task RemovePresetAsync(Camera camera, string presetToken, CancellationToken ct = default)
        {
            var profileToken = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var ptzUrl = await GetPtzServiceUrlAsync(camera, ct);
            var body = $@"<tptz:RemovePreset>
                          <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                          <tptz:PresetToken>{presetToken}</tptz:PresetToken>
                        </tptz:RemovePreset>";
            var soap = BuildSoap(camera, ONVIF_PTZ, body);
            await SendSoapAsync(ptzUrl, soap, camera.Username, camera.Password, ct);
        }

        public async Task<byte[]> GetSnapshotAsync(Camera camera, CancellationToken ct = default)
        {
            var mediaUrl = await GetMediaServiceUrlAsync(camera, ct);
            var token = (await GetProfilesAsync(camera, ct)).FirstOrDefault()?.Token ?? string.Empty;
            var body = $"<trt:GetSnapshotUri><trt:ProfileToken>{token}</trt:ProfileToken></trt:GetSnapshotUri>";
            var soap = BuildSoap(camera, ONVIF_MEDIA, body);
            var response = await SendSoapAsync(mediaUrl, soap, camera.Username, camera.Password, ct);
            var doc = XDocument.Parse(response);
            var snapshotUri = doc.Descendants(ONVIF_TT + "Uri").FirstOrDefault()?.Value ?? string.Empty;

            using var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, snapshotUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{camera.Username}:{camera.Password}")));
            var imgResponse = await client.SendAsync(request, ct);
            return await imgResponse.Content.ReadAsByteArrayAsync(ct);
        }

        public async Task<OnvifVideoConfig> GetVideoConfigAsync(Camera camera, string profileToken, CancellationToken ct = default)
        {
            var mediaUrl = await GetMediaServiceUrlAsync(camera, ct);
            var body = $"<trt:GetVideoEncoderConfiguration><trt:ConfigurationToken>{profileToken}</trt:ConfigurationToken></trt:GetVideoEncoderConfiguration>";
            var soap = BuildSoap(camera, ONVIF_MEDIA, body);
            var response = await SendSoapAsync(mediaUrl, soap, camera.Username, camera.Password, ct);
            var doc = XDocument.Parse(response);
            var cfg = doc.Descendants(ONVIF_TT + "VideoEncoderConfiguration").FirstOrDefault();
            var enc = cfg?.Element(ONVIF_TT + "Encoding")?.Value ?? "H264";
            var res = cfg?.Element(ONVIF_TT + "Resolution");
            int.TryParse(res?.Element(ONVIF_TT + "Width")?.Value, out int w);
            int.TryParse(res?.Element(ONVIF_TT + "Height")?.Value, out int h);
            int.TryParse(cfg?.Descendants(ONVIF_TT + "FrameRateLimit").FirstOrDefault()?.Value, out int fps);
            int.TryParse(cfg?.Descendants(ONVIF_TT + "BitrateLimit").FirstOrDefault()?.Value, out int bitrate);
            return new OnvifVideoConfig(w, h, fps, bitrate, enc);
        }

        public async Task<bool> PingAsync(string ipAddress, int port = 80, CancellationToken ct = default)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(ipAddress, port, ct);
                return true;
            }
            catch { return false; }
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private async Task<string> GetMediaServiceUrlAsync(Camera camera, CancellationToken ct)
        {
            // Try standard path, fall back to GetCapabilities
            var mediaUrl = $"http://{camera.IpAddress}:{camera.Port}/onvif/media";
            return mediaUrl;
        }

        private async Task<string> GetPtzServiceUrlAsync(Camera camera, CancellationToken ct)
        {
            return $"http://{camera.IpAddress}:{camera.Port}/onvif/ptz";
        }

        private async Task SendPtzCommandAsync(Camera camera, string body, CancellationToken ct)
        {
            var ptzUrl = await GetPtzServiceUrlAsync(camera, ct);
            var soap = BuildSoap(camera, ONVIF_PTZ, body);
            await SendSoapAsync(ptzUrl, soap, camera.Username, camera.Password, ct);
        }

        // ============================================================
        // IMAGING SERVICE — Focus & Iris
        // ONVIF Imaging WSDL: http://www.onvif.org/ver20/imaging/wsdl
        // Endpoint: same host as OnvifServiceUrl with path /onvif/imaging
        // ============================================================

        private static string GetImagingUrl(Camera camera)
        {
            var uri = new Uri(camera.OnvifServiceUrl);
            return $"{uri.Scheme}://{uri.Authority}/onvif/imaging";
        }

        private async Task<string> GetVideoSourceTokenAsync(Camera camera, CancellationToken ct)
        {
            var profiles = await GetProfilesAsync(camera, ct);
            var first = profiles.FirstOrDefault();
            if (first == null) throw new InvalidOperationException("No ONVIF profiles found on camera");
            return first.Token;
        }

        // ---- FOCUS ----

        public async Task FocusContinuousMoveAsync(Camera camera, float speed, CancellationToken ct = default)
        {
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            string body;
            if (Math.Abs(speed) < 0.001f)
            {
                body = $@"<timg:Stop xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                        </timg:Stop>";
            }
            else
            {
                body = $@"<timg:Move xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                          <timg:Focus>
                            <tt:Continuous xmlns:tt=""http://www.onvif.org/ver10/schema"">
                              <tt:Speed>{speed.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</tt:Speed>
                            </tt:Continuous>
                          </timg:Focus>
                        </timg:Move>";
            }
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            _logger.LogDebug("FocusContinuousMove: camera={CameraId} speed={Speed}", camera.Id, speed);
        }

        public async Task FocusAbsoluteMoveAsync(Camera camera, float position, float speed = 1.0f, CancellationToken ct = default)
        {
            position = Math.Clamp(position, 0.0f, 1.0f);
            speed = Math.Clamp(speed, 0.0f, 1.0f);
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var body = $@"<timg:Move xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                          <timg:Focus>
                            <tt:Absolute xmlns:tt=""http://www.onvif.org/ver10/schema"">
                              <tt:Position>{position.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</tt:Position>
                              <tt:Speed>{speed.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</tt:Speed>
                            </tt:Absolute>
                          </timg:Focus>
                        </timg:Move>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            _logger.LogDebug("FocusAbsoluteMove: camera={CameraId} position={Position}", camera.Id, position);
        }

        public async Task SetFocusModeAsync(Camera camera, FocusMode mode, CancellationToken ct = default)
        {
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var onvifMode = mode == FocusMode.Auto ? "AUTO" : "MANUAL";
            var body = $@"<timg:SetImagingSettings xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                          <timg:ImagingSettings>
                            <tt:Focus xmlns:tt=""http://www.onvif.org/ver10/schema"">
                              <tt:AutoFocusMode>{onvifMode}</tt:AutoFocusMode>
                            </tt:Focus>
                          </timg:ImagingSettings>
                          <timg:ForcePersistence>true</timg:ForcePersistence>
                        </timg:SetImagingSettings>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            _logger.LogDebug("SetFocusMode: camera={CameraId} mode={Mode}", camera.Id, mode);
        }

        public async Task<OnvifFocusStatus> GetFocusStatusAsync(Camera camera, CancellationToken ct = default)
        {
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var body = $@"<timg:GetStatus xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                        </timg:GetStatus>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            var response = await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            return ParseFocusStatus(response);
        }

        private OnvifFocusStatus ParseFocusStatus(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var focusStatus = doc.Descendants(ONVIF_TT + "FocusStatus20").FirstOrDefault()
                               ?? doc.Descendants(ONVIF_TT + "FocusStatus").FirstOrDefault();
                var posStr = focusStatus?.Element(ONVIF_TT + "Position")?.Value ?? "0";
                var moveStr = focusStatus?.Element(ONVIF_TT + "MoveStatus")?.Value ?? "IDLE";
                float.TryParse(posStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pos);
                var modeStr = doc.Descendants(ONVIF_TT + "AutoFocusMode").FirstOrDefault()?.Value ?? "AUTO";
                var focusMode = modeStr.Equals("MANUAL", StringComparison.OrdinalIgnoreCase)
                    ? FocusMode.Manual : FocusMode.Auto;
                return new OnvifFocusStatus(focusMode, pos, moveStr);
            }
            catch { return new OnvifFocusStatus(FocusMode.Auto, 0f, "UNKNOWN"); }
        }

        // ---- IRIS / EXPOSURE ----

        public async Task SetIrisAsync(Camera camera, float level, CancellationToken ct = default)
        {
            level = Math.Clamp(level, 0.0f, 1.0f);
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var body = $@"<timg:SetImagingSettings xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                          <timg:ImagingSettings>
                            <tt:Exposure xmlns:tt=""http://www.onvif.org/ver10/schema"">
                              <tt:Mode>MANUAL</tt:Mode>
                              <tt:Iris>{level.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</tt:Iris>
                            </tt:Exposure>
                          </timg:ImagingSettings>
                          <timg:ForcePersistence>false</timg:ForcePersistence>
                        </timg:SetImagingSettings>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            _logger.LogDebug("SetIris: camera={CameraId} level={Level}", camera.Id, level);
        }

        public async Task SetIrisModeAsync(Camera camera, IrisMode mode, CancellationToken ct = default)
        {
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var onvifMode = mode == IrisMode.Auto ? "AUTO" : "MANUAL";
            var body = $@"<timg:SetImagingSettings xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                          <timg:ImagingSettings>
                            <tt:Exposure xmlns:tt=""http://www.onvif.org/ver10/schema"">
                              <tt:Mode>{onvifMode}</tt:Mode>
                            </tt:Exposure>
                          </timg:ImagingSettings>
                          <timg:ForcePersistence>true</timg:ForcePersistence>
                        </timg:SetImagingSettings>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            _logger.LogDebug("SetIrisMode: camera={CameraId} mode={Mode}", camera.Id, mode);
        }

        public async Task<OnvifIrisStatus> GetIrisStatusAsync(Camera camera, CancellationToken ct = default)
        {
            var imgUrl = GetImagingUrl(camera);
            var sourceToken = await GetVideoSourceTokenAsync(camera, ct);
            var body = $@"<timg:GetImagingSettings xmlns:timg=""http://www.onvif.org/ver20/imaging/wsdl"">
                          <timg:VideoSourceToken>{sourceToken}</timg:VideoSourceToken>
                        </timg:GetImagingSettings>";
            var soap = BuildSoap(camera, ONVIF_IMAGING, body);
            var response = await SendSoapAsync(imgUrl, soap, camera.Username, camera.Password, ct);
            return ParseIrisStatus(response);
        }

        private OnvifIrisStatus ParseIrisStatus(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var exposure = doc.Descendants(ONVIF_TT + "Exposure").FirstOrDefault();
                var modeStr = exposure?.Element(ONVIF_TT + "Mode")?.Value ?? "AUTO";
                var irisStr = exposure?.Element(ONVIF_TT + "Iris")?.Value ?? "0";
                float.TryParse(irisStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float irisLevel);
                var irisMode = modeStr.Equals("MANUAL", StringComparison.OrdinalIgnoreCase)
                    ? IrisMode.Manual : IrisMode.Auto;
                return new OnvifIrisStatus(irisMode, irisLevel);
            }
            catch { return new OnvifIrisStatus(IrisMode.Auto, 0f); }
        }

        private string BuildSoap(Camera camera, XNamespace serviceNs, string body)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope""
                                xmlns:tds=""http://www.onvif.org/ver10/device/wsdl""
                                xmlns:trt=""http://www.onvif.org/ver10/media/wsdl""
                                xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl""
                                xmlns:tt=""http://www.onvif.org/ver10/schema"">
                      <s:Header>
                        <Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
                          {BuildWsseAuth(camera.Username, camera.Password)}
                        </Security>
                      </s:Header>
                      <s:Body>{body}</s:Body>
                    </s:Envelope>";
        }

        private string BuildWsseAuth(string username, string password)
        {
            var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            // Password digest: Base64(SHA1(nonce + created + password))
            var combined = Convert.FromBase64String(nonce).Concat(
                Encoding.UTF8.GetBytes(created)).Concat(Encoding.UTF8.GetBytes(password)).ToArray();
            var digest = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(combined));

            return $@"<UsernameToken>
                      <Username>{username}</Username>
                      <Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"">{digest}</Password>
                      <Nonce EncodingType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"">{nonce}</Nonce>
                      <Created xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">{created}</Created>
                    </UsernameToken>";
        }

        private async Task<string> SendSoapAsync(string url, string soap, string username, string password, CancellationToken ct)
        {
            using var client = _httpClientFactory.CreateClient("onvif");
            var content = new StringContent(soap, Encoding.UTF8, "application/soap+xml");
            var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private OnvifPtzStatus ParsePtzStatus(string xml)
        {
            var doc = XDocument.Parse(xml);
            var pos = doc.Descendants(ONVIF_TT + "Position").FirstOrDefault();
            var pt = pos?.Element(ONVIF_TT + "PanTilt");
            var zoom = pos?.Element(ONVIF_TT + "Zoom");
            float.TryParse(pt?.Attribute("x")?.Value, out float pan);
            float.TryParse(pt?.Attribute("y")?.Value, out float tilt);
            float.TryParse(zoom?.Attribute("x")?.Value, out float z);
            var moveStatus = doc.Descendants(ONVIF_TT + "MoveStatus").FirstOrDefault()
                ?.Element(ONVIF_TT + "PanTilt")?.Value ?? "Idle";
            return new OnvifPtzStatus(pan, tilt, z, moveStatus);
        }

        private IEnumerable<OnvifPtzPreset> ParsePresets(string xml)
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants(ONVIF_PTZ + "Preset").Select(p =>
            {
                var token = p.Attribute(ONVIF_TT + "token")?.Value ?? p.Attribute("token")?.Value ?? string.Empty;
                var name = p.Element(ONVIF_TT + "Name")?.Value ?? string.Empty;
                var pos = p.Element(ONVIF_TT + "PTZPosition");
                var pt = pos?.Element(ONVIF_TT + "PanTilt");
                var z = pos?.Element(ONVIF_TT + "Zoom");
                float? pan = null, tilt = null, zoom = null;
                if (float.TryParse(pt?.Attribute("x")?.Value, out var pv)) pan = pv;
                if (float.TryParse(pt?.Attribute("y")?.Value, out var tv)) tilt = tv;
                if (float.TryParse(z?.Attribute("x")?.Value, out var zv)) zoom = zv;
                return new OnvifPtzPreset(token, name, pan, tilt, zoom);
            });
        }
    }
}
