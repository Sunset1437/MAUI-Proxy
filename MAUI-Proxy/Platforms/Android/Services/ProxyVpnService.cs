using Android.App;
using Android.Content;
using Android.Net;
using Android.Util;
using Android.OS;
using Android.Runtime;
using Java.Net;
using MAUI_Proxy.Models;
using MAUI_Proxy.Platforms.Android.Extensions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Android.Media;
using AndroidX.Core.App;
using Java.IO;
using Encoding = System.Text.Encoding;
using SocketException = System.Net.Sockets.SocketException;
using Socket = Java.Net.Socket;
using SocketType = System.Net.Sockets.SocketType;
using IOException = Java.IO.IOException;
using System.Text.RegularExpressions;
using Stream = System.IO.Stream;

namespace MAUI_Proxy.Platforms.Android.Services
{
    [Service(
        Name = "MAUI_Proxy.Platforms.Android.Services.ProxyVpnService",
        Exported = true,
        Permission = "android.permission.BIND_VPN_SERVICE"
    )]
    [IntentFilter(new[] { "android.net.VpnService" })]
    public class ProxyVpnService : VpnService
    {
        private const string TAG = "ProxyVpnService";
        private const string VPN_INTERFACE_NAME = "ProxyVPN";
        private const int MTU = 1500;
        private const string CHANNEL_ID = "vpn_channel";

        private ParcelFileDescriptor _vpnInterface;
        private Thread _proxyThread;
        private ProxySettings _settings;
        private bool _isRunning;
        private Java.Net.Socket _proxySocket;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Extras == null)
            {
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            _settings = ProxySettings.FromBundle(intent.Extras);

            // Создаем уведомление для Foreground Service
            CreateNotificationChannel();
            var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("VPN Proxy Service")
                .SetContentText($"Connected to {_settings.IpAddress}:{_settings.Port}")
                .SetSmallIcon(Resource.Drawable.ic_vpn_notification)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build();

            StartForeground(1, notification);

            // Настраиваем VPN
            try
            {
                var builder = new Builder(this)
                    .SetSession(VPN_INTERFACE_NAME)
                    .SetMtu(MTU)
                    .AddAddress("10.8.0.2", 24)
                    .AddRoute("0.0.0.0", 0) // Весь трафик через VPN
                    .AddDnsServer("8.8.8.8");

                foreach (var app in _settings.BypassList)
                    builder.AddAllowedApplication(app);

                _vpnInterface = builder.Establish();
                _isRunning = true;

                _proxyThread = new Thread(() => RunProxy(_settings))
                {
                    IsBackground = true
                };
                _proxyThread.Start();

                return StartCommandResult.Sticky;
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Failed to start VPN: {ex}");
                StopSelf();
                return StartCommandResult.NotSticky;
            }
        }

        private async Task RunProxy(ProxySettings settings)
        {
            FileInputStream vpnInStream = null;
            FileOutputStream vpnOutStream = null;
            Java.Net.Socket proxySocket = null;
            byte[] buffer = new byte[MTU];

            try
            {
                // 1. Инициализация VPN потоков
                Log.Debug(TAG, "Инициализация VPN потоков");
                vpnInStream = new FileInputStream(_vpnInterface.FileDescriptor);
                vpnOutStream = new FileOutputStream(_vpnInterface.FileDescriptor);

                // 2. Подключение к прокси
                Log.Debug(TAG, $"Попытка подключения к прокси {settings.IpAddress}:{settings.Port}");
                proxySocket = new Java.Net.Socket();


                await proxySocket.ConnectAsync(new InetSocketAddress(settings.IpAddress, settings.Port), 15000);

                if (!proxySocket.IsConnected)
                {
                    Log.Error(TAG, "Не удалось установить соединение с прокси");
                    throw new IOException("Proxy connection failed");
                }

                Log.Debug(TAG, "Успешное подключение к прокси-серверу");

                // 3. Основной цикл обработки пакетов
                Log.Debug(TAG, "Вход в основной цикл обработки");
                while (_isRunning)
                {
                    try
                    {
                        // Исправленное чтение с преобразованием ValueTask в Task
                        var readTask = vpnInStream.ReadAsync(buffer);
                        if (await Task.WhenAny(readTask, Task.Delay(5000)) != readTask)
                        {
                            Log.Debug(TAG, "Таймаут чтения из VPN, проверка соединения");
                            continue;
                        }

                        int bytesRead = await readTask;
                        if (bytesRead <= 0)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        // Анализ пакета
                        var packetInfo = ParsePacket(buffer, bytesRead);
                        if (packetInfo == null)
                        {
                            Log.Debug(TAG, "Не удалось распознать пакет");
                            continue;
                        }

                        // Пропускаем DNS-запросы
                        if (packetInfo.IsDnsQuery)
                        {
                            Log.Debug(TAG, $"Пропуск DNS-запроса для {packetInfo.TargetHost}");
                            continue;
                        }

                        Log.Debug(TAG, $"Обработка пакета для {packetInfo.TargetHost}:{packetInfo.TargetPort}");

                        // Аутентификация при необходимости
                        if (!string.IsNullOrEmpty(settings.Username))
                        {
                            await PerformProxyAuthentication(proxySocket, settings, packetInfo);
                        }

                        // Отправка данных в прокси
                        await proxySocket.OutputStream.WriteAsync(buffer, 0, bytesRead);
                        await proxySocket.OutputStream.FlushAsync();

                        // Исправленное чтение ответа
                        var receiveTask = proxySocket.InputStream.ReadAsync(buffer).AsTask();
                        if (await Task.WhenAny(receiveTask, Task.Delay(10000)) != receiveTask)
                        {
                            Log.Warn(TAG, "Таймаут получения ответа от прокси");
                            continue;
                        }

                        int received = await receiveTask;
                        if (received > 0)
                        {
                            await vpnOutStream.WriteAsync(buffer, 0, received);
                        }
                    }
                    catch (SocketTimeoutException)
                    {
                        Log.Debug(TAG, "Таймаут операции, продолжение работы");
                        continue;
                    }
                    catch (Java.IO.IOException ioEx)
                    {
                        Log.Error(TAG, $"IO Ошибка: {ioEx}");
                        await ReconnectToProxy(settings, proxySocket);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(TAG, $"Ошибка в основном цикле: {ex}");
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception mainEx)
            {
                Log.Error(TAG, $"Критическая ошибка: {mainEx}");
            }
            finally
            {
                Log.Debug(TAG, "Завершение работы, освобождение ресурсов");
                try { proxySocket?.Close(); } catch { }
                try { vpnInStream?.Close(); } catch { }
                try { vpnOutStream?.Close(); } catch { }

                if (_isRunning)
                {
                    StopSelf();
                }
            }
        }

        private async Task ReconnectToProxy(ProxySettings settings, Java.Net.Socket proxySocket)
        {
            try
            {
                Log.Debug(TAG, "Попытка переподключения к прокси");
                proxySocket?.Close();
                proxySocket = new Java.Net.Socket();

                await proxySocket.ConnectAsync(new InetSocketAddress(settings.IpAddress, settings.Port), 15000);

                if (!proxySocket.IsConnected)
                {
                    throw new IOException("Не удалось переподключиться к прокси");
                }

                Log.Debug(TAG, "Переподключение к прокси успешно");
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Ошибка переподключения: {ex}");
                await Task.Delay(5000);
                throw;
            }
        }

        private async Task PerformProxyAuthentication(Java.Net.Socket socket, ProxySettings settings, PacketInfo packetInfo)
        {
            try
            {
                string authStr = $"{settings.Username}:{settings.Password}";
                string authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes(authStr));

                string authRequest = $"CONNECT {packetInfo.TargetHost}:{packetInfo.TargetPort} HTTP/1.1\r\n" +
                                   $"Host: {packetInfo.TargetHost}:{packetInfo.TargetPort}\r\n" +
                                   $"Proxy-Authorization: Basic {authHeader}\r\n" +
                                   $"User-Agent: MyProxyClient/1.0\r\n" +
                                   $"Connection: keep-alive\r\n" +
                                   "\r\n";

                byte[] requestBytes = Encoding.ASCII.GetBytes(authRequest);
                await socket.OutputStream.WriteAsync(requestBytes);
                await socket.OutputStream.FlushAsync();

                var response = await ReadProxyResponse(socket.InputStream);
                if (!response.StartsWith("HTTP/1.1 200") && !response.StartsWith("HTTP/1.0 200"))
                {
                    throw new Exception($"Ошибка аутентификации: {response}");
                }

                Log.Debug(TAG, "Аутентификация успешна");
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Ошибка аутентификации: {ex}");
                throw;
            }
        }

        private async Task<string> ReadProxyResponse(Stream inputStream)
        {
            using var memoryStream = new MemoryStream();
            byte[] tempBuffer = new byte[1024];
            int totalRead = 0;

            while (totalRead < 4096) // Максимальный размер ответа
            {
                int bytesRead = await inputStream.ReadAsync(tempBuffer, 0, tempBuffer.Length);
                if (bytesRead <= 0) break;

                await memoryStream.WriteAsync(tempBuffer, 0, bytesRead);
                totalRead += bytesRead;

                var response = Encoding.ASCII.GetString(memoryStream.ToArray());
                if (response.Contains("\r\n\r\n"))
                {
                    return response;
                }
            }

            return Encoding.ASCII.GetString(memoryStream.ToArray());
        }

        private class PacketInfo
        {
            public bool IsDnsQuery { get; set; }
            public string TargetHost { get; set; }
            public int TargetPort { get; set; }
        }

        private PacketInfo ParsePacket(byte[] buffer, int length)
        {
            if (length < 20) return null; // Минимальный размер IP заголовка

            try
            {
                // 1. Анализ IP заголовка
                int ipHeaderLength = (buffer[0] & 0x0F) * 4; // Длина IP заголовка в байтах
                byte protocol = buffer[9]; // Протокол (6 = TCP, 17 = UDP)

                // 2. Для TCP пакетов (например, HTTP/HTTPS)
                if (protocol == 6 && length >= ipHeaderLength + 20) // 20 - минимальный TCP заголовок
                {
                    int tcpHeaderLength = ((buffer[ipHeaderLength + 12] >> 4) * 4); // Длина TCP заголовка
                    int payloadOffset = ipHeaderLength + tcpHeaderLength;

                    // Проверяем, есть ли полезная нагрузка
                    if (length > payloadOffset)
                    {
                        // Анализ HTTP/HTTPS (упрощенно)
                        string payload = Encoding.ASCII.GetString(buffer, payloadOffset, Math.Min(100, length - payloadOffset));

                        // Для HTTPS (TLS) - извлекаем SNI (Server Name Indication)
                        if (buffer[payloadOffset] == 0x16 && buffer[payloadOffset + 1] == 0x03)
                        {
                            // Это TLS Client Hello
                            string host = ExtractSniFromTls(buffer, payloadOffset, length - payloadOffset);
                            Log.Info("HOST: ",host);
                            if (!string.IsNullOrEmpty(host))
                            {
                                int destPort = (buffer[ipHeaderLength + 2] << 8) | buffer[ipHeaderLength + 3];
                                return new PacketInfo
                                {
                                    IsDnsQuery = false,
                                    TargetHost = host,
                                    TargetPort = destPort
                                };
                            }
                        }
                        // Для HTTP - извлекаем Host header
                        else if (payload.StartsWith("GET") || payload.StartsWith("POST") || payload.StartsWith("CONNECT"))
                        {
                            var hostHeader = Regex.Match(payload, @"Host:\s*([^\r\n]+)");
                            if (hostHeader.Success)
                            {
                                int destPort = (buffer[ipHeaderLength + 2] << 8) | buffer[ipHeaderLength + 3];
                                return new PacketInfo
                                {
                                    IsDnsQuery = false,
                                    TargetHost = hostHeader.Groups[1].Value.Trim(),
                                    TargetPort = destPort
                                };
                            }
                        }
                    }
                }
                // 3. Для UDP пакетов (DNS запросы)
                else if (protocol == 17 && length >= ipHeaderLength + 8)
                {
                    int destPort = (buffer[ipHeaderLength + 2] << 8) | buffer[ipHeaderLength + 3];
                    if (destPort == 53) // DNS порт
                    {
                        return new PacketInfo { IsDnsQuery = true };
                    }
                }

                // 4. Если не удалось распознать - возвращаем null
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Ошибка анализа пакета: {ex}");
                return null;
            }
        }

        private string ExtractSniFromTls(byte[] buffer, int offset, int length)
        {
            try
            {
                // TLS Client Hello должно быть достаточно длинным
                if (length < 40) return null;

                // Проверяем TLS версию (должно быть 0x0301, 0x0302 или 0x0303)
                if (buffer[offset + 1] != 0x03) return null;

                // Пропускаем фиксированные части заголовка
                int pos = offset + 5; // после TLS запись заголовка

                // Длина Client Hello
                int helloLength = (buffer[pos] << 16) | (buffer[pos + 1] << 8) | buffer[pos + 2];
                pos += 3;

                // Пропускаем Client Version, Random, Session ID
                pos += 2 + 32 + 1 + buffer[pos + 2 + 32];

                // Длина cipher suites
                int cipherSuitesLength = (buffer[pos] << 8) | buffer[pos + 1];
                pos += 2 + cipherSuitesLength;

                // Пропускаем compression methods
                pos += 1 + buffer[pos];

                // Extensions
                if (pos + 2 > offset + length) return null;
                int extensionsLength = (buffer[pos] << 8) | buffer[pos + 1];
                pos += 2;

                while (pos + 4 <= offset + length)
                {
                    int extensionType = (buffer[pos] << 8) | buffer[pos + 1];
                    int extensionLength = (buffer[pos + 2] << 8) | buffer[pos + 3];
                    pos += 4;

                    if (extensionType == 0x00) // SNI extension
                    {
                        if (pos + 2 > offset + length) return null;
                        int sniListLength = (buffer[pos] << 8) | buffer[pos + 1];
                        pos += 2;

                        if (pos + 3 > offset + length) return null;
                        int sniType = buffer[pos];
                        int sniNameLength = (buffer[pos + 1] << 8) | buffer[pos + 2];
                        pos += 3;

                        if (sniType == 0 && sniNameLength > 0 && pos + sniNameLength <= offset + length)
                        {
                            return Encoding.ASCII.GetString(buffer, pos, sniNameLength);
                        }
                        return null;
                    }

                    pos += extensionLength;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var channel = new NotificationChannel(
                CHANNEL_ID,
                "VPN Proxy Service",
                NotificationImportance.Default
            );

            var manager = GetSystemService(NotificationService) as NotificationManager;
            manager?.CreateNotificationChannel(channel);
        }

        public override void OnDestroy()
        {
            _isRunning = false;
            _proxyThread?.Interrupt();
            _vpnInterface?.Close();
            _proxySocket?.Close();
            base.OnDestroy();
        }

        public static Intent GetStartIntent(Context context, ProxySettings settings)
        {
            var intent = new Intent(context, typeof(ProxyVpnService));
            intent.PutExtras(settings.ToBundle());
            return intent;
        }
    }
}

