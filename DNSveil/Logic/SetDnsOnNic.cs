﻿using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using MsmhToolsClass;
using System.IO;

namespace DNSveil.Logic;

public class SetDnsOnNic
{
    private List<NetworkTool.NICResult> NICs { get; set; } = new();
    private string SavedDnssPath { get; set; } = Pathes.NicName;

    public readonly struct DefaultNicName
    {
        public static readonly string Auto = "Automatic";
    }

    public class ActiveNICs
    {
        public List<string> NICs { get; set; } = new();
        public async Task<string> PrimaryNic(IPAddress bootstrapIP, int bootstrapPort)
        {
            List<string> nics = NICs.ToList();
            int count = nics.Count;
            if (count > 1)
            {
                for (int n = 0; n < count; n++)
                {
                    string nicName = nics[n];
                    if (!string.IsNullOrEmpty(nicName))
                    {
                        NetworkInterface? nic = NetworkTool.GetNICByName(nicName);
                        if (nic != null)
                        {
                            if (nic.OperationalStatus == OperationalStatus.Up)
                            {
                                IPInterfaceStatistics statistics = nic.GetIPStatistics();
                                long br1 = statistics.BytesReceived;
                                long bs1 = statistics.BytesSent;
                                if (br1 > 0 && bs1 > 0)
                                {
                                    try
                                    {
                                        using TcpClient client = new();
                                        client.ReceiveTimeout = 200;
                                        client.SendTimeout = 200;
                                        await client.ConnectAsync(bootstrapIP, bootstrapPort);
                                    }
                                    catch (Exception) { }

                                    statistics = nic.GetIPStatistics();
                                    long br2 = statistics.BytesReceived;
                                    long bs2 = statistics.BytesSent;
                                    if (br2 > br1 || bs2 > bs1)
                                    {
                                        return nicName;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return nics.Any() ? nics[0] : string.Empty;
        }
    }

    public SetDnsOnNic() { }

    private async Task SaveToFile_Async()
    {
        try
        {
            string result = string.Empty;
            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
            {
                if (nics[n].IsDnsSetToLoopback)
                {
                    result += nics[n].NIC_Name + Environment.NewLine;
                }
            }

            // Save NIC names to file
            FileDirectory.CreateEmptyFile(SavedDnssPath);
            await File.WriteAllTextAsync(SavedDnssPath, result);
        }
        catch (Exception) { }
    }

    public ActiveNICs GetActiveNICs()
    {
        ActiveNICs activeNICs = new();

        try
        {
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();

            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
            {
                NetworkTool.NICResult nicR = nics[n];
                if (nicR.IsUpAndRunning) activeNICs.NICs.Add(nicR.NIC_Name);
            }
        }
        catch (Exception) { }

        return activeNICs;
    }

    private bool IsUpdatingNics = false;
    public async Task<(List<string> AllNICs, string PrimaryNIC)> UpdateNICs(IPAddress bootstrapIP, int bootstrapPort, bool selectAuto)
    {
        List<string> allNICs = new();
        string primaryActiveNic = string.Empty;

        if (IsUpdatingNics) return (allNICs, primaryActiveNic);
        IsUpdatingNics = true;

        try
        {
            List<NetworkTool.NICResult> nics = NetworkTool.GetAllNetworkInterfaces();

            if (nics.Count < 1)
            {
                Debug.WriteLine("There is no Network Interface.");
                return (allNICs, primaryActiveNic);
            }

            // Add NICs To List
            allNICs.Add(DefaultNicName.Auto);
            allNICs.AddRange(nics.Select(x => x.NIC_Name).ToArray());

            if (selectAuto)
            {
                primaryActiveNic = DefaultNicName.Auto;
            }
            else
            {
                ActiveNICs activeNICs = GetActiveNICs();
                primaryActiveNic = await activeNICs.PrimaryNic(bootstrapIP, bootstrapPort);

                if (string.IsNullOrWhiteSpace(primaryActiveNic))
                {
                    primaryActiveNic = DefaultNicName.Auto;
                }
            }
        }
        catch (Exception) { }

        IsUpdatingNics = false;
        return (allNICs, primaryActiveNic);
    }

    public bool IsDnsSet(string nicName, out bool isDnsSetOn)
    {
        bool isAnyDnsSet = false;
        bool isDnsSetOnOut = false;
        
        try
        {
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();

            ActiveNICs activeNICs = new();
            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
            {
                NetworkTool.NICResult nicR = nics[n];
                if (nicR.IsDnsSetToLoopback) isAnyDnsSet = true;
                if (nicR.IsUpAndRunning) activeNICs.NICs.Add(nicR.NIC_Name);
            }

            if (!string.IsNullOrEmpty(nicName))
            {
                if (nicName.Equals(DefaultNicName.Auto))
                {
                    isDnsSetOnOut = IsDnsSet(activeNICs.NICs);
                }
                else isDnsSetOnOut = IsDnsSet(nicName);
            }
        }
        catch (Exception) { }

        isDnsSetOn = isDnsSetOnOut;
        return isAnyDnsSet;
    }

    public bool IsDnsSet(string nicName, out bool isDnsSetOn, out ActiveNICs activeNICs)
    {
        bool isAnyDnsSet = false;
        bool isDnsSetOnOut = false;
        ActiveNICs activeNICsOut = new();

        try
        {
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();

            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
            {
                NetworkTool.NICResult nicR = nics[n];
                if (nicR.IsDnsSetToLoopback) isAnyDnsSet = true;
                if (nicR.IsUpAndRunning) activeNICsOut.NICs.Add(nicR.NIC_Name);
            }

            if (!string.IsNullOrEmpty(nicName))
            {
                if (nicName.Equals(DefaultNicName.Auto))
                {
                    isDnsSetOnOut = IsDnsSet(activeNICsOut.NICs);
                }
                else isDnsSetOnOut = IsDnsSet(nicName);
            }
        }
        catch (Exception) { }

        isDnsSetOn = isDnsSetOnOut;
        activeNICs = activeNICsOut;
        return isAnyDnsSet;
    }

    public bool IsDnsSet(List<string> nicNameList)
    {
        try
        {
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();

            int count = 0;
            for (int i = 0; i < nicNameList.Count; i++)
            {
                string nicName = nicNameList[i];
                List<NetworkTool.NICResult> nics = NICs.ToList();
                for (int n = 0; n < nics.Count; n++)
                    if (nics[n].NIC_Name.Equals(nicName) && nics[n].IsDnsSetToLoopback)
                    {
                        count++; break;
                    }
            }

            return nicNameList.Any() && nicNameList.Count == count;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SetDnsOnNic IsDnsSet 1: " + ex.Message);
            return false;
        }
    }

    public bool IsDnsSet(NetworkInterface nic)
    {
        try
        {
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();
            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
                if (nics[n].NIC_Name.Equals(nic.Name)) return nics[n].IsDnsSetToLoopback;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SetDnsOnNic IsDnsSet 2: " + ex.Message);
        }

        return false;
    }

    public bool IsDnsSet(string? nicName)
    {
        try
        {
            if (string.IsNullOrEmpty(nicName)) return false;
            // We Need To Get The New NIC With New Properties
            NICs = NetworkTool.GetNetworkInterfaces();
            List<NetworkTool.NICResult> nics = NICs.ToList();
            for (int n = 0; n < nics.Count; n++)
                if (nics[n].NIC_Name.Equals(nicName)) return nics[n].IsDnsSetToLoopback;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SetDnsOnNic IsDnsSet 3: " + ex.Message);
        }

        return false;
    }

    public async Task SetDns_Async(string nicName)
    {
        await Task.Run(async () => await NetworkTool.SetDnsIPv4Async(nicName, IPAddress.Loopback.ToString()));
        await Task.Run(async () => await NetworkTool.SetDnsIPv6Async(nicName, IPAddress.IPv6Loopback.ToString()));
        await SaveToFile_Async();
    }

    public async Task SetDns_Async(NetworkInterface nic)
    {
        await Task.Run(async () => await NetworkTool.SetDnsIPv4Async(nic, IPAddress.Loopback.ToString()));
        await Task.Run(async () => await NetworkTool.SetDnsIPv6Async(nic, IPAddress.IPv6Loopback.ToString()));
        await SaveToFile_Async();
    }

    public async Task SetDns_Async(List<string> nicNameList)
    {
        for (int n = 0; n < nicNameList.Count; n++)
        {
            string nicName = nicNameList[n];
            await SetDns_Async(nicName);
        }
    }

    public async Task UnsetDnsToDHCP_Async(string nicName)
    {
        if (string.IsNullOrEmpty(nicName)) return;
        await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName));
        await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName));
        await SaveToFile_Async();
    }

    public async Task UnsetDnsToDHCP_Async(List<string> nicNameList)
    {
        for (int n = 0; n < nicNameList.Count; n++)
        {
            string nicName = nicNameList[n];
            await UnsetDnsToDHCP_Async(nicName);
        }
    }

    public async Task UnsetDnsToDHCP_Async(NetworkInterface? nic)
    {
        if (nic == null) return;
        await UnsetDnsToDHCP_Async(nic.Name);
    }

    public async Task UnsetDnsToStatic_Async(string ipv4_1, string ipv4_2, string ipv6_1, string ipv6_2, string nicName)
    {
        if (string.IsNullOrEmpty(nicName)) return;

        ipv4_1 = ipv4_1.Trim();
        ipv4_2 = ipv4_2.Trim();
        ipv6_1 = ipv6_1.Trim();
        ipv6_2 = ipv6_2.Trim();

        if (NetworkTool.IsIP(ipv4_1, out IPAddress? ip41) && ip41 != null && NetworkTool.IsIPv4(ip41))
        {
            if (NetworkTool.IsIP(ipv4_2, out IPAddress? ip42) && ip42 != null && NetworkTool.IsIPv4(ip42))
            {
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName, ipv4_1, ipv4_2));
            }
            else
            {
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName, ipv4_1, null));
            }
        }
        else
        {
            await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName));
        }

        if (NetworkTool.IsIP(ipv6_1, out IPAddress? ip61) && ip61 != null && NetworkTool.IsIPv6(ip61))
        {
            if (NetworkTool.IsIP(ipv6_2, out IPAddress? ip62) && ip62 != null && NetworkTool.IsIPv6(ip62))
            {
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName, ipv6_1, ipv6_2));
            }
            else
            {
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName, ipv6_1, null));
            }
        }
        else
        {
            await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName));
        }

        await SaveToFile_Async();
    }

    public async Task UnsetDnsToStatic_Async(string ipv4_1, string ipv4_2, string ipv6_1, string ipv6_2, List<string> nicNameList)
    {
        for (int n = 0; n < nicNameList.Count; n++)
        {
            string nicName = nicNameList[n];
            await UnsetDnsToStatic_Async(ipv4_1, ipv4_2, ipv6_1, ipv6_2, nicName);
        }
    }

    public async Task UnsetDnsToStatic_Async(string ipv4_1, string ipv4_2, string ipv6_1, string ipv6_2, NetworkInterface? nic)
    {
        if (nic == null) return;
        await UnsetDnsToStatic_Async(ipv4_1, ipv4_2, ipv6_1, ipv6_2, nic.Name);
    }

    public async Task UnsetSavedDnssToDHCP_Async()
    {
        if (File.Exists(SavedDnssPath))
        {
            string content = string.Empty;

            try
            {
                content = await File.ReadAllTextAsync(SavedDnssPath);
            }
            catch (Exception) { }

            List<string> nicNames = content.SplitToLines();
            for (int n = 0; n < nicNames.Count; n++)
            {
                string nicName = nicNames[n].Trim();
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName));
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName));
            }
        }
    }

    public async Task UnsetSavedDnssToStatic_Async(string dns1, string dns2)
    {
        if (File.Exists(SavedDnssPath))
        {
            dns1 = dns1.Trim();
            dns2 = dns2.Trim();

            string content = string.Empty;

            try
            {
                content = await File.ReadAllTextAsync(SavedDnssPath);
            }
            catch (Exception) { }

            List<string> nicNames = content.SplitToLines();
            for (int n = 0; n < nicNames.Count; n++)
            {
                string nicName = nicNames[n].Trim();
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv4Async(nicName, dns1, dns2));
                await Task.Run(async () => await NetworkTool.UnsetDnsIPv6Async(nicName));
            }
        }
    }

    public List<NetworkTool.NICResult> GetNicsList => NICs.ToList();
}