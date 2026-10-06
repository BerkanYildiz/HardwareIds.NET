#include "Internal.hpp"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <iphlpapi.h>

#include <algorithm>
#include <cstring>
#include <thread>
#include <unordered_set>

namespace HardwareIds::Detail
{
    //
    // Addresses.
    //

    IpAddress IpAddress::FromV4(std::uint32_t InHostOrder)
    {
        IpAddress Address;
        Address.Bytes[0] = static_cast<std::uint8_t>(InHostOrder >> 24);
        Address.Bytes[1] = static_cast<std::uint8_t>(InHostOrder >> 16);
        Address.Bytes[2] = static_cast<std::uint8_t>(InHostOrder >> 8);
        Address.Bytes[3] = static_cast<std::uint8_t>(InHostOrder);
        return Address;
    }

    std::uint32_t IpAddress::ToV4() const
    {
        return this->IsV6 ? 0 : (static_cast<std::uint32_t>(this->Bytes[0]) << 24) | (this->Bytes[1] << 16) | (this->Bytes[2] << 8) | this->Bytes[3];
    }

    bool IpAddress::IsV6LinkLocal() const
    {
        return this->IsV6 && this->Bytes[0] == 0xFE && (this->Bytes[1] & 0xC0) == 0x80;
    }

    std::wstring IpAddress::ToString() const
    {
        wchar_t Buffer[64];

        if (!this->IsV6)
        {
            swprintf_s(Buffer, L"%u.%u.%u.%u", this->Bytes[0], this->Bytes[1], this->Bytes[2], this->Bytes[3]);
            return Buffer;
        }

        //
        // Format like .NET: lower-case groups without leading zeros, the longest run of two or more zero groups compressed,
        // and the last 32 bits written as IPv4 for the IPv4-compatible, IPv4-mapped, SIIT and ISATAP forms.
        //

        std::uint16_t Groups[8];

        for (auto I = 0; I < 8; I++)
            Groups[I] = static_cast<std::uint16_t>((this->Bytes[I * 2] << 8) | this->Bytes[I * 2 + 1]);

        auto EmbedsIPv4 = false;

        if (Groups[0] == 0 && Groups[1] == 0 && Groups[2] == 0 && Groups[3] == 0 && Groups[6] != 0)
            EmbedsIPv4 = (Groups[4] == 0 && (Groups[5] == 0 || Groups[5] == 0xFFFF)) || (Groups[4] == 0xFFFF && Groups[5] == 0);

        if (Groups[4] == 0 && Groups[5] == 0x5EFE)
            EmbedsIPv4 = true;

        auto GroupCount = EmbedsIPv4 ? 6 : 8;
        auto LongestStart = -1;
        auto LongestLength = 0;
        auto CurrentLength = 0;

        for (auto I = 0; I < GroupCount; I++)
        {
            if (Groups[I] == 0)
            {
                CurrentLength++;

                if (CurrentLength > LongestLength)
                {
                    LongestLength = CurrentLength;
                    LongestStart = I - CurrentLength + 1;
                }
            }
            else
            {
                CurrentLength = 0;
            }
        }

        if (LongestLength <= 1)
            LongestStart = -1;

        std::wstring Result;
        auto NeedsColon = false;

        for (auto I = 0; I < GroupCount; I++)
        {
            if (I == LongestStart)
            {
                Result += L"::";
                I += LongestLength - 1;
                NeedsColon = false;
                continue;
            }

            if (NeedsColon)
                Result += L':';

            swprintf_s(Buffer, L"%x", Groups[I]);
            Result += Buffer;
            NeedsColon = true;
        }

        if (EmbedsIPv4)
        {
            if (NeedsColon)
                Result += L':';

            swprintf_s(Buffer, L"%u.%u.%u.%u", this->Bytes[12], this->Bytes[13], this->Bytes[14], this->Bytes[15]);
            Result += Buffer;
        }

        if (this->ScopeId != 0)
            Result += L"%" + std::to_wstring(this->ScopeId);

        return Result;
    }

    static std::optional<IpAddress> FromSocketAddress(const SOCKADDR* InAddress)
    {
        if (InAddress == nullptr)
            return std::nullopt;

        IpAddress Address;

        if (InAddress->sa_family == AF_INET)
        {
            std::memcpy(Address.Bytes.data(), &reinterpret_cast<const sockaddr_in*>(InAddress)->sin_addr, 4);
            return Address;
        }

        if (InAddress->sa_family == AF_INET6)
        {
            auto V6 = reinterpret_cast<const sockaddr_in6*>(InAddress);
            Address.IsV6 = true;
            std::memcpy(Address.Bytes.data(), &V6->sin6_addr, 16);
            Address.ScopeId = V6->sin6_scope_id;
            return Address;
        }

        return std::nullopt;
    }

    //
    // Rules.
    //

    bool IsPhysicalAdapter(std::optional<std::int32_t> InCharacteristics, bool InIsPresent, std::optional<std::uint8_t> InInterfaceFlags)
    {
        //
        // What WMI reports as "PhysicalAdapter", derived from every case met so far:
        // - the driver declares the adapter physical (NCF_PHYSICAL); kernel debugger, Hyper-V switch, WAN miniport and VPN adapters declare NCF_VIRTUAL;
        // - the device is present; removed adapters (or those of the machine a VM image was captured on) keep their class key and even their interface;
        // - its network interface exists and has a connector (IF_FLAG_CONNECTOR_PRESENT).
        //

        constexpr std::int32_t NCF_PHYSICAL = 0x4;
        constexpr std::uint8_t IF_FLAG_CONNECTOR_PRESENT = 0x4;

        return InCharacteristics && (*InCharacteristics & NCF_PHYSICAL) != 0
            && InIsPresent
            && InInterfaceFlags && (*InInterfaceFlags & IF_FLAG_CONNECTOR_PRESENT) != 0;
    }

    bool HasUsableUnicastAddress(const std::vector<IpAddress>& InAddresses)
    {
        return std::any_of(InAddresses.begin(), InAddresses.end(), [](const IpAddress& InAddress) { return !InAddress.IsV6 || !InAddress.IsV6LinkLocal(); });
    }

    bool IsInSubnet(std::uint32_t InAddress, std::uint32_t InSubnetAddress, std::uint32_t InMask)
    {
        return (InAddress & InMask) == (InSubnetAddress & InMask);
    }

    std::vector<std::uint32_t> GetProbeAddresses(std::uint32_t InAddress, std::uint32_t InMask, int InCount)
    {
        std::vector<std::uint32_t> Result;

        //
        // Link-local (169.254.0.0/16) hosts pick random addresses, so probing the first ones is pointless.
        //

        if ((InAddress >> 16) == 0xA9FE)
            return Result;

        auto Network = InAddress & InMask;
        auto Broadcast = Network | ~InMask;

        for (auto Host = static_cast<std::uint64_t>(Network) + 1; Host < Broadcast && Host - Network <= static_cast<std::uint64_t>(InCount); Host++)
            Result.push_back(static_cast<std::uint32_t>(Host));

        return Result;
    }

    bool IsReportableNeighbor(ByteSpan InAddress, ByteSpan InPhysicalAddress, std::uint32_t InState)
    {
        if (InAddress.size() != 4 || InPhysicalAddress.size() != 6)
            return false;

        if (InState == NlnsUnreachable || InState == NlnsIncomplete || InState > NlnsPermanent)
            return false;

        if (InAddress[0] == 0 || InAddress[0] >= 224)
            return false;

        auto AllZero = std::all_of(InPhysicalAddress.begin(), InPhysicalAddress.end(), [](auto InByte) { return InByte == 0x00; });
        auto AllOnes = std::all_of(InPhysicalAddress.begin(), InPhysicalAddress.end(), [](auto InByte) { return InByte == 0xFF; });
        return !AllZero && !AllOnes;
    }

    //
    // Interfaces and neighbours.
    //

    struct InterfaceRow
    {
        GUID InterfaceGuid = {};
        NET_IFINDEX InterfaceIndex = 0;
        std::uint8_t Flags = 0;
        bool IsAdminUp = false;
        std::wstring Description;
        std::optional<Bytes> PhysicalAddress;
        std::optional<Bytes> PermanentPhysicalAddress;
    };

    static std::vector<InterfaceRow> GetInterfaces()
    {
        std::vector<InterfaceRow> Result;
        MIB_IF_TABLE2* Table = nullptr;

        if (GetIfTable2(&Table) != NO_ERROR || Table == nullptr)
            return Result;

        for (ULONG I = 0; I < Table->NumEntries; I++)
        {
            const auto& Row = Table->Table[I];
            auto Length = Row.PhysicalAddressLength;

            InterfaceRow Entry;
            Entry.InterfaceGuid = Row.InterfaceGuid;
            Entry.InterfaceIndex = Row.InterfaceIndex;
            std::memcpy(&Entry.Flags, &Row.InterfaceAndOperStatusFlags, 1);
            Entry.IsAdminUp = Row.AdminStatus == NET_IF_ADMIN_STATUS_UP;
            Entry.Description = Row.Description;

            if (Length != 0 && Length <= 32)
            {
                Entry.PhysicalAddress = Bytes(Row.PhysicalAddress, Row.PhysicalAddress + Length);
                Entry.PermanentPhysicalAddress = Bytes(Row.PermanentPhysicalAddress, Row.PermanentPhysicalAddress + Length);
            }

            Result.push_back(std::move(Entry));
        }

        FreeMibTable(Table);
        return Result;
    }

    struct Neighbor
    {
        NET_IFINDEX InterfaceIndex = 0;
        IpAddress Address;
        Bytes PhysicalAddress;
    };

    static std::vector<Neighbor> GetNeighbors()
    {
        std::vector<Neighbor> Result;
        MIB_IPNET_TABLE2* Table = nullptr;

        if (GetIpNetTable2(AF_INET, &Table) != NO_ERROR || Table == nullptr)
            return Result;

        for (ULONG I = 0; I < Table->NumEntries; I++)
        {
            const auto& Row = Table->Table[I];

            if (Row.Address.si_family != AF_INET)
                continue;

            std::uint8_t Address[4];
            std::memcpy(Address, &Row.Address.Ipv4.sin_addr, 4);
            Bytes PhysicalAddress(Row.PhysicalAddress, Row.PhysicalAddress + std::min<ULONG>(Row.PhysicalAddressLength, 32));

            if (!IsReportableNeighbor(Address, PhysicalAddress, static_cast<std::uint32_t>(Row.State)))
                continue;

            Neighbor Entry;
            Entry.InterfaceIndex = Row.InterfaceIndex;
            std::memcpy(Entry.Address.Bytes.data(), Address, 4);
            Entry.PhysicalAddress = std::move(PhysicalAddress);
            Result.push_back(std::move(Entry));
        }

        FreeMibTable(Table);
        return Result;
    }

    //
    // Network adapters.
    //

    static std::optional<int> ParseAdapterIndex(const std::wstring& InName)
    {
        //
        // The class key's adapter subkeys are named "0000", "0001"..., which is the index WMI reports.
        //

        if (InName.size() != 4)
            return std::nullopt;

        auto Value = 0;

        for (auto Character : InName)
        {
            if (Character < L'0' || Character > L'9')
                return std::nullopt;

            Value = Value * 10 + (Character - L'0');
        }

        return Value;
    }

    void RetrieveNetworkAdapters(Hwid& InHwid)
    {
        auto Interfaces = GetInterfaces();
        std::vector<HwNetworkAdapter> Entries;

        auto ClassKey = RegistryKey::OpenLocalMachine(L"SYSTEM\\CurrentControlSet\\Control\\Class\\" + FormatGuidBraces(GUID_DEVCLASS_NET_));

        if (!ClassKey)
            return;

        for (const auto& SubkeyName : ClassKey.GetSubKeyNames())
        {
            auto Index = ParseAdapterIndex(SubkeyName);

            if (!Index)
                continue;

            auto AdapterKey = ClassKey.OpenSubKey(SubkeyName);
            auto InterfaceGuidText = AdapterKey.GetString(L"NetCfgInstanceId");
            auto InterfaceGuid = InterfaceGuidText ? ParseGuid(*InterfaceGuidText) : std::nullopt;

            if (!InterfaceGuid)
                continue;

            auto InstanceId = AdapterKey.GetString(L"DeviceInstanceID");
            auto DevNode = InstanceId ? LocateDevNode(*InstanceId) : std::nullopt;
            auto Interface = std::find_if(Interfaces.begin(), Interfaces.end(), [&](const InterfaceRow& InRow) { return InRow.InterfaceGuid == *InterfaceGuid; });
            auto HasInterface = Interface != Interfaces.end();

            if (!IsPhysicalAdapter(AdapterKey.GetDword(L"Characteristics"), DevNode.has_value(), HasInterface ? std::optional<std::uint8_t>(Interface->Flags) : std::nullopt))
                continue;

            HwNetworkAdapter Entry;
            Entry.Id = *Index;
            Entry.InterfaceId = static_cast<int>(Interface->InterfaceIndex);
            Entry.Name = AdapterKey.GetString(L"DriverDesc");

            if (!Entry.Name)
                Entry.Name = Interface->Description;

            Entry.InterfaceGuid = InterfaceGuidText;
            Entry.ServiceName = GetDevNodeProperty(*DevNode, DEVPKEY_Device_Service_);
            Entry.IsPhysical = true;
            Entry.IsEnabled = Interface->IsAdminUp;

            if (auto Timestamp = AdapterKey.GetQword(L"NetworkInterfaceInstallTimestamp"); Timestamp && *Timestamp > 0)
                Entry.InstallDate = FromFileTime(*Timestamp);

            if (!Entry.InstallDate)
                Entry.InstallDate = GetDevNodeDateProperty(*DevNode, DEVPKEY_Device_InstallDate_);

            Entry.InstanceId = InstanceId;

            if (Interface->PhysicalAddress)
                Entry.Address.Current = FormatMacAddress(*Interface->PhysicalAddress);

            if (Interface->PermanentPhysicalAddress)
                Entry.Address.Permanent = FormatMacAddress(*Interface->PermanentPhysicalAddress);

            Entries.push_back(std::move(Entry));
        }

        std::stable_sort(Entries.begin(), Entries.end(), [](const HwNetworkAdapter& InLeft, const HwNetworkAdapter& InRight) { return InLeft.Id < InRight.Id; });
        InHwid.NetworkAdapters.insert(InHwid.NetworkAdapters.end(), std::make_move_iterator(Entries.begin()), std::make_move_iterator(Entries.end()));
    }

    //
    // Network signatures (readable when elevated only).
    //

    void RetrieveNetworkSignatures(Hwid& InHwid)
    {
        auto UnmanagedSignatures = RegistryKey::OpenLocalMachine(L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\NetworkList\\Signatures\\Unmanaged");

        if (!UnmanagedSignatures)
            return;

        for (const auto& SubkeyName : UnmanagedSignatures.GetSubKeyNames())
        {
            auto Signature = UnmanagedSignatures.OpenSubKey(SubkeyName);

            if (!Signature)
                continue;

            auto GatewayMac = Signature.GetBinary(L"DefaultGatewayMac");

            HwNetworkSignature Entry;
            Entry.Id = static_cast<int>(InHwid.NetworkSignatures.size());
            Entry.ProfileGuid = Signature.GetString(L"ProfileGuid");
            Entry.Name = Signature.GetString(L"Description");
            Entry.DefaultGatewayMac = GatewayMac ? FormatMacAddress(*GatewayMac) : std::wstring();
            InHwid.NetworkSignatures.push_back(std::move(Entry));
        }
    }

    //
    // Local network scan.
    //

    struct Subnet
    {
        std::uint32_t Address = 0;   // Host order.
        std::uint32_t Mask = 0;      // Host order.
    };

    struct KnownDevice
    {
        IpAddress Address;
        Bytes PhysicalAddress;
    };

    static void SetKnownDevice(std::vector<KnownDevice>& InDevices, const IpAddress& InAddress, const Bytes& InPhysicalAddress)
    {
        // Insertion-ordered map, like a .NET Dictionary that is never removed from.
        for (auto& Device : InDevices)
        {
            if (Device.Address == InAddress)
            {
                Device.PhysicalAddress = InPhysicalAddress;
                return;
            }
        }

        InDevices.push_back({ InAddress, InPhysicalAddress });
    }

    static const KnownDevice* FindKnownDevice(const std::vector<KnownDevice>& InDevices, const IpAddress& InAddress)
    {
        for (const auto& Device : InDevices)
        {
            if (Device.Address == InAddress)
                return &Device;
        }

        return nullptr;
    }

    static std::optional<Bytes> ResolveMacAddress(const IpAddress& InAddress)
    {
        if (InAddress.IsV6)
            return std::nullopt;

        //
        // Unspecified, loopback, multicast and broadcast addresses are not devices; Windows answers some of them with our own MAC.
        //

        if (InAddress.Bytes[0] == 0 || InAddress.Bytes[0] == 127 || InAddress.Bytes[0] >= 224)
            return std::nullopt;

        ULONG Destination;
        std::memcpy(&Destination, InAddress.Bytes.data(), 4);

        ULONG MacAddress[2] = {};
        ULONG MacAddressLength = 6;

        if (SendARP(Destination, 0, MacAddress, &MacAddressLength) != NO_ERROR || MacAddressLength != 6)
            return std::nullopt;

        Bytes Result(reinterpret_cast<const std::uint8_t*>(MacAddress), reinterpret_cast<const std::uint8_t*>(MacAddress) + 6);
        auto AllZero = std::all_of(Result.begin(), Result.end(), [](auto InByte) { return InByte == 0x00; });
        auto AllOnes = std::all_of(Result.begin(), Result.end(), [](auto InByte) { return InByte == 0xFF; });
        return AllZero || AllOnes ? std::nullopt : std::optional<Bytes>(std::move(Result));
    }

    static bool WaitFor(std::chrono::milliseconds InDuration, const std::stop_token& InStopToken)
    {
        // Returns false when the wait was cancelled.
        auto Deadline = std::chrono::steady_clock::now() + InDuration;

        while (std::chrono::steady_clock::now() < Deadline)
        {
            if (InStopToken.stop_requested())
                return false;

            std::this_thread::sleep_for(std::chrono::milliseconds(20));
        }

        return !InStopToken.stop_requested();
    }

    static bool ProbeNetworkDevices(const HwRouter& InEntry, const std::vector<Subnet>& InSubnets, const std::vector<IpAddress>& InOwnAddresses, std::vector<KnownDevice>& InKnownDevices, int InInterfaceIndex, std::chrono::milliseconds InWait, const std::stop_token& InStopToken)
    {
        //
        // One UDP datagram to the discard port of every host makes Windows resolve them all in parallel, without blocking;
        // after a short wait, the neighbour cache holds the MAC address of every host that answered.
        //

        std::vector<IpAddress> Targets;
        std::unordered_set<std::uint32_t> Seen;

        auto AddTarget = [&](const IpAddress& InAddress)
        {
            if (InAddress.IsV6 || !Seen.insert(InAddress.ToV4()).second)
                return;

            if (std::find(InOwnAddresses.begin(), InOwnAddresses.end(), InAddress) != InOwnAddresses.end() || FindKnownDevice(InKnownDevices, InAddress) != nullptr)
                return;

            Targets.push_back(InAddress);
        };

        for (const auto& Subnet : InSubnets)
        {
            for (auto Host : GetProbeAddresses(Subnet.Address, Subnet.Mask, NetworkProbeCount))
                AddTarget(IpAddress::FromV4(Host));
        }

        for (const auto& Gateway : InEntry.Gateways)
        {
            if (!Gateway.MacAddress && Gateway.Ip)
            {
                IN_ADDR Address;

                if (InetPtonW(AF_INET, Gateway.Ip->c_str(), &Address) == 1)
                {
                    IpAddress Target;
                    std::memcpy(Target.Bytes.data(), &Address, 4);
                    AddTarget(Target);
                }
            }
        }

        if (Targets.empty())
            return true;

        //
        // Like .NET, a socket that cannot be created or bound ends the probe right away.
        //

        auto Socket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);

        if (Socket == INVALID_SOCKET)
            return !InStopToken.stop_requested();

        if (!InSubnets.empty())
        {
            sockaddr_in Source = {};
            Source.sin_family = AF_INET;
            Source.sin_addr.s_addr = htonl(InSubnets.front().Address);

            if (bind(Socket, reinterpret_cast<const sockaddr*>(&Source), sizeof(Source)) == SOCKET_ERROR)
            {
                closesocket(Socket);
                return !InStopToken.stop_requested();
            }
        }

        const char Payload = 0;

        for (const auto& Target : Targets)
        {
            if (InStopToken.stop_requested())
            {
                closesocket(Socket);
                return false;
            }

            sockaddr_in Destination = {};
            Destination.sin_family = AF_INET;
            Destination.sin_port = htons(9);
            std::memcpy(&Destination.sin_addr, Target.Bytes.data(), 4);
            sendto(Socket, &Payload, 1, 0, reinterpret_cast<const sockaddr*>(&Destination), sizeof(Destination));
        }

        closesocket(Socket);

        if (!WaitFor(InWait, InStopToken))
            return false;

        for (const auto& Neighbor : GetNeighbors())
        {
            if (static_cast<int>(Neighbor.InterfaceIndex) != InInterfaceIndex)
                continue;

            if (std::find(Targets.begin(), Targets.end(), Neighbor.Address) != Targets.end())
                SetKnownDevice(InKnownDevices, Neighbor.Address, Neighbor.PhysicalAddress);
        }

        return true;
    }

    void ScanNetworkDevices(Hwid& InHwid, std::chrono::milliseconds InProbeWait, std::stop_token InStopToken)
    {
        WSADATA WsaData;

        if (WSAStartup(MAKEWORD(2, 2), &WsaData) != 0)
            return;

        //
        // The neighbour cache already holds every device the computer recently exchanged packets with; no probing needed.
        //

        auto Neighbors = GetNeighbors();

        ULONG Size = 16 * 1024;
        Bytes Buffer;
        ULONG Status = ERROR_BUFFER_OVERFLOW;

        for (auto Attempt = 0; Attempt < 4 && Status == ERROR_BUFFER_OVERFLOW; Attempt++)
        {
            Buffer.resize(Size);
            Status = GetAdaptersAddresses(AF_UNSPEC, GAA_FLAG_INCLUDE_GATEWAYS | GAA_FLAG_INCLUDE_WINS_INFO, nullptr, reinterpret_cast<IP_ADAPTER_ADDRESSES*>(Buffer.data()), &Size);
        }

        if (Status != NO_ERROR)
        {
            WSACleanup();
            return;
        }

        for (auto Adapter = reinterpret_cast<IP_ADAPTER_ADDRESSES*>(Buffer.data()); Adapter != nullptr; Adapter = Adapter->Next)
        {
            if (Adapter->OperStatus != IfOperStatusUp || Adapter->IfType == IF_TYPE_SOFTWARE_LOOPBACK)
                continue;

            //
            // NDIS filter modules (QoS scheduler, WFP filters, ...) show up as "Up" interfaces without any address; they are not networks.
            //

            std::vector<IpAddress> UnicastAddresses;
            std::vector<Subnet> Subnets;

            for (auto Unicast = Adapter->FirstUnicastAddress; Unicast != nullptr; Unicast = Unicast->Next)
            {
                auto Address = FromSocketAddress(Unicast->Address.lpSockaddr);

                if (!Address)
                    continue;

                UnicastAddresses.push_back(*Address);

                if (!Address->IsV6)
                {
                    ULONG Mask = 0;

                    if (ConvertLengthToIpv4Mask(Unicast->OnLinkPrefixLength, &Mask) == NO_ERROR)
                        Subnets.push_back({ Address->ToV4(), ntohl(Mask) });
                }
            }

            if (!HasUsableUnicastAddress(UnicastAddresses))
                continue;

            HwRouter Entry;
            Entry.Id = static_cast<int>(InHwid.Routers.size());

            auto InterfaceIndex = (Adapter->Flags & IP_ADAPTER_IPV4_ENABLED) != 0 ? static_cast<int>(Adapter->IfIndex) : -1;
            std::vector<KnownDevice> KnownDevices;

            for (const auto& Neighbor : Neighbors)
            {
                if (static_cast<int>(Neighbor.InterfaceIndex) != InterfaceIndex)
                    continue;

                if (std::find(UnicastAddresses.begin(), UnicastAddresses.end(), Neighbor.Address) == UnicastAddresses.end())
                    SetKnownDevice(KnownDevices, Neighbor.Address, Neighbor.PhysicalAddress);
            }

            //
            // Retrieve the gateways, DNS and DHCP servers. Gateways get their MAC address from the cache when it has it.
            //

            for (auto Gateway = Adapter->FirstGatewayAddress; Gateway != nullptr; Gateway = Gateway->Next)
            {
                auto Address = FromSocketAddress(Gateway->Address.lpSockaddr);

                if (!Address || Address->IsV6)
                    continue;

                HwNetworkDevice Device;
                Device.Ip = Address->ToString();

                if (auto Known = FindKnownDevice(KnownDevices, *Address))
                    Device.MacAddress = FormatMacAddress(Known->PhysicalAddress);

                Entry.Gateways.push_back(std::move(Device));
            }

            for (auto Dns = Adapter->FirstDnsServerAddress; Dns != nullptr; Dns = Dns->Next)
            {
                if (auto Address = FromSocketAddress(Dns->Address.lpSockaddr))
                    Entry.DnsServers.push_back(Address->ToString());
            }

            if (auto Dhcp = FromSocketAddress(Adapter->Dhcpv4Server.lpSockaddr); Dhcp && !Dhcp->IsV6)
                Entry.DhcpServers.push_back(Dhcp->ToString());

            //
            // Probe the subnets and the unresolved gateways, then list every device known for this interface.
            // A device must live in one of the subnets of the interface, and must not answer with a gateway's MAC
            // address: on-link routing and proxy ARP make off-link destinations look like neighbours otherwise.
            //

            auto Cancelled = false;

            if (!InStopToken.stop_requested())
            {
                Cancelled = !ProbeNetworkDevices(Entry, Subnets, UnicastAddresses, KnownDevices, InterfaceIndex, InProbeWait, InStopToken);

                for (auto& Gateway : Entry.Gateways)
                {
                    if (Gateway.MacAddress || !Gateway.Ip)
                        continue;

                    IpAddress Address;
                    IN_ADDR Raw;

                    if (InetPtonW(AF_INET, Gateway.Ip->c_str(), &Raw) != 1)
                        continue;

                    std::memcpy(Address.Bytes.data(), &Raw, 4);

                    if (auto Known = FindKnownDevice(KnownDevices, Address))
                        Gateway.MacAddress = FormatMacAddress(Known->PhysicalAddress);
                    else if (!Cancelled)
                    {
                        if (auto Resolved = ResolveMacAddress(Address))
                            Gateway.MacAddress = FormatMacAddress(*Resolved);
                    }
                }

                std::vector<std::wstring> GatewayAddresses;
                std::vector<std::wstring> GatewayMacs;

                for (const auto& Gateway : Entry.Gateways)
                {
                    if (Gateway.Ip)
                        GatewayAddresses.push_back(*Gateway.Ip);

                    if (Gateway.MacAddress)
                        GatewayMacs.push_back(*Gateway.MacAddress);
                }

                for (const auto& Known : KnownDevices)
                {
                    auto MacAddress = FormatMacAddress(Known.PhysicalAddress);
                    auto AddressText = Known.Address.ToString();

                    if (std::find(GatewayAddresses.begin(), GatewayAddresses.end(), AddressText) != GatewayAddresses.end())
                        continue;

                    if (std::any_of(GatewayMacs.begin(), GatewayMacs.end(), [&](const std::wstring& InMac) { return EqualsIgnoreCase(InMac, MacAddress); }))
                        continue;

                    if (std::any_of(Subnets.begin(), Subnets.end(), [&](const Subnet& InSubnet) { return IsInSubnet(Known.Address.ToV4(), InSubnet.Address, InSubnet.Mask); }))
                        Entry.NetworkDevices.push_back({ MacAddress, AddressText });
                }
            }

            std::sort(Entry.NetworkDevices.begin(), Entry.NetworkDevices.end(), [](const HwNetworkDevice& InLeft, const HwNetworkDevice& InRight)
            {
                IN_ADDR Left = {};
                IN_ADDR Right = {};
                InetPtonW(AF_INET, InLeft.Ip->c_str(), &Left);
                InetPtonW(AF_INET, InRight.Ip->c_str(), &Right);
                return ntohl(Left.s_addr) < ntohl(Right.s_addr);
            });

            if (!Entry.Gateways.empty() || !Entry.DnsServers.empty() || !Entry.DhcpServers.empty() || !Entry.NetworkDevices.empty())
                InHwid.Routers.push_back(std::move(Entry));

            if (Cancelled)
                break;
        }

        WSACleanup();
    }
}
