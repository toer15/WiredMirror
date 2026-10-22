using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WiredMirror;

internal sealed class AirPlaySetup : Form
{
    public AirPlaySetup()
    {
        Text = "AirPlay network setup"; ClientSize = new Size(650, 440);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = MaximizeBox = false;
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString());
        var text = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            Text = "1. Connect the iPhone and PC to the same trusted Wi-Fi/LAN. Avoid guest Wi-Fi, VPNs and router client isolation.\r\n\r\n" +
            "2. Allow the bundled AirPlay receiver through Windows Firewall using the button below. This adds rules for this app's receiver only, from the local subnet, including networks Windows labels Public. Use it on a network you trust. It does not turn off the firewall or change your network profile. Windows may ask for administrator approval.\r\n\r\n" +
            "3. Close other WiredMirror/AirPlay receiver windows. Choose Restart AirPlay. On iPhone: Control Centre > Screen Mirroring > WiredMirror. Then select that sender in the app and connect.\r\n\r\n" +
            "PC IPv4 addresses: " + string.Join(", ", addresses) + "\r\n\r\nIf the receiver still does not appear, check that multicast/mDNS traffic is allowed by your router. Bluetooth pairing does not enable AirPlay." };
        var allow = new Button { Text = "Allow AirPlay on this network", AutoSize = true };
        var close = new Button { Text = "Done", AutoSize = true, DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8) };
        buttons.Controls.AddRange([allow, close]);
        Controls.Add(text); Controls.Add(buttons);
        allow.Click += async (_, _) =>
        {
            allow.Enabled = false;
            try
            {
                var host = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Wireless", "iPhoneMirror.WirelessHost.exe"));
                if (!File.Exists(host)) throw new InvalidOperationException("Receiver is missing. Extract the full app package.");
                var rule = "WiredMirror-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host.ToUpperInvariant())))[..16];
                // All variable text is a quoted PowerShell literal, never command text.
                var pathLiteral = "'" + host.Replace("'", "''") + "'";
                var script = "$ErrorActionPreference='Stop'; try { $receiver=" + pathLiteral + "; " +
                    "foreach($protocol in @('TCP','UDP')) { $name='" + rule + "-'+$protocol; " +
                    "Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
                    "New-NetFirewallRule -Name $name -DisplayName ('WiredMirror AirPlay '+$protocol) -Direction Inbound -Action Allow -Program $receiver -Protocol $protocol -Profile Any -RemoteAddress LocalSubnet | Out-Null }; exit 0 } catch { exit 1 }";
                var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                info.Arguments = "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                using var process = Process.Start(info) ?? throw new InvalidOperationException("Windows did not start network setup.");
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new InvalidOperationException("Windows could not add the firewall rules. Check your administrator/network policy.");
                MessageBox.Show(this, "AirPlay is allowed for this receiver from the local subnet. Close this window and press Restart AirPlay.", "AirPlay setup");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "AirPlay setup", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { allow.Enabled = true; }
        };
    }
}
