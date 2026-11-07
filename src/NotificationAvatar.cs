using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SoulWebLauncher
{
    // Consumes only an optional image URL supplied by an official web notification.
    // It never accesses the browser profile, sends cookies, or persists the image.
    internal static class NotificationAvatar
    {
        private const int MaximumBytes = 1024 * 1024;
        private const int MaximumEdge = 2048;
        private const long MaximumPixels = 4000000;

        internal static async Task<Icon[]> TryLoadAsync(string iconUri, CancellationToken cancellationToken)
        {
            Uri address;
            if (cancellationToken.IsCancellationRequested || !TryGetAllowedAddress(iconUri, out address))
                return null;

            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    handler.UseCookies = false;
                    handler.AllowAutoRedirect = false;
                    handler.UseDefaultCredentials = false;
                    handler.Credentials = null;
                    handler.PreAuthenticate = false;
                    handler.AutomaticDecompression = DecompressionMethods.None;

                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        using (HttpResponseMessage response = await client.GetAsync(address,
                            HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                                return null;
                            if (response.Content.Headers.ContentLength.HasValue &&
                                response.Content.Headers.ContentLength.Value > MaximumBytes)
                                return null;
                            string mediaType = response.Content.Headers.ContentType == null
                                ? String.Empty : response.Content.Headers.ContentType.MediaType;
                            // System.Drawing does not decode WebP or SVG. Fall back instead.
                            if (!String.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase) &&
                                !String.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase) &&
                                !String.Equals(mediaType, "image/gif", StringComparison.OrdinalIgnoreCase))
                                return null;

                            using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (MemoryStream data = new MemoryStream())
                            {
                                byte[] buffer = new byte[8192];
                                int count;
                                while ((count = await input.ReadAsync(buffer, 0, buffer.Length,
                                    timeout.Token).ConfigureAwait(false)) > 0)
                                {
                                    if (data.Length + count > MaximumBytes)
                                        return null;
                                    data.Write(buffer, 0, count);
                                }
                                timeout.Token.ThrowIfCancellationRequested();
                                byte[] bytes = data.ToArray();
                                int width;
                                int height;
                                // Check declared dimensions before asking a decoder to allocate pixels.
                                if (!TryGetDimensions(bytes, mediaType, out width, out height) ||
                                    !AllowedDimensions(width, height))
                                    return null;
                                data.Position = 0;
                                using (Image avatar = Image.FromStream(data, false, true))
                                {
                                    if (!AllowedDimensions(avatar.Width, avatar.Height))
                                        return null;
                                    timeout.Token.ThrowIfCancellationRequested();
                                    Icon[] frames = AppIcons.CreateTrayFrames(avatar);
                                    if (timeout.IsCancellationRequested)
                                    {
                                        if (frames != null)
                                            foreach (Icon frame in frames) if (frame != null) frame.Dispose();
                                        return null;
                                    }
                                    return frames;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (HttpRequestException) { return null; }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
            catch (ExternalException) { return null; }
            // GDI+ also reports malformed or unsupported images as OutOfMemoryException.
            catch (OutOfMemoryException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        private static bool TryGetAllowedAddress(string value, out Uri address)
        {
            address = null;
            Uri candidate;
            if (!Uri.TryCreate(value, UriKind.Absolute, out candidate) ||
                candidate.Scheme != Uri.UriSchemeHttps || candidate.Port != 443 ||
                !String.IsNullOrEmpty(candidate.UserInfo))
                return false;
            string host = candidate.DnsSafeHost;
            if (!String.Equals(host, "soulapp.cn", StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith(".soulapp.cn", StringComparison.OrdinalIgnoreCase))
                return false;
            address = candidate;
            return true;
        }

        private static bool AllowedDimensions(int width, int height)
        {
            return width > 0 && height > 0 && width <= MaximumEdge && height <= MaximumEdge &&
                (long)width * height <= MaximumPixels;
        }

        private static bool TryGetDimensions(byte[] data, string mediaType, out int width, out int height)
        {
            width = height = 0;
            if (String.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                if (data.Length < 24) return false;
                for (int i = 0; i < signature.Length; i++) if (data[i] != signature[i]) return false;
                if (data[12] != 73 || data[13] != 72 || data[14] != 68 || data[15] != 82) return false;
                width = ReadBigEndian32(data, 16);
                height = ReadBigEndian32(data, 20);
                return true;
            }
            if (String.Equals(mediaType, "image/gif", StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length < 10 || data[0] != 71 || data[1] != 73 || data[2] != 70 ||
                    data[3] != 56 || (data[4] != 55 && data[4] != 57) || data[5] != 97) return false;
                width = data[6] | (data[7] << 8);
                height = data[8] | (data[9] << 8);
                return true;
            }
            if (!String.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                data.Length < 4 || data[0] != 255 || data[1] != 216) return false;
            int offset = 2;
            while (offset < data.Length)
            {
                if (data[offset++] != 255) return false;
                while (offset < data.Length && data[offset] == 255) offset++;
                if (offset >= data.Length) return false;
                int marker = data[offset++];
                if (marker == 217 || marker == 218) return false;
                if (marker == 1 || marker == 216 || (marker >= 208 && marker <= 215)) continue;
                if (offset + 2 > data.Length) return false;
                int length = (data[offset] << 8) | data[offset + 1];
                if (length < 2 || offset + length > data.Length) return false;
                bool startOfFrame = marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204;
                if (startOfFrame)
                {
                    if (length < 7) return false;
                    height = (data[offset + 3] << 8) | data[offset + 4];
                    width = (data[offset + 5] << 8) | data[offset + 6];
                    return true;
                }
                offset += length;
            }
            return false;
        }

        private static int ReadBigEndian32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
