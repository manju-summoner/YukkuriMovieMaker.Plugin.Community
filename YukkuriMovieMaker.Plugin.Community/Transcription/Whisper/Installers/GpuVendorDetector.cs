using System.Management;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers
{
    class GpuVendorDetector
    {
        /// <summary>
        /// ベンダー一覧。下の物ほど優先。
        /// </summary>
        public enum GpuVendor
        {
            Unknown = 0,
            Intel = 1,
            Amd = 2,
            Nvidia = 3,
        }

        record class GpuInfo(GpuVendor Vendor, ulong Ram);

        public static GpuVendor GetGpuVendor()
        {
            var infos = new List<GpuInfo>();
            using var searcher = new ManagementObjectSearcher("select PNPDeviceID, AdapterRAM from Win32_VideoController");
            foreach (var gpu in searcher.Get())
            {
                var pnp = gpu["PNPDeviceID"]?.ToString() ?? string.Empty;

                // PCI\VEN_10DE&DEV_1B80&SUBSYS_...
                //     ~~~~~~~~
                var idx = pnp.IndexOf("VEN_", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;

                var vid = pnp.Substring(idx + 4, 4).ToUpperInvariant();
                var vendor = vid switch
                {
                    "10DE" => GpuVendor.Nvidia,
                    "1002" or "1022" => GpuVendor.Amd,
                    "8086" => GpuVendor.Intel,
                    _ => GpuVendor.Unknown,
                };
                if(vendor is GpuVendor.Unknown)
                    continue;

                var ram = gpu["AdapterRAM"] is uint ramValue ? ramValue : 0;
                infos.Add(new GpuInfo(vendor, ram));
            }

            return infos
                .OrderByDescending(x => x.Ram)
                .ThenByDescending(x => x.Vendor)
                .Select(x => x.Vendor)
                .FirstOrDefault(GpuVendor.Unknown);
        }
    }
}
