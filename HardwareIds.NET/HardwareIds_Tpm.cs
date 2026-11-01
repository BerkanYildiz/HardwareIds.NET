namespace HardwareIds.NET
{
    using System;
    using System.Threading;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        /// <summary>
        /// Retrieves the TPM identity, capabilities and available public key material.
        /// </summary>
        internal static void RetrieveTpms(Hwid InHwid, CancellationToken InCancellationToken = default)
        {
            if (InCancellationToken.IsCancellationRequested)
                return;

            var Acquired = false;

            try
            {
                Tpm.QueryLock.Wait(InCancellationToken);
                Acquired = true;

                if (InCancellationToken.IsCancellationRequested)
                    return;

                var Status = Tpm.Tbsi_GetDeviceInfo(16, out var Info);

                if (Status == Tpm.TBS_E_TPM_NOT_FOUND)
                    return;

                var Device = new HwTpm { Id = InHwid.Tpms.Count };
                InHwid.Tpms.Add(Device);

                if (Status == 0)
                {
                    Device.Version = Info.tpmVersion == 2 ? "2.0" : Info.tpmVersion == 1 ? "1.2" : null;
                    Device.InterfaceType = Info.tpmInterfaceType;
                    Device.ImplementationRevision = Info.tpmImpRevision;
                }
                else
                {
                    Tpm.AddError(Device, "Tbsi_GetDeviceInfo", "TBS", Status);
                }

                if (Device.Version == "2.0" && !InCancellationToken.IsCancellationRequested)
                    Tpm.ReadCapabilities(Device, InCancellationToken);

                if (!InCancellationToken.IsCancellationRequested)
                    Tpm.ReadPublicMaterial(Device, InCancellationToken);
            }
            catch (Exception)
            {
                // ...
            }
            finally
            {
                if (Acquired)
                    Tpm.QueryLock.Release();
            }
        }
    }
}
