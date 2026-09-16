namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        private static readonly string[] ChassisTypeNames =
        [
            "Unknown", "Other", "Unknown", "Desktop", "Low Profile Desktop", "Pizza Box", "Mini Tower", "Tower", "Portable", "Laptop",
            "Notebook", "Hand Held", "Docking Station", "All in One", "Sub Notebook", "Space-saving", "Lunch Box", "Main Server Chassis",
            "Expansion Chassis", "SubChassis", "Bus Expansion Chassis", "Peripheral Chassis", "RAID Chassis", "Rack Mount Chassis",
            "Sealed-case PC", "Multi-system chassis", "Compact PCI", "Advanced TCA", "Blade", "Blade Enclosure", "Tablet", "Convertible",
            "Detachable", "IoT Gateway", "Embedded PC", "Mini PC", "Stick PC",
        ];

        internal static void RetrieveChassis(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // SMBIOS type 3: System Enclosure or Chassis.
                // 

                foreach (var Chassis in InSmbios.OfType(3))
                {
                    var Type = Chassis.GetByte(0x05) & 0x7F;

                    InHwid.Chassis.Add(new HwChassis
                    {
                        Id = InHwid.Chassis.Count,
                        Manufacturer = Chassis.GetString(0x04),
                        Type = Type,
                        TypeName = GetChassisTypeName(Type),
                        Version = Chassis.GetString(0x06),
                        SerialNumber = Chassis.GetString(0x07),
                        AssetTag = Chassis.GetString(0x08),
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Gets the name of a SMBIOS chassis type.
        /// </summary>
        /// <param name="InType">The chassis type.</param>
        internal static string GetChassisTypeName(int InType)
        {
            return InType > 0 && InType < ChassisTypeNames.Length ? ChassisTypeNames[InType] : "Unknown";
        }
    }
}
